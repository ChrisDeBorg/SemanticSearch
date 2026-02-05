using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System.Reflection;

namespace SemanticSearch.Services;

public class EmbeddingService : IDisposable
{
    private readonly InferenceSession _session;
    private readonly Tokenizer _tokenizer;
    private readonly int _maxSequenceLength;
    private readonly int _embeddingDimension;
    private const int MaxTokens = 256;

    public int EmbeddingDimension => _embeddingDimension;

    public EmbeddingService(string modelPath, int maxSequenceLength = 512, int embeddingDimension = 384)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"ONNX-Modell nicht gefunden: {modelPath}\n" +
                $"Bitte laden Sie ein Modell herunter, z.B. all-MiniLM-L6-v2 von Hugging Face");
        }

        _maxSequenceLength = maxSequenceLength;
        _embeddingDimension = embeddingDimension;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _session = new InferenceSession(modelPath, sessionOptions);
        _tokenizer = LoadTokenizer();
    }

    private Tokenizer LoadTokenizer()
    {
        // Versuche vocab.txt aus verschiedenen Quellen zu laden

        // 1. Versuch: Aus dem gleichen Verzeichnis wie das Modell
        var modelDir = Path.GetDirectoryName(typeof(EmbeddingService).Assembly.Location);
        var vocabPath = Path.Combine(modelDir!, "models", "vocab.txt");

        // 2. Versuch: Als Embedded Resource
        if (!File.Exists(vocabPath))
        {
            vocabPath = ExtractEmbeddedVocab();
        }

        // 3. Versuch: Standard BERT vocab aus Resources
        if (!File.Exists(vocabPath) || !IsValidVocabFile(vocabPath))
        {
            vocabPath = CreateMinimalVocabFile();
        }

        // Lade Tokenizer mit Version 2.0 API
        try
        {
            using var vocabStream = File.OpenRead(vocabPath);

            // BertTokenizer.Create in Version 2.0 erwartet:
            // - vocabStream
            // - Optional: lowerCase (default true)
            // - Optional: unknownToken (default "[UNK]")
            // Die Special Tokens müssen im vocab.txt vorhanden sein!

            var tokenizer = BertTokenizer.Create(
                vocabStream,
                new BertOptions
                {
                    LowerCaseBeforeTokenization = true,
                    UnknownToken = "[UNK]",
                    SeparatorToken = "[SEP]",
                    ClassificationToken = "[CLS]",
                    PaddingToken = "[PAD]",
                    MaskingToken = "[MASK]"
                }
            );

            System.Diagnostics.Debug.WriteLine("✅ BertTokenizer erfolgreich geladen");
            return tokenizer;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Fehler beim Laden des Tokenizers: {ex.Message}");

            // Letzter Fallback: Erstelle ein minimales funktionierendes Vokabular
            var fallbackVocabPath = CreateWorkingVocabFile();
            using var stream = File.OpenRead(fallbackVocabPath);

            return BertTokenizer.Create(
                stream,
                new BertOptions
                {
                    LowerCaseBeforeTokenization = true,
                    UnknownToken = "[UNK]",
                    SeparatorToken = "[SEP]",
                    ClassificationToken = "[CLS]",
                    PaddingToken = "[PAD]",
                    MaskingToken = "[MASK]"
                }
            );
        }
    }

    private bool IsValidVocabFile(string vocabPath)
    {
        try
        {
            var lines = File.ReadLines(vocabPath).Take(10).ToList();

            // Prüfe ob die Datei mindestens ein paar Zeilen hat
            if (lines.Count < 5)
                return false;

            // Prüfe ob es wirklich Text ist und nicht HTML
            var firstLine = lines[0];
            return !firstLine.Contains("<html>", StringComparison.OrdinalIgnoreCase) &&
                   !firstLine.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string CreateWorkingVocabFile()
    {
        // Erstelle ein minimales BERT-Vokabular das garantiert funktioniert
        var vocab = new List<string>
        {
            // Special Tokens - MÜSSEN am Anfang stehen in dieser Reihenfolge
            "[PAD]",      // ID 0
            "[UNK]",      // ID 1 (oder 100, je nach Modell)
            "[CLS]",      // ID 2 (oder 101)
            "[SEP]",      // ID 3 (oder 102)
            "[MASK]",     // ID 4 (oder 103)
        };

        // Füge Zahlen hinzu
        for (int i = 0; i < 100; i++)
        {
            vocab.Add(i.ToString());
        }

        // Einzelne Zeichen
        for (char c = 'a'; c <= 'z'; c++)
        {
            vocab.Add(c.ToString());
        }
        for (char c = 'A'; c <= 'Z'; c++)
        {
            vocab.Add(c.ToString());
        }

        // Deutsche und englische Häufigkeiten
        var commonWords = new[]
        {
            // Englisch
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "should",
            "can", "could", "may", "might", "must", "shall", "of", "in", "to", "for",
            "on", "with", "at", "by", "from", "up", "about", "into", "through", "during",
            "i", "you", "he", "she", "it", "we", "they", "this", "that", "these", "those",
            "and", "or", "but", "if", "because", "as", "what", "which", "who", "whom",
            "when", "where", "why", "how", "all", "any", "some", "no", "not", "other",
            
            // Deutsch
            "der", "die", "das", "den", "dem", "des", "ein", "eine", "eines", "einem", "einen",
            "ist", "sind", "war", "waren", "sein", "hat", "haben", "hatte", "hatten", "wird",
            "werden", "wurde", "wurden", "kann", "können", "konnte", "konnten", "könnte",
            "muss", "müssen", "musste", "mussten", "soll", "sollen", "sollte", "sollten",
            "ich", "du", "er", "sie", "es", "wir", "ihr", "und", "oder", "aber", "wenn",
            "nicht", "auch", "nur", "noch", "schon", "mehr", "sehr", "zu", "auf", "für",
            "mit", "von", "bei", "nach", "über", "unter", "durch", "gegen", "aus", "an",
            
            // WordPiece Tokens (mit ##)
            "##en", "##er", "##es", "##e", "##s", "##n", "##t", "##ing", "##ed", "##ly"
        };

        vocab.AddRange(commonWords);

        var tempPath = Path.Combine(Path.GetTempPath(), "minimal_vocab.txt");
        File.WriteAllLines(tempPath, vocab);

        System.Diagnostics.Debug.WriteLine($"⚠️ Verwende minimales Fallback-Vokabular: {tempPath}");
        System.Diagnostics.Debug.WriteLine("   Für bessere Ergebnisse laden Sie vocab.txt von Hugging Face herunter!");

        return tempPath;
    }

    private string CreateMinimalVocabFile()
    {
        // Erstelle ein Standard-BERT Vokabular
        return CreateWorkingVocabFile();
    }

    
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        return await Task.Run(() => GetEmbedding(text));
    }

    public float[] GetEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[_embeddingDimension];
        }

        // Tokenisiere den Text
        var encoded = _tokenizer.EncodeToIds(text);

        // Extrahiere Arrays und truncate/pad auf max length
        var inputIds = encoded.Take(_maxSequenceLength).ToList();
        var attentionMask = Enumerable.Repeat(1, inputIds.Count).ToList();
        var tokenTypeIds = Enumerable.Repeat(0, inputIds.Count).ToList();

        // Padding falls zu kurz
        while (inputIds.Count < _maxSequenceLength)
        {
            inputIds.Add(0); // [PAD] token ID
            attentionMask.Add(0);
            tokenTypeIds.Add(0);
        }

        // Erstelle Input-Tensoren
        var inputIdsTensor = new DenseTensor<long>(new[] { 1, _maxSequenceLength });
        var attentionMaskTensor = new DenseTensor<long>(new[] { 1, _maxSequenceLength });
        var tokenTypeIdsTensor = new DenseTensor<long>(new[] { 1, _maxSequenceLength });

        for (int i = 0; i < _maxSequenceLength; i++)
        {
            inputIdsTensor[0, i] = inputIds[i];
            attentionMaskTensor[0, i] = attentionMask[i];
            tokenTypeIdsTensor[0, i] = tokenTypeIds[i];
        }

        // Führe Inferenz aus
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        // Mean pooling über alle Tokens
        return MeanPooling(output, attentionMask.Select(x => (long)x).ToArray());
    }


    private float[] MeanPooling(Tensor<float> embeddings, long[] attentionMask)
    {
        var batchSize = embeddings.Dimensions[0];
        var sequenceLength = embeddings.Dimensions[1];
        var hiddenSize = embeddings.Dimensions[2];

        var pooled = new float[hiddenSize];
        var maskSum = 0f;

        for (int i = 0; i < sequenceLength; i++)
        {
            if (attentionMask[i] == 1)
            {
                for (int j = 0; j < hiddenSize; j++)
                {
                    pooled[j] += embeddings[0, i, j];
                }
                maskSum += 1.0f;
            }
        }

        // Normalisiere
        for (int i = 0; i < hiddenSize; i++)
        {
            pooled[i] /= maskSum;
        }

        // L2-Normalisierung
        var norm = 0f;
        for (int i = 0; i < hiddenSize; i++)
        {
            norm += pooled[i] * pooled[i];
        }
        norm = (float)Math.Sqrt(norm);

        for (int i = 0; i < hiddenSize; i++)
        {
            pooled[i] /= norm;
        }

        return pooled;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
    

    private string ExtractEmbeddedVocab()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith("vocab.txt"));

        if (resourceName == null)
            return string.Empty;

        var targetPath = Path.Combine(Path.GetTempPath(), "semantic_search_vocab.txt");

        if (!File.Exists(targetPath))
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var fileStream = File.Create(targetPath);
                stream.CopyTo(fileStream);
            }
        }

        return targetPath;
    }


    //2nd implementation
    public float[] GenerateEmbedding(string text)
    {
        // Text tokenisieren
        // Simple Tokenisierung
        string normalizedText;
        int textLength;
        var tokenIds = _tokenizer.EncodeToIds(text, _maxSequenceLength, out normalizedText, out textLength);

        var tokens = tokenIds
            .Take(MaxTokens)
            .Select(id => (long)id)
            .ToArray();

        // Auf MaxTokens begrenzen
        if (tokens.Length > MaxTokens)
        {
            tokens = tokens.Take(MaxTokens).ToArray();
        }

        // Padding auf MaxTokens
        var inputIds = new long[MaxTokens];
        var attentionMask = new long[MaxTokens];

        for (int i = 0; i < MaxTokens; i++)
        {
            if (i < tokens.Length)
            {
                inputIds[i] = tokens[i];
                attentionMask[i] = 1;
            }
            else
            {
                inputIds[i] = 0;
                attentionMask[i] = 0;
            }
        }

        // Tensoren erstellen
        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, MaxTokens });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, MaxTokens });

        // Inferenz durchführen
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        using var results = _session.Run(inputs);
        var embeddings = results.First().AsEnumerable<float>().ToArray();

        // Normalisieren
        return Normalize(embeddings);
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(x => x * x));
        return vector.Select(x => (float)(x / norm)).ToArray();
    }

}

