using Microsoft.Extensions.VectorData;
using System.Numerics.Tensors;

namespace SemanticSearch.Services;

public class DocumentSearchService
{
    private readonly List<DocumentChunk> _documents = new();
    private readonly EmbeddingService _embeddingService;
    private readonly DocumentIndexer _indexer;

    public DocumentSearchService(EmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
        _indexer = new DocumentIndexer(embeddingService);
    }

    public async Task IndexDocumentAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        List<DocumentChunk> chunks = extension switch
        {
            ".pdf" => await _indexer.IndexPdfAsync(filePath),
            ".epub" => await _indexer.IndexEpubAsync(filePath),
            _ => throw new NotSupportedException($"File type {extension} not supported")
        };

        // Alte Chunks des gleichen Dokuments entfernen
        var fileName = Path.GetFileName(filePath);
        _documents.RemoveAll(d => d.FileName == fileName);

        _documents.AddRange(chunks);
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int topK = 5)
    {
        if (_documents.Count == 0)
            return new List<SearchResult>();

        var queryEmbedding = await Task.Run(() => _embeddingService.GenerateEmbedding(query));

        var results = _documents
            .Select(doc => new SearchResult
            {
                Id = doc.Id,
                Content = doc.Content,
                FileName = doc.FileName,
                FilePath = doc.SourceFile,
                ChunkIndex = doc.ChunkIndex,
                Similarity = TensorPrimitives.CosineSimilarity(
                    queryEmbedding.AsSpan(),
                    doc.Embedding.Span
                )
            })
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();

        return results;
    }

    public int GetDocumentCount() => _documents.Select(d => d.FileName).Distinct().Count();

    public int GetChunkCount() => _documents.Count;

    // Persistierung
    public async Task SaveIndexAsync(string indexPath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_documents);
        await File.WriteAllTextAsync(indexPath, json);
    }

    public async Task LoadIndexAsync(string indexPath)
    {
        if (!File.Exists(indexPath))
            return;

        var json = await File.ReadAllTextAsync(indexPath);
        var docs = System.Text.Json.JsonSerializer.Deserialize<List<DocumentChunk>>(json);

        if (docs != null)
        {
            _documents.Clear();
            _documents.AddRange(docs);
        }
    }
}