using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Minerva.Persistence.Entities;
using Minerva.Persistence.Relations;
using System.Text.Json;

namespace SemanticSearch.Services;

/// <summary>
/// Named Entity Recognition und Relation Extraction Service.
/// Extrahiert Entities (Person, Organization, Event) und deren Beziehungen aus Text.
/// </summary>
public class NerService : IDisposable
{
    private readonly EmbeddingService _embeddingService;
    private InferenceSession? _nerSession;
    private readonly string _apiKey; // Optional: für Claude API

    public NerService(EmbeddingService embeddingService, string? apiKey = null)
    {
        _embeddingService = embeddingService;
        _apiKey = apiKey ?? string.Empty;
    }

    /// <summary>
    /// Extrahiert Entities aus einem Text-Dokument.
    /// Verwendet zuerst einfache Heuristiken, dann optional LLM-Klassifikation.
    /// </summary>
    public async Task<List<ExtractedEntity>> ExtractEntitiesAsync(
        string text,
        bool useLLM = false)
    {
        var entities = new List<ExtractedEntity>();

        // Phase 1: Pattern-basierte Extraktion
        entities.AddRange(ExtractWithPatterns(text));

        // Phase 2: NER-Modell (falls ONNX-Modell verfügbar)
        if (_nerSession != null)
        {
            var nerEntities = await ExtractWithNerModelAsync(text);
            entities.AddRange(nerEntities);
        }

        // Phase 3: LLM-basierte Verfeinerung (optional)
        if (useLLM && !string.IsNullOrEmpty(_apiKey))
        {
            entities = await RefineWithLLMAsync(text, entities);
        }

        // Deduplizierung
        return DeduplicateEntities(entities);
    }

    /// <summary>
    /// Extrahiert Relationen zwischen bereits identifizierten Entities.
    /// </summary>
    public async Task<List<ExtractedRelation>> ExtractRelationsAsync(
        string text,
        List<ExtractedEntity> entities,
        bool useLLM = false)
    {
        var relations = new List<ExtractedRelation>();

        // Phase 1: Pattern-basierte Relation-Extraktion
        relations.AddRange(ExtractRelationsWithPatterns(text, entities));

        // Phase 2: LLM-basierte Relation-Extraktion
        if (useLLM && !string.IsNullOrEmpty(_apiKey))
        {
            var llmRelations = await ExtractRelationsWithLLMAsync(text, entities);
            relations.AddRange(llmRelations);
        }

        return DeduplicateRelations(relations);
    }

    #region Pattern-based Extraction

    private List<ExtractedEntity> ExtractWithPatterns(string text)
    {
        var entities = new List<ExtractedEntity>();

        // Organisationen: Typische Suffixe und Präfixe
        var orgPatterns = new[]
        {
            @"\b([A-ZÄÖÜ][a-zäöüß]+(?: [A-ZÄÖÜ][a-zäöüß]+)*)\s+(AG|GmbH|SE|Inc\.|Corp\.|Ltd\.|Bank|Foundation|Trust)\b",
            @"\b(United Nations|World Bank|IMF|WHO|NATO|EU|OECD|WTO|BIS)\b",
            @"\b(CIA|FBI|NSA|MI6|Mossad|BND|KGB|FSB)\b",
        };

        foreach (var pattern in orgPatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                entities.Add(new ExtractedEntity
                {
                    Text = match.Value,
                    Type = "Organization",
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Confidence = 0.7f
                });
            }
        }

        // Personen: Titel + Name
        var personPatterns = new[]
        {
            @"\b(Mr\.|Mrs\.|Ms\.|Dr\.|Prof\.|CEO|President|Director)\s+([A-ZÄÖÜ][a-zäöüß]+(?:\s+[A-ZÄÖÜ][a-zäöüß]+)+)\b",
            @"\b([A-ZÄÖÜ][a-zäöüß]+\s+[A-ZÄÖÜ][a-zäöüß]+)\b", // Einfach: Vorname Nachname
        };

