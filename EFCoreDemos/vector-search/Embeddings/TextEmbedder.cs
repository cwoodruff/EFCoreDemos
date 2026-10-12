using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Tokenizers;

namespace vector_search.Embeddings;

/// <summary>
/// Runs all-MiniLM-L6-v2 in-process with ONNX Runtime: BERT word-piece tokenization, the transformer,
/// then mean pooling over the tokens and L2 normalization (what sentence-transformers does).
/// The same embedder produces the stored vectors and the vector for the phrase typed on stage.
/// </summary>
public sealed class TextEmbedder : IDisposable
{
    private const int MaxTokens = 256; // the model's max_seq_length

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;

    private TextEmbedder(InferenceSession session, BertTokenizer tokenizer)
    {
        _session = session;
        _tokenizer = tokenizer;
    }

    public static async Task<TextEmbedder> CreateAsync(TextWriter? log = null)
    {
        await ModelFiles.EnsureDownloadedAsync(log);
        return new TextEmbedder(new InferenceSession(ModelFiles.ModelPath), BertTokenizer.Create(ModelFiles.VocabPath));
    }

    public float[] Embed(string text) => Embed([text])[0];

    /// <summary>Embeds a batch of texts, padding each batch to its longest input.</summary>
    public float[][] Embed(IReadOnlyList<string> texts)
    {
        var ids = texts.Select(Tokenize).ToList();
        var batch = ids.Count;
        var length = ids.Max(i => i.Count);

        var inputIds = new long[batch * length];
        var attentionMask = new long[batch * length];
        var tokenTypeIds = new long[batch * length]; // single-segment input: all zeros

        for (var b = 0; b < batch; b++)
        {
            for (var t = 0; t < ids[b].Count; t++)
            {
                inputIds[b * length + t] = ids[b][t];
                attentionMask[b * length + t] = 1;
            }
        }

        long[] shape = [batch, length];
        using var inputIdsValue = OrtValue.CreateTensorValueFromMemory(inputIds, shape);
        using var attentionMaskValue = OrtValue.CreateTensorValueFromMemory(attentionMask, shape);
        using var tokenTypeIdsValue = OrtValue.CreateTensorValueFromMemory(tokenTypeIds, shape);
        using var runOptions = new RunOptions();

        using var outputs = _session.Run(
            runOptions,
            ["input_ids", "attention_mask", "token_type_ids"],
            [inputIdsValue, attentionMaskValue, tokenTypeIdsValue],
            ["last_hidden_state"]);

        // last_hidden_state: [batch, length, 384]
        var hidden = outputs[0].GetTensorDataAsSpan<float>();
        var dims = ModelFiles.Dimensions;
        var result = new float[batch][];

        for (var b = 0; b < batch; b++)
        {
            var pooled = new float[dims];
            var tokens = ids[b].Count;
            for (var t = 0; t < tokens; t++)
            {
                TensorPrimitives.Add(pooled, hidden.Slice((b * length + t) * dims, dims), pooled);
            }

            TensorPrimitives.Divide(pooled, tokens, pooled);
            TensorPrimitives.Divide(pooled, TensorPrimitives.Norm(pooled), pooled);
            result[b] = pooled;
        }

        return result;
    }

    private IReadOnlyList<int> Tokenize(string text)
    {
        // [CLS] ... [SEP]; truncate long inputs but keep the trailing [SEP].
        var ids = _tokenizer.EncodeToIds(text);
        return ids.Count <= MaxTokens ? ids : [.. ids.Take(MaxTokens - 1), ids[^1]];
    }

    public void Dispose() => _session.Dispose();
}
