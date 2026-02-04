using System.Text;
using UglyToad.PdfPig;
using VersOne.Epub;

namespace SemanticSearch.Services;

public class DocumentIndexer
{
    private readonly EmbeddingService _embeddingService;
    private const int ChunkSize = 500; // Zeichen pro Chunk
    private const int ChunkOverlap = 100; // Überlappung zwischen Chunks

    public DocumentIndexer(EmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public async Task<List<DocumentChunk>> IndexPdfAsync(string filePath)
    {
        var chunks = new List<DocumentChunk>();

        using var document = PdfDocument.Open(filePath);
        var fullText = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            fullText.AppendLine(page.Text);
        }

        var textChunks = SplitIntoChunks(fullText.ToString());
        var fileName = Path.GetFileName(filePath);

        for (int i = 0; i < textChunks.Count; i++)
        {
            var embedding = await Task.Run(() => _embeddingService.GenerateEmbedding(textChunks[i]));

            chunks.Add(new DocumentChunk
            {
                Id = $"{fileName}_{i}",
                Content = textChunks[i],
                SourceFile = filePath,
                FileName = fileName,
                ChunkIndex = i,
                IndexedAt = DateTime.UtcNow,
                Embedding = embedding
            });
        }

        return chunks;
    }

    public async Task<List<DocumentChunk>> IndexEpubAsync(string filePath)
    {
        var chunks = new List<DocumentChunk>();

        var book = await EpubReader.ReadBookAsync(filePath);
        var fullText = new StringBuilder();

        foreach (var item in book.ReadingOrder)
        {
            var content = item.Content;
            // HTML-Tags entfernen (einfache Variante)
            var plainText = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", " ");
            fullText.AppendLine(plainText);
        }

        var textChunks = SplitIntoChunks(fullText.ToString());
        var fileName = Path.GetFileName(filePath);

        for (int i = 0; i < textChunks.Count; i++)
        {
            var embedding = await Task.Run(() => _embeddingService.GenerateEmbedding(textChunks[i]));

            chunks.Add(new DocumentChunk
            {
                Id = $"{fileName}_{i}",
                Content = textChunks[i],
                SourceFile = filePath,
                FileName = fileName,
                ChunkIndex = i,
                IndexedAt = DateTime.UtcNow,
                Embedding = embedding
            });
        }

        return chunks;
    }

    private List<string> SplitIntoChunks(string text)
    {
        var chunks = new List<string>();
        text = text.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + ChunkSize, text.Length);

            // Bei Leerzeichen oder Satzende trennen
            if (end < text.Length)
            {
                var lastSpace = text.LastIndexOfAny([' ', '.', '!', '?', '\n'], end, Math.Min(100, end - start));
                if (lastSpace > start)
                {
                    end = lastSpace + 1;
                }
            }

            var chunk = text.Substring(start, end - start).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            start = end - ChunkOverlap;
            if (start < 0) start = end;
        }

        return chunks;
    }
}