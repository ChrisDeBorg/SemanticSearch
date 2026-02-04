
using System.Numerics.Tensors;

namespace SemanticSearch.Services;

public class DocumentChunk
{
    public string Content { get; set; } = string.Empty;
    //public float[] Embedding { get; set; } = Array.Empty<float>();
    public int? PageNumber { get; set; }
    public int? CharStart { get; set; }
    public int? CharEnd { get; set; }



    //[VectorStoreRecordKey]
    public string Id { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    //[VectorStoreRecordData]
    //public string Text { get; set; } = string.Empty;

    //[VectorStoreRecordData]
    public string SourceFile { get; set; } = string.Empty;

    //[VectorStoreRecordData]
    public string FileName { get; set; } = string.Empty;

    //[VectorStoreRecordData]
    public int ChunkIndex { get; set; }

    //[VectorStoreRecordData]
    public DateTime IndexedAt { get; set; }

    //[VectorStoreRecordVector(384)] // all-MiniLM-L6-v2 hat 384 Dimensionen
    public ReadOnlyMemory<float> Embedding { get; set; } = Array.Empty<float>();
}

