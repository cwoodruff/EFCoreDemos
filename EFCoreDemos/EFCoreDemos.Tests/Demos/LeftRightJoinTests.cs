extern alias left_right_join;

using EFCoreDemos.Tests.Infrastructure;
using left_right_join::left_right_join;
using left_right_join::left_right_join.Chinook;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// left-right-join: EF Core 10's LeftJoin / RightJoin operators translate to the same LEFT / RIGHT JOIN
    /// as the old GroupJoin + SelectMany + DefaultIfEmpty pattern and return the same rows.
    /// </summary>
    public class LeftRightJoinTests : IDisposable
    {
        // Seeded Chinook: 275 artists, 347 albums, 71 artists with no album -> 347 + 71 = 418 left-join rows.
        // 8 employees, 59 customers; 5 employees support no customers -> 59 + 5 = 64 right-join rows.
        private const int ArtistAlbumRows = 418;
        private const int ArtistsWithoutAlbums = 71;
        private const int RepCustomerRows = 64;
        private const int EmployeesWithoutCustomers = 5;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext() => new(_db.Options<ChinookContext>());

        [Fact]
        public void LeftJoin_GeneratesTheSameSqlAsGroupJoinSelectManyDefaultIfEmpty()
        {
            using var db = CreateContext();

            var oldSql = JoinQueries.ArtistAlbumsOldWay(db).ToQueryString();
            var newSql = JoinQueries.ArtistAlbumsLeftJoin(db).ToQueryString();

            Assert.Contains("LEFT JOIN \"Album\"", newSql);
            Assert.Equal(oldSql, newSql);
        }

        [Fact]
        public void LeftJoin_KeepsArtistsWithoutAlbums_AndMatchesTheOldPattern()
        {
            using var db = CreateContext();

            var oldRows = JoinQueries.ArtistAlbumsOldWay(db).ToList();
            var newRows = JoinQueries.ArtistAlbumsLeftJoin(db).ToList();

            Assert.Equal(ArtistAlbumRows, newRows.Count);
            Assert.Equal(ArtistsWithoutAlbums, newRows.Count(r => r.AlbumTitle is null));
            Assert.Equal(oldRows, newRows);
        }

        [Fact]
        public void RightJoin_TranslatesToRightJoin()
        {
            using var db = CreateContext();

            var sql = JoinQueries.SupportRepsRightJoin(db).ToQueryString();

            Assert.Contains("FROM \"Customer\"", sql);
            Assert.Contains("RIGHT JOIN \"Employee\"", sql);
        }

        [Fact]
        public void RightJoin_KeepsEmployeesWithoutCustomers_AndMatchesTheFlippedLeftJoin()
        {
            using var db = CreateContext();

            var rightRows = JoinQueries.SupportRepsRightJoin(db).ToList();
            var flippedRows = JoinQueries.SupportRepsOldWay(db).ToList();

            Assert.Equal(RepCustomerRows, rightRows.Count);
            Assert.Equal(EmployeesWithoutCustomers, rightRows.Count(r => r.CustomerName is null));
            Assert.Equal(flippedRows, rightRows);
        }
    }
}
