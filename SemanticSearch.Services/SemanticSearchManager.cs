namespace SemanticSearch.Services;

public class SemanticSearchManager : IDisposable
{
    private readonly VectorSearchService _vectorSearch;
    private readonly EmbeddingService _embeddingService;
    private readonly DocumentParserService _documentParser;
    private readonly HybridSearchService _hybridSearch;
    private bool _isInitialized;

    public SemanticSearchManager(
        string databasePath,
        string onnxModelPath,
        int embeddingDimension = 384)
    {
        _embeddingService = new EmbeddingService(onnxModelPath, embeddingDimension: embeddingDimension);
        _vectorSearch = new VectorSearchService(databasePath, embeddingDimension);
        _documentParser = new DocumentParserService(chunkSize: 500, chunkOverlap: 50);
        _hybridSearch = new HybridSearchService(_vectorSearch, _embeddingService);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _vectorSearch.InitializeAsync();
        _isInitialized = true;
    }

    public async Task<IndexResult> IndexDocumentAsync(
        string filePath,
        IProgress<IndexProgress>? progress = null)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service wurde nicht initialisiert. Rufen Sie InitializeAsync() auf.");

        var result = new IndexResult
        {
            Filepath = filePath,
            StartTime = DateTime.Now
        };

        try
        {
            // 1. Parse Dokument
            progress?.Report(new IndexProgress { Stage = "Parsing", Percentage = 0 });
            var parsedDoc = await _documentParser.ParseDocumentAsync(filePath);
            result.TotalChunks = parsedDoc.Chunks.Count;

            // 2. Erstelle Embeddings
            progress?.Report(new IndexProgress { Stage = "Generating Embeddings", Percentage = 30 });
            var chunks = new List<DocumentChunk>();

            for (int i = 0; i < parsedDoc.Chunks.Count; i++)
            {
                var chunk = parsedDoc.Chunks[i];
                var embedding = await _embeddingService.GetEmbeddingAsync(chunk.Content);

                chunks.Add(new DocumentChunk
                {
                    Content = chunk.Content,
                    Embedding = embedding,
                    PageNumber = chunk.PageNumber,
                    CharStart = chunk.CharStart,
                    CharEnd = chunk.CharEnd
                });

                var percentage = 30 + (int)((i / (float)parsedDoc.Chunks.Count) * 40);
                progress?.Report(new IndexProgress
                {
                    Stage = "Generating Embeddings",
                    Percentage = percentage,
                    CurrentChunk = i + 1,
                    TotalChunks = parsedDoc.Chunks.Count
                });
            }

            // 3. Indexiere in Vektordatenbank
            progress?.Report(new IndexProgress { Stage = "Indexing", Percentage = 70 });
            var documentId = Guid.NewGuid().ToString();

            await _vectorSearch.IndexDocumentAsync(
                documentId,
                parsedDoc.Filename,
                parsedDoc.Filepath,
                parsedDoc.FileType,
                chunks,
                parsedDoc.Metadata
            );

            result.DocumentId = documentId;
            result.Success = true;
            result.EndTime = DateTime.Now;

            progress?.Report(new IndexProgress { Stage = "Completed", Percentage = 100 });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            throw;
        }

        return result;
    }

    public async Task<List<IndexResult>> IndexMultipleDocumentsAsync(
        IEnumerable<string> filePaths,
        IProgress<BatchIndexProgress>? progress = null)
    {
        var results = new List<IndexResult>();
        var filePathList = filePaths.ToList();
        var totalFiles = filePathList.Count;

        for (int i = 0; i < totalFiles; i++)
        {
            var filePath = filePathList[i];

            progress?.Report(new BatchIndexProgress
            {
                CurrentFile = i + 1,
                TotalFiles = totalFiles,
                CurrentFilename = Path.GetFileName(filePath),
                OverallPercentage = (int)((i / (float)totalFiles) * 100)
            });

            try
            {
                var fileProgress = new Progress<IndexProgress>(p =>
                {
                    progress?.Report(new BatchIndexProgress
                    {
                        CurrentFile = i + 1,
                        TotalFiles = totalFiles,
                        CurrentFilename = Path.GetFileName(filePath),
                        CurrentFileProgress = p,
                        OverallPercentage = (int)(((i + (p.Percentage / 100f)) / totalFiles) * 100)
                    });
                });

                var result = await IndexDocumentAsync(filePath, fileProgress);
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(new IndexResult
                {
                    Filepath = filePath,
                    Success = false,
                    ErrorMessage = ex.Message,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now
                });
            }
        }

        progress?.Report(new BatchIndexProgress
        {
            CurrentFile = totalFiles,
            TotalFiles = totalFiles,
            OverallPercentage = 100,
            IsCompleted = true
        });

        return results;
    }

    public async Task<List<HybridSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        SearchMode mode = SearchMode.Hybrid,
        string? documentIdFilter = null,
        int? pageNumberFilter = null)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service wurde nicht initialisiert.");

        return await _hybridSearch.SearchAsync(query, limit, mode, documentIdFilter, pageNumberFilter);
    }

    public async Task<List<DocumentInfo>> GetAllDocumentsAsync()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service wurde nicht initialisiert.");

        return await _vectorSearch.GetAllDocumentsAsync();
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service wurde nicht initialisiert.");

        return await _vectorSearch.DeleteDocumentAsync(documentId);
    }

    public async Task<List<string>> SuggestCorrectionsAsync(string query, int maxSuggestions = 5)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service wurde nicht initialisiert.");

        return await _hybridSearch.SuggestCorrections(query, maxSuggestions);
    }

    public void Dispose()
    {
        _vectorSearch?.Dispose();
        _embeddingService?.Dispose();
    }
}

public class IndexResult
{
    public string Filepath { get; set; } = string.Empty;
    public string? DocumentId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalChunks { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

public class IndexProgress
{
    public string Stage { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public int CurrentChunk { get; set; }
    public int TotalChunks { get; set; }
}

public class BatchIndexProgress
{
    public int CurrentFile { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFilename { get; set; } = string.Empty;
    public IndexProgress? CurrentFileProgress { get; set; }
    public int OverallPercentage { get; set; }
    public bool IsCompleted { get; set; }
}