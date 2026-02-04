using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using VersOne.Epub;
using System.Text;

namespace SemanticSearch.Services;

public class DocumentParserService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;

    public DocumentParserService(int chunkSize = 500, int chunkOverlap = 50)
    {
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
    }

    public async Task<ParsedDocument> ParseDocumentAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => await ParsePdfAsync(filePath),
            ".epub" => await ParseEpubAsync(filePath),
            ".txt" => await ParseTextAsync(filePath),
            ".md" => await ParseTextAsync(filePath),
            ".html" => await ParseHtmlAsync(filePath),
            _ => throw new NotSupportedException($"Dateiformat {extension} wird nicht unterstützt")
        };
    }

    private async Task<ParsedDocument> ParsePdfAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var document = new ParsedDocument
            {
                Filename = Path.GetFileName(filePath),
                Filepath = filePath,
                FileType = "pdf"
            };

            using var pdfDocument = PdfDocument.Open(filePath);
            var allText = new StringBuilder();

            foreach (var page in pdfDocument.GetPages())
            {
                var pageText = page.Text;
                if (string.IsNullOrWhiteSpace(pageText)) continue;

                var chunks = ChunkText(pageText, _chunkSize, _chunkOverlap);

                foreach (var chunk in chunks)
                {
                    document.Chunks.Add(new TextChunk
                    {
                        Content = chunk,
                        PageNumber = page.Number,
                        CharStart = allText.Length,
                        CharEnd = allText.Length + chunk.Length
                    });
                    allText.Append(chunk).Append(" ");
                }
            }

            document.TotalPages = pdfDocument.NumberOfPages;
            document.FullText = allText.ToString();

            return document;
        });
    }

    private async Task<ParsedDocument> ParseEpubAsync(string filePath)
    {
        return await Task.Run(async () =>
        {
            var document = new ParsedDocument
            {
                Filename = Path.GetFileName(filePath),
                Filepath = filePath,
                FileType = "epub"
            };

            var epubBook = await EpubReader.ReadBookAsync(filePath);
            var allText = new StringBuilder();
            var chapterIndex = 0;

            foreach (var chapter in epubBook.ReadingOrder)
            {
                var content = chapter.Content;

                // Entferne HTML-Tags (vereinfacht)
                content = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", " ");
                content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();

                if (string.IsNullOrWhiteSpace(content)) continue;

                var chunks = ChunkText(content, _chunkSize, _chunkOverlap);

                foreach (var chunk in chunks)
                {
                    document.Chunks.Add(new TextChunk
                    {
                        Content = chunk,
                        PageNumber = chapterIndex,
                        CharStart = allText.Length,
                        CharEnd = allText.Length + chunk.Length
                    });
                    allText.Append(chunk).Append(" ");
                }

                chapterIndex++;
            }

            document.TotalPages = chapterIndex;
            document.FullText = allText.ToString();
            document.Metadata["Title"] = epubBook.Title ?? "";
            document.Metadata["Author"] = epubBook.Author ?? "";

            return document;
        });
    }

    private async Task<ParsedDocument> ParseTextAsync(string filePath)
    {
        return await Task.Run(async () =>
        {
            var document = new ParsedDocument
            {
                Filename = Path.GetFileName(filePath),
                Filepath = filePath,
                FileType = Path.GetExtension(filePath).TrimStart('.')
            };

            var text = await File.ReadAllTextAsync(filePath);
            var chunks = ChunkText(text, _chunkSize, _chunkOverlap);

            var position = 0;
            foreach (var chunk in chunks)
            {
                document.Chunks.Add(new TextChunk
                {
                    Content = chunk,
                    CharStart = position,
                    CharEnd = position + chunk.Length
                });
                position += chunk.Length;
            }

            document.FullText = text;
            document.TotalPages = 1;

            return document;
        });
    }

    private async Task<ParsedDocument> ParseHtmlAsync(string filePath)
    {
        return await Task.Run(async () =>
        {
            var document = new ParsedDocument
            {
                Filename = Path.GetFileName(filePath),
                Filepath = filePath,
                FileType = "html"
            };

            var html = await File.ReadAllTextAsync(filePath);

            // Entferne HTML-Tags und Scripts
            html = System.Text.RegularExpressions.Regex.Replace(html, "<script.*?</script>", "",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, "<style.*?</style>", "",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ").Trim();

            var chunks = ChunkText(html, _chunkSize, _chunkOverlap);

            var position = 0;
            foreach (var chunk in chunks)
            {
                document.Chunks.Add(new TextChunk
                {
                    Content = chunk,
                    CharStart = position,
                    CharEnd = position + chunk.Length
                });
                position += chunk.Length;
            }

            document.FullText = html;
            document.TotalPages = 1;

            return document;
        });
    }

    private List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var chunks = new List<string>();

        // Teile an Satzgrenzen
        var sentences = SplitIntoSentences(text);
        var currentChunk = new StringBuilder();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var sentenceLength = sentence.Length;

            if (currentLength + sentenceLength > maxChunkSize && currentLength > 0)
            {
                // Speichere aktuellen Chunk
                chunks.Add(currentChunk.ToString().Trim());

                // Starte neuen Chunk mit Overlap
                var overlapText = GetLastWords(currentChunk.ToString(), overlap);
                currentChunk = new StringBuilder(overlapText);
                currentLength = overlapText.Length;
            }

            currentChunk.Append(sentence).Append(" ");
            currentLength += sentenceLength + 1;
        }

        // Füge letzten Chunk hinzu
        if (currentLength > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Vereinfachte Satztrennung
        var sentenceEndings = new[] { '.', '!', '?', ';' };
        var sentences = new List<string>();
        var currentSentence = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            currentSentence.Append(text[i]);

            if (sentenceEndings.Contains(text[i]))
            {
                // Prüfe auf Abkürzungen (z.B. "Dr.", "etc.")
                if (i < text.Length - 1 && char.IsWhiteSpace(text[i + 1]))
                {
                    if (i < text.Length - 2 && !char.IsUpper(text[i + 2]))
                    {
                        continue; // Wahrscheinlich eine Abkürzung
                    }

                    sentences.Add(currentSentence.ToString().Trim());
                    currentSentence.Clear();
                }
            }
        }

        if (currentSentence.Length > 0)
        {
            sentences.Add(currentSentence.ToString().Trim());
        }

        return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private string GetLastWords(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        // Finde den letzten Satz oder die letzten Wörter
        var lastPart = text.Substring(text.Length - maxLength);
        var firstSpace = lastPart.IndexOf(' ');

        if (firstSpace > 0)
        {
            return lastPart.Substring(firstSpace + 1);
        }

        return lastPart;
    }
}

public class ParsedDocument
{
    public string Filename { get; set; } = string.Empty;
    public string Filepath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public int TotalPages { get; set; }
    public List<TextChunk> Chunks { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class TextChunk
{
    public string Content { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public int CharStart { get; set; }
    public int CharEnd { get; set; }
}