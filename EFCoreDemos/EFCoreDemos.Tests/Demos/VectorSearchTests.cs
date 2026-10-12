extern alias vector_search;

using System.Net.Sockets;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using vector_search::vector_search.Data;
using vector_search::vector_search.Embeddings;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// vector-search: Chinook tracks embedded with all-MiniLM-L6-v2 (in-process ONNX) into a vector(384)
    /// column in the demo-owned VectorDemo database, searched with EF.Functions.VectorDistance.
    /// Tests that need the model skip when it isn't cached and huggingface.co is unreachable.
    /// </summary>
    public class VectorSearchTests
    {
        private static readonly Lazy<Task<TextEmbedder>> SharedEmbedder = new(() => TextEmbedder.CreateAsync());

        private static async Task<TextEmbedder> SeededEmbedderAsync()
        {
            var embedder = await SharedEmbedder.Value;
            await VectorStore.EnsureSeededAsync(embedder);
            return embedder;
        }

        private static VectorDemoContext CreateContext() => new(VectorStore.Options(
            SqlServerTestServer.ConnectionString + "Database=VectorDemo;"));

        [Fact]
        public void ServerSideQuery_TranslatesToVectorDistanceOrderByTop()
        {
            using var db = CreateContext();
            var query = new SqlVector<float>(new float[ModelFiles.Dimensions]);

            var sql = VectorStore.ServerSideQuery(db, query, 5).ToQueryString();

            Assert.Contains("SELECT TOP(@p)", sql);
            Assert.Contains("ORDER BY VECTOR_DISTANCE('cosine', [t].[Embedding], @query)", sql);
            Assert.Contains("FROM [TrackEmbedding]", sql);
        }

        [Fact]
        public void Model_EmbeddingColumnIsVector384()
        {
            using var db = CreateContext();
            var property = db.Model.FindEntityType(typeof(TrackEmbedding))!.FindProperty(nameof(TrackEmbedding.Embedding))!;

            Assert.Equal("vector(384)", property.GetColumnType());
            Assert.Equal(typeof(SqlVector<float>), property.ClrType);
        }

        [EmbeddingModelFact]
        public async Task Embedder_ProducesNormalizedVectorsThatCaptureMeaning()
        {
            var embedder = await SharedEmbedder.Value;

            var vectors = embedder.Embed(["heavy metal guitar", "loud metal riffs", "baroque violin concerto"]);

            Assert.All(vectors, v =>
            {
                Assert.Equal(ModelFiles.Dimensions, v.Length);
                Assert.Equal(1.0, Math.Sqrt(v.Sum(x => (double)x * x)), 3);
            });

            // Unit vectors: dot product == cosine similarity.
            static double Similarity(float[] a, float[] b) => a.Zip(b, (x, y) => (double)x * y).Sum();
            Assert.True(Similarity(vectors[0], vectors[1]) > Similarity(vectors[0], vectors[2]));

            // Batching pads inputs; the padding must not change the result.
            Assert.Equal(embedder.Embed("heavy metal guitar"), vectors[0], (x, y) => Math.Abs(x - y) < 1e-4);
        }

        [VectorSearchFact]
        public async Task Seed_EmbedsEveryChinookTrack()
        {
            await SeededEmbedderAsync();
            await using var db = CreateContext();

            Assert.Equal(3503, await db.TrackEmbeddings.CountAsync());
            Assert.Equal(0, await db.TrackEmbeddings.CountAsync(t => !t.Text.Contains(" by ")));
        }

        [VectorSearchFact]
        public async Task ServerSideSearch_FindsTheRightGenreAndMatchesClientSideRanking()
        {
            var embedder = await SeededEmbedderAsync();
            await using var db = CreateContext();

            var metal = embedder.Embed("aggressive heavy metal");
            var server = await VectorStore.ServerSideQuery(db, new SqlVector<float>(metal), 5).ToListAsync();
            var client = await VectorStore.ClientSideSearchAsync(db, metal, 5);

            Assert.Equal(5, server.Count);
            Assert.All(server, r => Assert.Contains("Metal", r.Genre));
            Assert.Equal(server.Select(r => r.Distance).Order(), server.Select(r => r.Distance));
            Assert.Equal(client.Select(r => r.TrackId), server.Select(r => r.TrackId));
            Assert.Equal(client.Select(r => r.Distance), server.Select(r => r.Distance), (x, y) => Math.Abs(x - y) < 1e-4);

            var classical = await VectorStore.ServerSideQuery(db, new SqlVector<float>(embedder.Embed("classical piano")), 5)
                .ToListAsync();
            Assert.Contains(classical, r => r.Genre == "Classical");
        }
    }

    /// <summary>Skipped when the embedding model isn't cached and can't be downloaded.</summary>
    public class EmbeddingModelFactAttribute : FactAttribute
    {
        private static readonly Lazy<bool> CanDownload = new(() =>
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync("huggingface.co", 443).Wait(TimeSpan.FromSeconds(3)) && client.Connected;
            }
            catch
            {
                return false;
            }
        });

        public static bool ModelAvailable => ModelFiles.IsCached || CanDownload.Value;

        public EmbeddingModelFactAttribute()
        {
            if (!ModelAvailable)
            {
                Skip = $"Embedding model not cached in {ModelFiles.Directory} and huggingface.co is unreachable.";
            }
        }
    }

    /// <summary>Needs both the embedding model and the docker SQL Server.</summary>
    public sealed class VectorSearchFactAttribute : EmbeddingModelFactAttribute
    {
        public VectorSearchFactAttribute()
        {
            if (Skip is null && !SqlServerTestServer.IsAvailable)
            {
                Skip = "SQL Server on localhost:1433 is not reachable (start the sql2025 docker container).";
            }
        }
    }
}
