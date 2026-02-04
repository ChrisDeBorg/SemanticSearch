using Microsoft.Data.Sqlite;
using System.Text;
using Microsoft.Extensions.VectorData;

namespace SemanticSearch.Services;

public class VectorSearchService : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;
    private readonly int _embeddingDimension;
    private bool _isInitialized;

    public VectorSearchService(string dbPath, int embeddingDimension = 384)
    {
        _dbPath = dbPath;
        _embeddingDimension = embeddingDimension;

        // Stelle sicher, dass das Verzeichnis existiert
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            // Lade sqlite-vec Extension
            var extensionPath = GetVecExtensionPath();
            // ✅ Debug-Ausgabe
            //Console.WriteLine($"[DEBUG] Extension-Pfad: {extensionPath}");
            //Console.WriteLine($"[DEBUG] Datei existiert: {File.Exists(extensionPath)}");
            //Console.WriteLine($"[DEBUG] Dateigröße: {new FileInfo(extensionPath).Length} bytes");
            //Console.WriteLine($"[DEBUG] BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            //Console.WriteLine($"[DEBUG] Working Directory: {Environment.CurrentDirectory}");



            if (!File.Exists(extensionPath))
            {
                throw new FileNotFoundException(
                    $"sqlite-vec Extension nicht gefunden: {extensionPath}\n" +
                    $"Bitte laden Sie die passende Version von https://github.com/asg017/sqlite-vec/releases herunter.");
            }

            _connection.LoadExtension(extensionPath);

            // Erstelle Datenbank-Schema
            await CreateSchemaAsync();

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Fehler beim Initialisieren der Vektorsuche", ex);
        }
    }

    private string GetVecExtensionPath()
    {
        string dllName;

        if (OperatingSystem.IsWindows())
            dllName = "vec0.dll";
        else if (OperatingSystem.IsLinux())
            dllName = "vec0.so";
        else if (OperatingSystem.IsMacOS())
            dllName = "vec0.dylib";
        else
            throw new PlatformNotSupportedException("Plattform nicht unterstützt");

        // ✅ Absoluter Pfad zum BaseDirectory
        var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dllName);


        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"sqlite-vec Extension nicht gefunden: {fullPath}\n" +
                $"Bitte laden Sie die passende Version von https://github.com/asg017/sqlite-vec/releases herunter.");
        }

        return fullPath;
    }

    private async Task CreateSchemaAsync()
    {
        var commands = new[]
        {
        @"CREATE TABLE IF NOT EXISTS documents (
            id TEXT PRIMARY KEY,
            filename TEXT NOT NULL,
            filepath TEXT NOT NULL,
            file_type TEXT NOT NULL,
            total_chunks INTEGER DEFAULT 0,
            indexed_date DATETIME DEFAULT CURRENT_TIMESTAMP,
            file_size_bytes INTEGER,
            metadata TEXT
        )",

        @"CREATE TABLE IF NOT EXISTS document_chunks (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            document_id TEXT NOT NULL,
            chunk_index INTEGER NOT NULL,
            content TEXT NOT NULL,
            page_number INTEGER,
            char_start INTEGER,
            char_end INTEGER,
            FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE
        )",

        $@"CREATE VIRTUAL TABLE IF NOT EXISTS vec_chunks USING vec0(
            chunk_id INTEGER PRIMARY KEY,
            embedding FLOAT[{_embeddingDimension}]
        )",

        "CREATE INDEX IF NOT EXISTS idx_chunks_document ON document_chunks(document_id)",
        "CREATE INDEX IF NOT EXISTS idx_chunks_page ON document_chunks(page_number)",
        "CREATE INDEX IF NOT EXISTS idx_documents_filename ON documents(filename)"
    };

        foreach (var sql in commands)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<string> IndexDocumentAsync(
        string documentId,
        string filename,
        string filepath,
        string fileType,
        List<DocumentChunk> chunks,
        Dictionary<string, string>? metadata = null)
    {
        using var transaction = _connection.BeginTransaction();

        try
        {
            await DeleteDocumentAsync(documentId, transaction);

            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                INSERT INTO documents (id, filename, filepath, file_type, total_chunks, file_size_bytes, metadata)
                VALUES (@id, @filename, @filepath, @fileType, @totalChunks, @fileSize, @metadata)";

                cmd.Parameters.AddWithValue("@id", documentId);
                cmd.Parameters.AddWithValue("@filename", filename);
                cmd.Parameters.AddWithValue("@filepath", filepath);
                cmd.Parameters.AddWithValue("@fileType", fileType);
                cmd.Parameters.AddWithValue("@totalChunks", chunks.Count);
                cmd.Parameters.AddWithValue("@fileSize", new FileInfo(filepath).Length);
                cmd.Parameters.AddWithValue("@metadata",
                    metadata != null ? System.Text.Json.JsonSerializer.Serialize(metadata) : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }

            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                long chunkId;

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                    INSERT INTO document_chunks (document_id, chunk_index, content, page_number, char_start, char_end)
                    VALUES (@docId, @index, @content, @page, @charStart, @charEnd)
                    RETURNING id";

                    cmd.Parameters.AddWithValue("@docId", documentId);
                    cmd.Parameters.AddWithValue("@index", index);
                    cmd.Parameters.AddWithValue("@content", chunk.Content);
                    cmd.Parameters.AddWithValue("@page", chunk.PageNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@charStart", chunk.CharStart ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@charEnd", chunk.CharEnd ?? (object)DBNull.Value);

                    chunkId = (long)(await cmd.ExecuteScalarAsync())!;
                }

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                    INSERT INTO vec_chunks (chunk_id, embedding)
                    VALUES (@chunkId, @embedding)";

                    cmd.Parameters.AddWithValue("@chunkId", chunkId);
                    cmd.Parameters.AddWithValue("@embedding", SerializeEmbedding(chunk.Embedding.ToArray()));

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            return documentId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int limit = 10,
        string? documentIdFilter = null,
        int? pageNumberFilter = null)
    {
        var sql = new StringBuilder(@"
        SELECT 
            dc.id,
            dc.document_id,
            dc.content,
            dc.chunk_index,
            dc.page_number,
            d.filename,
            d.filepath,
            d.file_type,
            vec.distance
        FROM vec_chunks vec
        INNER JOIN document_chunks dc ON vec.chunk_id = dc.id
        INNER JOIN documents d ON dc.document_id = d.id
        WHERE vec.embedding MATCH @embedding
            AND vec.k = @limit");

        if (!string.IsNullOrEmpty(documentIdFilter))
            sql.Append(" AND dc.document_id = @docId");

        if (pageNumberFilter.HasValue)
            sql.Append(" AND dc.page_number = @page");

        sql.Append(" ORDER BY vec.distance ASC");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("@embedding", SerializeEmbedding(queryEmbedding));
        cmd.Parameters.AddWithValue("@limit", limit);

        if (!string.IsNullOrEmpty(documentIdFilter))
            cmd.Parameters.AddWithValue("@docId", documentIdFilter);

        if (pageNumberFilter.HasValue)
            cmd.Parameters.AddWithValue("@page", pageNumberFilter.Value);

        var results = new List<SearchResult>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new SearchResult
            {
                ChunkId = reader.GetString(0),
                DocumentId = reader.GetString(1),
                Content = reader.GetString(2),
                ChunkIndex = reader.GetInt32(3),
                PageNumber = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                FileName = reader.GetString(5),
                FilePath = reader.GetString(6),
                FileType = reader.GetString(7),
                Distance = reader.GetFloat(8),
                Score = DistanceToSimilarity(reader.GetFloat(8))
            });
        }

        return results;
    }

    public async Task<List<DocumentInfo>> GetAllDocumentsAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
        SELECT id, filename, filepath, file_type, total_chunks, indexed_date, file_size_bytes
        FROM documents
        ORDER BY indexed_date DESC";

        var documents = new List<DocumentInfo>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            documents.Add(new DocumentInfo
            {
                Id = reader.GetString(0),
                Filename = reader.GetString(1),
                Filepath = reader.GetString(2),
                FileType = reader.GetString(3),
                TotalChunks = reader.GetInt32(4),
                IndexedDate = reader.GetDateTime(5),
                FileSizeBytes = reader.GetInt64(6)
            });
        }

        return documents;
    }

    public async Task<bool> DeleteDocumentAsync(string documentId, SqliteTransaction? transaction = null)
    {
        var ownTransaction = transaction == null;
        transaction ??= _connection.BeginTransaction();

        try
        {
            var chunkIds = new List<long>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT id FROM document_chunks WHERE document_id = @docId";
                cmd.Parameters.AddWithValue("@docId", documentId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    chunkIds.Add(reader.GetInt64(0));
                }
            }

            foreach (var chunkId in chunkIds)
            {
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM vec_chunks WHERE chunk_id = @chunkId";
                cmd.Parameters.AddWithValue("@chunkId", chunkId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM document_chunks WHERE document_id = @docId";
                cmd.Parameters.AddWithValue("@docId", documentId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM documents WHERE id = @docId";
                cmd.Parameters.AddWithValue("@docId", documentId);
                await cmd.ExecuteNonQueryAsync();
            }

            if (ownTransaction)
                await transaction.CommitAsync();

            return true;
        }
        catch
        {
            if (ownTransaction)
                await transaction.RollbackAsync();
            throw;
        }
    }

    private byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private float DistanceToSimilarity(float distance)
    {
        return 1.0f / (1.0f + distance);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

//public class SearchResult
//{
//    public long ChunkId { get; set; }
//    public long DocumentId { get; set; }
//    public string Content { get; set; } = string.Empty;
//    public int ChunkIndex { get; set; }
//    public int? PageNumber { get; set; }
//    public string FileName { get; set; } = string.Empty;
//    public string FilePath { get; set; } = string.Empty;
//    public string FileType { get; set; } = string.Empty;
//    public float Distance { get; set; }
//    public float Score { get; set; }
//}

public class DocumentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Filepath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public DateTime IndexedDate { get; set; }
    public long FileSizeBytes { get; set; }
}

