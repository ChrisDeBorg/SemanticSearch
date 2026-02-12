using SemanticSearch.Services;
using Minerva.Persistence.Entities;

namespace SemanticSearch.Examples;

/// <summary>
/// Beispiel: Verwendung der NER-Pipeline für Knowledge Graph Extraktion.
/// </summary>
public class NerPipelineExample
{
    private readonly NerService _nerService;
    private readonly EntityPersistenceService _persistenceService;
    private readonly SemanticSearchManager _searchManager;

    public NerPipelineExample(
        NerService nerService,
        EntityPersistenceService persistenceService,
        SemanticSearchManager searchManager)
    {
        _nerService = nerService;
        _persistenceService = persistenceService;
        _searchManager = searchManager;
    }

    /// <summary>
    /// Vollständiger Workflow: Dokument indexieren → Entities extrahieren → In DB speichern.
    /// </summary>
    public async Task ProcessDocumentAsync(string filePath, bool useLLM = false)
    {
        // 1. Dokument für semantische Suche indexieren
        Console.WriteLine("📄 Indexiere Dokument für Suche...");
        var indexResult = await _searchManager.IndexDocumentAsync(filePath);

        if (!indexResult.Success)
        {
            Console.WriteLine($"❌ Fehler beim Indexieren: {indexResult.ErrorMessage}");
            return;
        }

        Console.WriteLine($"✅ {indexResult.TotalChunks} Chunks indexiert");

        // 2. Dokument-Text laden
        var text = await File.ReadAllTextAsync(filePath);

        // 3. Entities extrahieren
        Console.WriteLine("\n🔍 Extrahiere Entities...");
        var entities = await _nerService.ExtractEntitiesAsync(text, useLLM);

        Console.WriteLine($"✅ {entities.Count} Entities gefunden:");
        foreach (var entity in entities.Take(10))
        {
            Console.WriteLine($"  - {entity.Type}: {entity.Text} (Confidence: {entity.Confidence:P0})");
        }

        // 4. Relationen extrahieren
        Console.WriteLine("\n🔗 Extrahiere Relationen...");
        var relations = await _nerService.ExtractRelationsAsync(text, entities, useLLM);

        Console.WriteLine($"✅ {relations.Count} Relationen gefunden:");
        foreach (var relation in relations.Take(10))
        {
            Console.WriteLine($"  - {relation.SourceEntity.Text} → [{relation.RelationType}] → {relation.TargetEntity.Text}");
        }

        // 5. In Datenbank speichern
        Console.WriteLine("\n💾 Speichere in Datenbank...");
        var entityMapping = await _persistenceService.PersistEntitiesAsync(entities, indexResult.DocumentId!);
        await _persistenceService.PersistRelationsAsync(relations, entityMapping);

        Console.WriteLine($"✅ {entityMapping.Count} Entities und {relations.Count} Relationen gespeichert");
    }

    /// <summary>
    /// Batch-Verarbeitung mehrerer Dokumente.
    /// </summary>
    public async Task ProcessMultipleDocumentsAsync(
        List<string> filePaths,
        bool useLLM = false,
        IProgress<BatchProgress>? progress = null)
    {
        for (int i = 0; i < filePaths.Count; i++)
        {
            var filePath = filePaths[i];

            progress?.Report(new BatchProgress
            {
                CurrentFile = i + 1,
                TotalFiles = filePaths.Count,
                CurrentFilename = Path.GetFileName(filePath),
                Percentage = (int)((i / (float)filePaths.Count) * 100)
            });

            try
            {
                await ProcessDocumentAsync(filePath, useLLM);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler bei {filePath}: {ex.Message}");
            }
        }

        progress?.Report(new BatchProgress
        {
            CurrentFile = filePaths.Count,
            TotalFiles = filePaths.Count,
            Percentage = 100,
            IsCompleted = true
        });
    }

    /// <summary>
    /// Beispiel: Suche nach allen Personen die mit "BlackRock" verbunden sind.
    /// </summary>
    public async Task FindRelatedPersonsExample(string organizationName)
    {
        // 1. Suche nach der Organisation in der semantischen Suche
        var searchResults = await _searchManager.SearchAsync(organizationName, limit: 1);

        if (!searchResults.Any())
        {
            Console.WriteLine($"Organisation '{organizationName}' nicht gefunden.");
            return;
        }

        // 2. Hole alle Relationen aus der Datenbank
        // (Benötigt DbContext-Zugriff - vereinfacht dargestellt)
        Console.WriteLine($"\n🔍 Personen verbunden mit {organizationName}:");
        Console.WriteLine("  - [Hier würden die Relationen aus der DB geladen werden]");
    }
}

public class BatchProgress
{
    public int CurrentFile { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFilename { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public bool IsCompleted { get; set; }
}


/// <summary>
/// Beispiel: Minimale Console-App zum Testen.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup
        var dbPath = "knowledge_graph.db";
        var modelPath = "models/all-MiniLM-L6-v2.onnx";
        var claudeApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        var embeddingService = new EmbeddingService(modelPath);
        var searchManager = new SemanticSearchManager(dbPath, modelPath, 384);
        await searchManager.InitializeAsync();

        var nerService = new NerService(embeddingService, claudeApiKey);

        // DbContext hier initialisieren (vereinfacht)
        // var dbContext = new YourDbContext();
        // var persistenceService = new EntityPersistenceService(dbContext);

        // var pipeline = new NerPipelineExample(nerService, persistenceService, searchManager);

        // Dokument verarbeiten
        // await pipeline.ProcessDocumentAsync("document.pdf", useLLM: true);

        Console.WriteLine("NER Pipeline bereit!");
    }
}
