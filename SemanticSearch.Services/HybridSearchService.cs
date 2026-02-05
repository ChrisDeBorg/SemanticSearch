using FuzzySharp;

namespace SemanticSearch.Services;

public class HybridSearchService
{
    private readonly VectorSearchService _vectorSearch;
    private readonly EmbeddingService _embeddingService;
    private readonly int _fuzzyThreshold;

    public HybridSearchService(
        VectorSearchService vectorSearch,
        EmbeddingService embeddingService,
        int fuzzyThreshold = 70)
    {
        _vectorSearch = vectorSearch;
        _embeddingService = embeddingService;
        _fuzzyThreshold = fuzzyThreshold;
    }

    public async Task<List<HybridSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        SearchMode mode = SearchMode.Hybrid,
        string? documentIdFilter = null,
        int? pageNumberFilter = null)
    {
        var results = new List<HybridSearchResult>();

        // Semantische Suche
        if (mode == SearchMode.Semantic || mode == SearchMode.Hybrid)
        {
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);
            var semanticResults = await _vectorSearch.SearchAsync(
                queryEmbedding,
                limit * 2, // Hole mehr Ergebnisse für Hybrid-Modus
                documentIdFilter,
                pageNumberFilter);

            foreach (var result in semanticResults)
            {
                results.Add(new HybridSearchResult
                {
                    ChunkId = result.ChunkId,
                    DocumentId = result.DocumentId,
                    Content = result.Content,
                    ChunkIndex = result.ChunkIndex,
                    PageNumber = result.PageNumber,
                    Filename = result.FileName,
                    Filepath = result.FilePath,
                    FileType = result.FileType,
                    SemanticScore = result.Score,
                    FuzzyScore = 0,
                    CombinedScore = result.Score,
                    MatchType = MatchType.Semantic
                });
            }
        }

        // Fuzzy-Suche (Tippfehlertoleranz)
        if (mode == SearchMode.Fuzzy || mode == SearchMode.Hybrid)
        {
            var allDocuments = await _vectorSearch.GetAllDocumentsAsync();
            var fuzzyResults = new List<HybridSearchResult>();

            // Suche in allen Dokumenten (könnte optimiert werden mit Volltext-Index)
            foreach (var doc in allDocuments)
            {
                if (!string.IsNullOrEmpty(documentIdFilter) && doc.Id != documentIdFilter)
                    continue;

                var fuzzyMatches = await FindFuzzyMatchesInDocument(doc.Id, query, pageNumberFilter);
                fuzzyResults.AddRange(fuzzyMatches);
            }

            // Füge Fuzzy-Ergebnisse hinzu
            foreach (var fuzzyResult in fuzzyResults.Where(r => r.FuzzyScore >= _fuzzyThreshold))
            {
                var existing = results.FirstOrDefault(r => r.ChunkId == fuzzyResult.ChunkId);

                if (existing != null)
                {
                    // Kombiniere Scores wenn Chunk bereits in semantischen Ergebnissen
                    existing.FuzzyScore = fuzzyResult.FuzzyScore;
                    existing.CombinedScore = CombineScores(existing.SemanticScore, existing.FuzzyScore);
                    existing.MatchType = MatchType.Both;
                }
                else
                {
                    results.Add(fuzzyResult);
                }
            }
        }

        // Sortiere nach kombiniertem Score und limitiere
        return results
            .OrderByDescending(r => r.CombinedScore)
            .Take(limit)
            .ToList();
    }

    private async Task<List<HybridSearchResult>> FindFuzzyMatchesInDocument(
        string documentId,
        string query,
        int? pageNumberFilter)
    {
        var results = new List<HybridSearchResult>();

        // Hole alle Chunks des Dokuments (in Produktion würde man hier einen Volltext-Index verwenden)
        var chunks = await GetDocumentChunks(documentId, pageNumberFilter);

        var queryLower = query.ToLowerInvariant();
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var chunk in chunks)
        {
            var contentLower = chunk.Content.ToLowerInvariant();

            // Token-basiertes Fuzzy-Matching
            var maxScore = 0;

            // Prüfe Partial Ratio für den gesamten Query
            var partialScore = Fuzz.PartialRatio(queryLower, contentLower);
            maxScore = Math.Max(maxScore, partialScore);

            // Prüfe Token Set Ratio
            var tokenSetScore = Fuzz.TokenSetRatio(queryLower, contentLower);
            maxScore = Math.Max(maxScore, tokenSetScore);

            // Prüfe einzelne Query-Wörter
            foreach (var word in queryWords)
            {
                var wordScore = Fuzz.PartialRatio(word, contentLower);
                maxScore = Math.Max(maxScore, wordScore);
            }

            if (maxScore >= _fuzzyThreshold)
            {
                results.Add(new HybridSearchResult
                {
                    ChunkId = chunk.Id,
                    DocumentId = chunk.DocumentId,
                    Content = chunk.Content,
                    ChunkIndex = chunk.ChunkIndex,
                    PageNumber = chunk.PageNumber,
                    Filename = chunk.FileName,
                    Filepath = chunk.FilePath,
                    FileType = chunk.FileType,
                    SemanticScore = 0,
                    FuzzyScore = maxScore / 100f,
                    CombinedScore = maxScore / 100f,
                    MatchType = MatchType.Fuzzy
                });
            }
        }

        return results;
    }

    private async Task<List<SearchResult>> GetDocumentChunks(string documentId, int? pageNumberFilter)
    {
        // Verwende eine sehr große Zahl für limit um alle Chunks zu bekommen
        // In Produktion würde man hier direkten Datenbankzugriff verwenden
        var dummyEmbedding = new float[_embeddingService.EmbeddingDimension];
        return await _vectorSearch.SearchAsync(dummyEmbedding, 4096, documentId, pageNumberFilter);
    }

    private float CombineScores(float semanticScore, float fuzzyScore)
    {
        // Gewichtete Kombination: 70% semantisch, 30% fuzzy
        return (semanticScore * 0.7f) + (fuzzyScore * 0.3f);
    }

    public async Task<List<string>> SuggestCorrections(string query, int maxSuggestions = 5)
    {
        // Einfache Tippfehlerkorrektur basierend auf indizierten Dokumenten
        var allDocuments = await _vectorSearch.GetAllDocumentsAsync();
        var suggestions = new HashSet<string>();

        foreach (var doc in allDocuments)
        {
            var words = doc.Filename.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                var score = Fuzz.Ratio(query.ToLowerInvariant(), word.ToLowerInvariant());
                if (score >= 70)
                {
                    suggestions.Add(word);
                }
            }

            if (suggestions.Count >= maxSuggestions)
                break;
        }

        return suggestions.Take(maxSuggestions).ToList();
    }
}

