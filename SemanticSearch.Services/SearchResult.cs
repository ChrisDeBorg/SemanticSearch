namespace SemanticSearch.Services;

public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public float Similarity { get; set; }

    public string ChunkId { get; set; } = string.Empty;
    public float Distance { get; set; }
    public float Score { get; set; }

}