        foreach (var pattern in personPatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Filter out known organizations
                if (!entities.Any(e => e.Text == match.Value && e.Type == "Organization"))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Text = match.Groups[match.Groups.Count - 1].Value,
                        Type = "Person",
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Confidence = 0.6f
                    });
                }
            }
        }

        // Events: Datumsangaben + Kontext
        var eventPatterns = new[]
        {
            @"\b(Meeting|Conference|Summit|Agreement|Treaty|Crisis|War)\s+(?:on|in|of)\s+(\d{4})\b",
        };

        foreach (var pattern in eventPatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                entities.Add(new ExtractedEntity
                {
                    Text = match.Value,
                    Type = "Event",
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Confidence = 0.5f
                });
            }
        }

        return entities;
    }

    private List<ExtractedRelation> ExtractRelationsWithPatterns(
        string text,
        List<ExtractedEntity> entities)
    {
        var relations = new List<ExtractedRelation>();

        // Muster für Arbeitsbeziehungen
        var employmentPatterns = new[]
        {
            @"(\w+)\s+(?:is|was|works|worked)\s+(?:as|at|for)\s+(?:CEO|Director|President|Chairman|Manager)\s+(?:of|at)\s+(\w+)",
            @"(\w+)\s+(?:joined|founded|leads|manages)\s+(\w+)",
        };

        foreach (var pattern in employmentPatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var source = entities.FirstOrDefault(e => match.Value.Contains(e.Text) && e.Type == "Person");
                var target = entities.FirstOrDefault(e => match.Value.Contains(e.Text) && e.Type == "Organization");

                if (source != null && target != null)
                {
                    relations.Add(new ExtractedRelation
                    {
                        SourceEntity = source,
                        TargetEntity = target,
                        RelationType = "Work:Employment",
                        Confidence = 0.6f,
                        Context = match.Value
                    });
                }
            }
        }

        // Weitere Pattern für andere Relationstypen...

        return relations;
    }

    #endregion

    #region LLM-based Extraction

    private async Task<List<ExtractedEntity>> RefineWithLLMAsync(
        string text,
        List<ExtractedEntity> candidates)
    {
        // Verwende Claude API für präzise Klassifikation
        var prompt = BuildEntityClassificationPrompt(text, candidates);
        var response = await CallClaudeAPIAsync(prompt);

        return ParseEntityResponse(response);
    }

    private async Task<List<ExtractedRelation>> ExtractRelationsWithLLMAsync(
        string text,
        List<ExtractedEntity> entities)
    {
        var prompt = BuildRelationExtractionPrompt(text, entities);
        var response = await CallClaudeAPIAsync(prompt);

        return ParseRelationResponse(response, entities);
    }

    private string BuildEntityClassificationPrompt(string text, List<ExtractedEntity> candidates)
    {
        var entityList = string.Join("\n", candidates.Select((e, i) =>
            $"{i + 1}. \"{e.Text}\" (tentative type: {e.Type})"));

        return $@"Given the following text and candidate entities, classify each entity precisely into one of these types:

**Entity Types:**
- Person: Real individuals (alive, dead, or historical)
- Organization: Companies, NGOs, governments, agencies
  - Organization:Company
  - Organization:NGO
  - Organization:Bank
  - Organization:Agency (CIA, FBI, etc.)
  - Organization:SecretService
  - Organization:NewsMediaOrganization
- Event: Meetings, conferences, crises, wars, agreements

**Text:**
{text}

**Candidate Entities:**
{entityList}

For each entity, respond with:
EntityNumber | FinalType | Confidence (0-1) | AdditionalInfo

Example:
1 | Person | 0.95 | CEO of BlackRock
2 | Organization:Bank | 0.9 | Central bank

Respond ONLY with the classifications, one per line.";
    }

    private string BuildRelationExtractionPrompt(string text, List<ExtractedEntity> entities)
    {
        var entityList = string.Join("\n", entities.Select((e, i) =>
            $"{i + 1}. {e.Type}: \"{e.Text}\""));

        return $@"Given the following text and identified entities, extract all relationships between them.

**Relation Types:**
- Work:Employment (person works at organization)
- Work:Leadership (person leads/founded organization)
- Work:Membership (person is member of organization)
- Influence:Control (entity controls another entity)
- Family:Parent, Family:Child, Family:Spouse
- Event:Organizer, Event:Participant

**Text:**
{text}

**Entities:**
{entityList}

For each relation, respond with:
SourceEntityNumber | RelationType | TargetEntityNumber | FromDate | ToDate | Role/Context

Example:
1 | Work:Leadership | 2 | 2012 | present | CEO
3 | Event:Participant | 4 | 2023-06 | 2023-06 | Speaker

Respond ONLY with the relations, one per line.";
    }

    private async Task<string> CallClaudeAPIAsync(string prompt)
    {
        // Implementierung des Claude API Calls
        // Falls API nicht verfügbar, return empty string
        if (string.IsNullOrEmpty(_apiKey))
            return string.Empty;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var request = new
        {
            model = "claude-3-5-sonnet-20241022",
            max_tokens = 2048,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        return result.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private List<ExtractedEntity> ParseEntityResponse(string response)
    {
        var entities = new List<ExtractedEntity>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3)
            {
                entities.Add(new ExtractedEntity
                {
                    Text = parts[1],
                    Type = parts[1],
                    Confidence = float.TryParse(parts[2], out var conf) ? conf : 0.8f,
                    AdditionalInfo = parts.Length > 3 ? parts[3] : null
                });
            }
        }

        return entities;
    }

    private List<ExtractedRelation> ParseRelationResponse(
        string response,
        List<ExtractedEntity> entities)
    {
        var relations = new List<ExtractedRelation>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3)
            {
                var sourceIdx = int.Parse(parts[0]) - 1;
                var targetIdx = int.Parse(parts[2]) - 1;

                if (sourceIdx >= 0 && sourceIdx < entities.Count &&
                    targetIdx >= 0 && targetIdx < entities.Count)
                {
                    relations.Add(new ExtractedRelation
                    {
                        SourceEntity = entities[sourceIdx],
                        TargetEntity = entities[targetIdx],
                        RelationType = parts[1],
                        FromDate = parts.Length > 3 ? ParseDate(parts[3]) : null,
                        ToDate = parts.Length > 4 ? ParseDate(parts[4]) : null,
                        Context = parts.Length > 5 ? parts[5] : null,
                        Confidence = 0.8f
                    });
                }
            }
        }

        return relations;
    }

    #endregion

    #region Helper Methods

    private async Task<List<ExtractedEntity>> ExtractWithNerModelAsync(string text)
    {
        // TODO: Implementierung mit ONNX NER-Modell
        // Z.B. dslim/bert-base-NER oder xlm-roberta-large-finetuned-conll03-german
        return new List<ExtractedEntity>();
    }

    private List<ExtractedEntity> DeduplicateEntities(List<ExtractedEntity> entities)
    {
        return entities
            .GroupBy(e => new { e.Text, e.Type })
            .Select(g => g.OrderByDescending(e => e.Confidence).First())
            .ToList();
    }

    private List<ExtractedRelation> DeduplicateRelations(List<ExtractedRelation> relations)
    {
        return relations
            .GroupBy(r => new
            {
                SourceText = r.SourceEntity.Text,
                TargetText = r.TargetEntity.Text,
                r.RelationType
            })
            .Select(g => g.OrderByDescending(r => r.Confidence).First())
            .ToList();
    }

    private DateOnly? ParseDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr) || dateStr == "present")
            return null;

        if (DateOnly.TryParse(dateStr, out var date))
            return date;

        return null;
    }

    #endregion

    public void Dispose()
    {
        _nerSession?.Dispose();
    }
}

#region Data Models

public class ExtractedEntity
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public float Confidence { get; set; }
    public string? AdditionalInfo { get; set; }
}

public class ExtractedRelation
{
    public ExtractedEntity SourceEntity { get; set; } = null!;
    public ExtractedEntity TargetEntity { get; set; } = null!;
    public string RelationType { get; set; } = string.Empty;
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Context { get; set; }
    public float Confidence { get; set; }
}

#endregion