public class HybridSearchResult
{
    public string ChunkId { get; set; }
    public string DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string Filepath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public float SemanticScore { get; set; }
    public float FuzzyScore { get; set; }
    public float CombinedScore { get; set; }
    public MatchType MatchType { get; set; }

    public string GetHighlightedContent(string query, int contextLength = 100)
    {
        var queryWords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var contentLower = Content.ToLowerInvariant();

        // Finde beste Übereinstimmung
        var bestPosition = -1;
        var bestScore = 0;

        foreach (var word in queryWords)
        {
            var position = contentLower.IndexOf(word, StringComparison.Ordinal);
            if (position >= 0)
            {
                var score = Fuzz.PartialRatio(word, contentLower.Substring(position, Math.Min(word.Length + 10, contentLower.Length - position)));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPosition = position;
                }
            }
        }

        // Extrahiere Kontext
        if (bestPosition >= 0)
        {
            var start = Math.Max(0, bestPosition - contextLength / 2);
            var length = Math.Min(contextLength, Content.Length - start);
            var snippet = Content.Substring(start, length);

            if (start > 0) snippet = "..." + snippet;
            if (start + length < Content.Length) snippet += "...";

            return snippet;
        }

        // Fallback: Zeige Anfang des Contents
        return Content.Length > contextLength
            ? Content.Substring(0, contextLength) + "..."
            : Content;
    }
}

public enum SearchMode
{
    Semantic,
    Fuzzy,
    Hybrid
}

public enum MatchType
{
    Semantic,
    Fuzzy,
    Both
}