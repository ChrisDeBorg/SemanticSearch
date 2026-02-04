using System.Numerics.Tensors;
using System.Text.Json;

namespace SemanticSearch.Services;

public class InMemoryVectorStore
{
    private readonly List<VectorDocument> _documents = new();
    private readonly object _lock = new();

    public void Add(VectorDocument document)
    {
        lock (_lock)
        {
            _documents.Add(document);
        }
    }

    public void AddRange(IEnumerable<VectorDocument> documents)
    {
        lock (_lock)
        {
            _documents.AddRange(documents);
        }
    }

    public void RemoveByFileName(string fileName)
    {
        lock (_lock)
        {
            _documents.RemoveAll(d => d.FileName == fileName);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _documents.Clear();
        }
    }

    public List<SearchResult> Search(float[] queryEmbedding, int topK = 5, float minSimilarity = 0.0f)
    {
        lock (_lock)
        {
            return _documents
                .Select(doc => new SearchResult
                {
                    Id = doc.Id,
                    Content = doc.Text,
                    FileName = doc.FileName,
                    FilePath = doc.SourceFile,
                    ChunkIndex = doc.ChunkIndex,
                    Similarity = TensorPrimitives.CosineSimilarity(
                        queryEmbedding.AsSpan(),
                        doc.Embedding.AsSpan()
                    )
                })
                .Where(r => r.Similarity >= minSimilarity)
                .OrderByDescending(r => r.Similarity)
                .Take(topK)
                .ToList();
        }
    }

    public int GetDocumentCount()
    {
        lock (_lock)
        {
            return _documents.Select(d => d.FileName).Distinct().Count();
        }
    }

    public int GetChunkCount()
    {
        lock (_lock)
        {
            return _documents.Count;
        }
    }

    public async Task SaveToFileAsync(string path)
    {
        List<VectorDocument> snapshot;
        lock (_lock)
        {
            snapshot = new List<VectorDocument>(_documents);
        }

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        await File.WriteAllTextAsync(path, json);
    }

    public async Task LoadFromFileAsync(string path)
    {
        if (!File.Exists(path))
            return;

        var json = await File.ReadAllTextAsync(path);
        var documents = JsonSerializer.Deserialize<List<VectorDocument>>(json);

        if (documents != null)
        {
            lock (_lock)
            {
                _documents.Clear();
                _documents.AddRange(documents);
            }
        }
    }
}

public class VectorDocument
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public DateTime IndexedAt { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
