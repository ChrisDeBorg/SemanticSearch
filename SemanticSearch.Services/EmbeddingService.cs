using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Tokenization;

namespace SemanticSearch.Services;

public class EmbeddingService : IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly Microsoft.ML.Tokenizers.Tokenizer _mstokenizer;
    private readonly int _maxSequenceLength;
    private readonly int _embeddingDimension;
    private const int MaxTokens = 256;
    private const int VocabSize = 30522; // BERT vocab size

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
        _tokenizer = new BertTokenizer();
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
        var tokens = _tokenizer.Tokenize(text, _maxSequenceLength);

        // Erstelle Input-Tensoren
        var inputIds = new DenseTensor<long>(new[] { 1, tokens.InputIds.Length });
        var attentionMask = new DenseTensor<long>(new[] { 1, tokens.AttentionMask.Length });
        var tokenTypeIds = new DenseTensor<long>(new[] { 1, tokens.TokenTypeIds.Length });

        for (int i = 0; i < tokens.InputIds.Length; i++)
        {
            inputIds[0, i] = tokens.InputIds[i];
            attentionMask[0, i] = tokens.AttentionMask[i];
            tokenTypeIds[0, i] = tokens.TokenTypeIds[i];
        }

        // Führe Inferenz aus
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        // Mean pooling über alle Tokens
        return MeanPooling(output, tokens.AttentionMask);
    }

    public async Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts)
    {
        return await Task.Run(() => texts.Select(GetEmbedding).ToList());
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


    //2nd implementation
    public float[] GenerateEmbedding(string text)
    {
        // Text tokenisieren
        // Simple Tokenisierung
        string normalizedText;
        int textLength;
        var tokenIds = _mstokenizer.EncodeToIds(text, _maxSequenceLength, out normalizedText, out textLength);

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

// Einfacher BERT Tokenizer
public class BertTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly int _clsTokenId = 101;  // [CLS]
    private readonly int _sepTokenId = 102;  // [SEP]
    private readonly int _padTokenId = 0;    // [PAD]
    private readonly int _unkTokenId = 100;  // [UNK]

    public BertTokenizer()
    {
        // Vereinfachtes Vokabular - in Produktion sollte das echte BERT-Vokabular geladen werden
        _vocab = LoadVocabulary();
    }

    public TokenizedResult Tokenize(string text, int maxLength)
    {
        // Normalisiere Text
        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");

        // Wortstücke-Tokenisierung (vereinfacht)
        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var tokenIds = new List<long> { _clsTokenId };

        foreach (var word in words)
        {
            if (tokenIds.Count >= maxLength - 1) break;

            // Versuche das Wort im Vokabular zu finden
            if (_vocab.TryGetValue(word, out var tokenId))
            {
                tokenIds.Add(tokenId);
            }
            else
            {
                // Zerlege in Subwörter (WordPiece-Tokenisierung)
                var subTokens = TokenizeWord(word);
                foreach (var subToken in subTokens)
                {
                    if (tokenIds.Count >= maxLength - 1) break;
                    tokenIds.Add(subToken);
                }
            }
        }

        tokenIds.Add(_sepTokenId);

        // Padding
        var attentionMask = new long[maxLength];
        var tokenTypeIds = new long[maxLength];

        for (int i = 0; i < tokenIds.Count; i++)
        {
            attentionMask[i] = 1;
        }

        while (tokenIds.Count < maxLength)
        {
            tokenIds.Add(_padTokenId);
        }

        return new TokenizedResult
        {
            InputIds = tokenIds.ToArray(),
            AttentionMask = attentionMask,
            TokenTypeIds = tokenTypeIds
        };
    }

    private List<long> TokenizeWord(string word)
    {
        var tokens = new List<long>();
        var start = 0;

        while (start < word.Length)
        {
            var end = word.Length;
            long? foundTokenId = null;

            while (start < end)
            {
                var substr = word.Substring(start, end - start);
                if (start > 0)
                {
                    substr = "##" + substr;
                }

                if (_vocab.TryGetValue(substr, out var tokenId))
                {
                    foundTokenId = tokenId;
                    break;
                }
                end--;
            }

            if (foundTokenId.HasValue)
            {
                tokens.Add(foundTokenId.Value);
                start = end;
            }
            else
            {
                tokens.Add(_unkTokenId);
                start++;
            }
        }

        return tokens;
    }

    private Dictionary<string, int> LoadVocabulary()
    {
        // In einer echten Implementierung würde hier das vocab.txt des BERT-Modells geladen
        // Für dieses Beispiel erstellen wir ein minimales Vokabular
        var vocab = new Dictionary<string, int>
        {
            ["[PAD]"] = 0,
            ["[UNK]"] = 100,
            ["[CLS]"] = 101,
            ["[SEP]"] = 102,
            ["[MASK]"] = 103
        };

        // Füge häufige Wörter hinzu (vereinfacht)
        var commonWords = new[] {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "should",
            "can", "could", "may", "might", "must", "shall",
            "i", "you", "he", "she", "it", "we", "they",
            "this", "that", "these", "those",
            "and", "or", "but", "if", "because", "as", "what", "which", "who",
            "when", "where", "why", "how",
            "all", "any", "some", "no", "not"
        };

        int currentId = 200;
        foreach (var word in commonWords)
        {
            vocab[word] = currentId++;
        }

        return vocab;
    }


    // Dispose-Methode, falls benötigt
    public void Dispose()
    {
        // Keine Ressourcen zu bereinigen in diesem einfachen Tokenizer
    }

}

public class TokenizedResult
{
    public long[] InputIds { get; set; } = Array.Empty<long>();
    public long[] AttentionMask { get; set; } = Array.Empty<long>();
    public long[] TokenTypeIds { get; set; } = Array.Empty<long>();
}



