using System.Security.Cryptography;

namespace vector_search.Embeddings;

/// <summary>
/// The all-MiniLM-L6-v2 sentence-transformers model (384 dimensions, Apache 2.0), exported to ONNX.
/// Downloaded on first run from a pinned Hugging Face revision into a git-ignored cache folder
/// (vector-search/models/) and reused afterwards. Run the demo once before a talk so it never downloads live.
/// </summary>
public static class ModelFiles
{
    public const string Repo = "sentence-transformers/all-MiniLM-L6-v2";

    // Pinned revision + hashes: the model can't change underneath the demo.
    public const string Revision = "1110a243fdf4706b3f48f1d95db1a4f5529b4d41";

    public const int Dimensions = 384;

    private static readonly (string Remote, string Local, string Sha256)[] Files =
    [
        ("onnx/model.onnx", "model.onnx", "6fd5d72fe4589f189f8ebc006442dbb529bb7ce38f8082112682524616046452"),
        ("vocab.txt", "vocab.txt", "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3"),
    ];

    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    /// <summary>
    /// vector-search/models next to the project (found by walking up to EFCoreDemos.sln), so the demo,
    /// the launcher and the tests all share one cache. Override with VECTOR_SEARCH_MODEL_DIR.
    /// </summary>
    public static string Directory { get; } = ResolveDirectory();

    public static string ModelPath => Path.Combine(Directory, "model.onnx");
    public static string VocabPath => Path.Combine(Directory, "vocab.txt");

    public static bool IsCached => Files.All(f => File.Exists(Path.Combine(Directory, f.Local)));

    public static async Task EnsureDownloadedAsync(TextWriter? log = null)
    {
        if (IsCached) return;

        await DownloadLock.WaitAsync();
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            foreach (var (remote, local, sha256) in Files)
            {
                var target = Path.Combine(Directory, local);
                if (File.Exists(target)) continue;

                var url = $"https://huggingface.co/{Repo}/resolve/{Revision}/{remote}";
                log?.WriteLine($"Downloading {Repo}/{remote} (first run only) ...");

                // Download to a temp file, verify, then move into place so a half-written file is never cached.
                var temp = target + $".{Guid.NewGuid():N}.part";
                try
                {
                    await using (var source = await http.GetStreamAsync(url))
                    await using (var file = File.Create(temp))
                    {
                        await source.CopyToAsync(file);
                    }

                    var actual = await HashAsync(temp);
                    if (!actual.Equals(sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"SHA-256 mismatch for {remote}: expected {sha256}, got {actual}.");

                    File.Move(temp, target, overwrite: true);
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException(
                        $"Could not download the embedding model from {url} and no cached copy exists in {Directory}. " +
                        "Connect to the internet and run the demo once to cache it.", ex);
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
            }

            log?.WriteLine($"Model cached in {Directory}");
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }

    private static string ResolveDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("VECTOR_SEARCH_MODEL_DIR");
        if (!string.IsNullOrWhiteSpace(overridden)) return overridden;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EFCoreDemos.sln")))
                return Path.Combine(dir.FullName, "vector-search", "models");
        }

        return Path.Combine(AppContext.BaseDirectory, "models");
    }
}
