using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace sproc_mapping.Chinook;

public partial class ChinookContext : DbContext
{
    public ChinookContext(DbContextOptions<ChinookContext> options)
        : base(options)
    {
    }

    public ChinookContext()
    {
    }

    static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information)
            .AddConsole();
    });

    public virtual DbSet<Album> Albums { get; set; } = null!;
    public virtual DbSet<Artist> Artists { get; set; } = null!;
    public virtual DbSet<Customer> Customers { get; set; } = null!;
    public virtual DbSet<Employee> Employees { get; set; } = null!;
    public virtual DbSet<Genre> Genres { get; set; } = null!;
    public virtual DbSet<Invoice> Invoices { get; set; } = null!;
    public virtual DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;
    public virtual DbSet<MediaType> MediaTypes { get; set; } = null!;
    public virtual DbSet<Playlist> Playlists { get; set; } = null!;
    public virtual DbSet<Track> Tracks { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder
                .UseSqlServer(
                    "Server=.;Database=Chinook;Trusted_Connection=True;TrustServerCertificate=True;Application Name=EFCoreDemos;")
                .UseLoggerFactory(loggerFactory);
        }
    }

    [DbFunction(Schema = "dbo")]
    public static int ComputeInvoiceCount(int customerId)
    {
        return 0;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Album");

            entity.HasIndex(e => e.ArtistId, "IFK_Artist_Album");

            entity.HasIndex(e => e.Id, "IPK_ProductItem");

            entity.Property(e => e.Title).HasMaxLength(160);

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.Artist)
                .WithMany(p => p.Albums)
                .HasForeignKey(d => d.ArtistId)
                .OnDelete(DeleteBehavior.ClientSetNull), "FK__Album__ArtistId__276EDEB3");
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Artist");

            entity.HasIndex(e => e.Id, "IPK_Artist");

            entity.Property(e => e.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Customer");

            entity.HasIndex(e => e.SupportRepId, "IFK_Employee_Customer");

            entity.HasIndex(e => e.Id, "IPK_Customer");

            entity.Property(e => e.Address).HasMaxLength(70);

            entity.Property(e => e.City).HasMaxLength(40);

            entity.Property(e => e.Company).HasMaxLength(80);

            entity.Property(e => e.Country).HasMaxLength(40);

            entity.Property(e => e.Email).HasMaxLength(60);

            entity.Property(e => e.Fax).HasMaxLength(24);

            entity.Property(e => e.FirstName).HasMaxLength(40);

            entity.Property(e => e.LastName).HasMaxLength(20);

            entity.Property(e => e.Phone).HasMaxLength(24);

            entity.Property(e => e.PostalCode).HasMaxLength(10);

            entity.Property(e => e.State).HasMaxLength(40);

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.SupportRep)
                .WithMany(p => p.Customers)
                .HasForeignKey(d => d.SupportRepId), "FK__Customer__Suppor__2C3393D0");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Employee");

            entity.HasIndex(e => e.ReportsTo, "IFK_Employee_ReportsTo");

            entity.HasIndex(e => e.Id, "IPK_Employee");

            entity.Property(e => e.Address).HasMaxLength(70);

            entity.Property(e => e.BirthDate).HasColumnType("datetime");

            entity.Property(e => e.City).HasMaxLength(40);

            entity.Property(e => e.Country).HasMaxLength(40);

            entity.Property(e => e.Email).HasMaxLength(60);

            entity.Property(e => e.Fax).HasMaxLength(24);

            entity.Property(e => e.FirstName).HasMaxLength(20);

            entity.Property(e => e.HireDate).HasColumnType("datetime");

            entity.Property(e => e.LastName).HasMaxLength(20);

            entity.Property(e => e.Phone).HasMaxLength(24);

            entity.Property(e => e.PostalCode).HasMaxLength(10);

            entity.Property(e => e.State).HasMaxLength(40);

            entity.Property(e => e.Title).HasMaxLength(30);

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.ReportsToNavigation)
                .WithMany(p => p.InverseReportsToNavigation)
                .HasForeignKey(d => d.ReportsTo), "FK__Employee__Report__2B3F6F97");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Genre");

            entity.HasIndex(e => e.Id, "IPK_Genre");

            entity.Property(e => e.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Invoice");

            entity.HasIndex(e => e.CustomerId, "IFK_Customer_Invoice");

            entity.HasIndex(e => e.Id, "IPK_Invoice");

            entity.Property(e => e.BillingAddress).HasMaxLength(70);

            entity.Property(e => e.BillingCity).HasMaxLength(40);

            entity.Property(e => e.BillingCountry).HasMaxLength(40);

            entity.Property(e => e.BillingPostalCode).HasMaxLength(10);

            entity.Property(e => e.BillingState).HasMaxLength(40);

            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");

            entity.Property(e => e.Total).HasColumnType("numeric(10, 2)");

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.Customer)
                .WithMany(p => p.Invoices)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull), "FK__Invoice__Custome__2D27B809");
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "InvoiceLine");

            entity.HasIndex(e => e.InvoiceId, "IFK_Invoice_InvoiceLine");

            entity.HasIndex(e => e.TrackId, "IFK_ProductItem_InvoiceLine");

            entity.HasIndex(e => e.Id, "IPK_InvoiceLine");

            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10, 2)");

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.Invoice)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull), "FK__InvoiceLi__Invoi__2F10007B");

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.Track)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.TrackId)
                .OnDelete(DeleteBehavior.ClientSetNull), "FK__InvoiceLi__Track__2E1BDC42");
        });

        modelBuilder.Entity<MediaType>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "MediaType");

            entity.HasIndex(e => e.Id, "IPK_MediaType");

            entity.Property(e => e.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Playlist");

            entity.HasIndex(e => e.Id, "IPK_Playlist");

            entity.Property(e => e.Name).HasMaxLength(120);

            entity.HasMany(d => d.Tracks)
                .WithMany(p => p.Playlists)
                .UsingEntity<Dictionary<string, object>>(
                    "PlaylistTrack",
                    l => l.HasOne<Track>().WithMany().HasForeignKey("TrackId").OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__PlaylistT__Track__300424B4"),
                    r => r.HasOne<Playlist>().WithMany().HasForeignKey("PlaylistId")
                        .OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__PlaylistT__Playl__30F848ED"),
                    j =>
                    {
                        j.HasKey("PlaylistId", "TrackId").HasName("PK__Playlist__A4A6282E25869641");

                        j.ToTable("PlaylistTrack");

                        j.HasIndex(new[] { "PlaylistId" }, "IFK_Playlist_PlaylistTrack");

                        j.HasIndex(new[] { "TrackId" }, "IFK_Track_PlaylistTrack");

                        j.HasIndex(new[] { "PlaylistId" }, "IPK_PlaylistTrack");
                    });
        });

        modelBuilder.Entity<Track>(entity =>
        {
            RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)entity, "Track");

            entity.HasIndex(e => e.AlbumId, "IFK_Album_Track");

            entity.HasIndex(e => e.GenreId, "IFK_Genre_Track");

            entity.HasIndex(e => e.MediaTypeId, "IFK_MediaType_Track");

            entity.HasIndex(e => e.Id, "IPK_Track");

            entity.Property(e => e.Composer).HasMaxLength(220);

            entity.Property(e => e.Name).HasMaxLength(200);

            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10, 2)");

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.Album)
                .WithMany(p => p.Tracks)
                .HasForeignKey(d => d.AlbumId), "FK__Track__AlbumId__286302EC");

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.Genre)
                .WithMany(p => p.Tracks)
                .HasForeignKey(d => d.GenreId), "FK__Track__GenreId__2A4B4B5E");

            RelationalForeignKeyBuilderExtensions.HasConstraintName((ReferenceCollectionBuilder)entity
                .HasOne(d => d.MediaType)
                .WithMany(p => p.Tracks)
                .HasForeignKey(d => d.MediaTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull), "FK__Track__MediaType__29572725");
        });

        modelBuilder.Entity<Album>()
            .InsertUsingStoredProcedure(
                "sproc_InsertAlbum",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.Title);
                    storedProcedureBuilder.HasParameter(e => e.ArtistId);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateAlbum",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.Title);
                    storedProcedureBuilder.HasParameter(e => e.ArtistId);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteAlbum",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<Artist>()
            .InsertUsingStoredProcedure(
                "sproc_InsertArtist",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateArtist",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteArtist",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<Customer>()
            .InsertUsingStoredProcedure(
                "sproc_InsertCustomer",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.FirstName);
                    storedProcedureBuilder.HasParameter(e => e.LastName);
                    storedProcedureBuilder.HasParameter(e => e.Company);
                    storedProcedureBuilder.HasParameter(e => e.Address);
                    storedProcedureBuilder.HasParameter(e => e.City);
                    storedProcedureBuilder.HasParameter(e => e.State);
                    storedProcedureBuilder.HasParameter(e => e.Country);
                    storedProcedureBuilder.HasParameter(e => e.PostalCode);
                    storedProcedureBuilder.HasParameter(e => e.Phone);
                    storedProcedureBuilder.HasParameter(e => e.Fax);
                    storedProcedureBuilder.HasParameter(e => e.Email);
                    storedProcedureBuilder.HasParameter(e => e.SupportRepId);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateCustomer",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.FirstName);
                    storedProcedureBuilder.HasParameter(e => e.LastName);
                    storedProcedureBuilder.HasParameter(e => e.Company);
                    storedProcedureBuilder.HasParameter(e => e.Address);
                    storedProcedureBuilder.HasParameter(e => e.City);
                    storedProcedureBuilder.HasParameter(e => e.State);
                    storedProcedureBuilder.HasParameter(e => e.Country);
                    storedProcedureBuilder.HasParameter(e => e.PostalCode);
                    storedProcedureBuilder.HasParameter(e => e.Phone);
                    storedProcedureBuilder.HasParameter(e => e.Fax);
                    storedProcedureBuilder.HasParameter(e => e.Email);
                    storedProcedureBuilder.HasParameter(e => e.SupportRepId);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteCustomer",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<Employee>()
            .InsertUsingStoredProcedure(
                "sproc_InsertEmployee",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.FirstName);
                    storedProcedureBuilder.HasParameter(e => e.LastName);
                    storedProcedureBuilder.HasParameter(e => e.Title);
                    storedProcedureBuilder.HasParameter(e => e.ReportsTo);
                    storedProcedureBuilder.HasParameter(e => e.BirthDate);
                    storedProcedureBuilder.HasParameter(e => e.HireDate);
                    storedProcedureBuilder.HasParameter(e => e.Address);
                    storedProcedureBuilder.HasParameter(e => e.City);
                    storedProcedureBuilder.HasParameter(e => e.State);
                    storedProcedureBuilder.HasParameter(e => e.Country);
                    storedProcedureBuilder.HasParameter(e => e.PostalCode);
                    storedProcedureBuilder.HasParameter(e => e.Phone);
                    storedProcedureBuilder.HasParameter(e => e.Fax);
                    storedProcedureBuilder.HasParameter(e => e.Email);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateEmployee",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.FirstName);
                    storedProcedureBuilder.HasParameter(e => e.LastName);
                    storedProcedureBuilder.HasParameter(e => e.Title);
                    storedProcedureBuilder.HasParameter(e => e.ReportsTo);
                    storedProcedureBuilder.HasParameter(e => e.BirthDate);
                    storedProcedureBuilder.HasParameter(e => e.HireDate);
                    storedProcedureBuilder.HasParameter(e => e.Address);
                    storedProcedureBuilder.HasParameter(e => e.City);
                    storedProcedureBuilder.HasParameter(e => e.State);
                    storedProcedureBuilder.HasParameter(e => e.Country);
                    storedProcedureBuilder.HasParameter(e => e.PostalCode);
                    storedProcedureBuilder.HasParameter(e => e.Phone);
                    storedProcedureBuilder.HasParameter(e => e.Fax);
                    storedProcedureBuilder.HasParameter(e => e.Email);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteEmployee",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<Genre>()
            .InsertUsingStoredProcedure(
                "sproc_InsertGenre",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateGenre",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteGenre",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<Invoice>()
            .InsertUsingStoredProcedure(
                "sproc_InsertInvoice",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.CustomerId);
                    storedProcedureBuilder.HasParameter(e => e.InvoiceDate);
                    storedProcedureBuilder.HasParameter(e => e.BillingAddress);
                    storedProcedureBuilder.HasParameter(e => e.BillingCity);
                    storedProcedureBuilder.HasParameter(e => e.BillingState);
                    storedProcedureBuilder.HasParameter(e => e.BillingCountry);
                    storedProcedureBuilder.HasParameter(e => e.BillingPostalCode);
                    storedProcedureBuilder.HasParameter(e => e.Total);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateInvoice",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.CustomerId);
                    storedProcedureBuilder.HasParameter(e => e.InvoiceDate);
                    storedProcedureBuilder.HasParameter(e => e.BillingAddress);
                    storedProcedureBuilder.HasParameter(e => e.BillingCity);
                    storedProcedureBuilder.HasParameter(e => e.BillingState);
                    storedProcedureBuilder.HasParameter(e => e.BillingCountry);
                    storedProcedureBuilder.HasParameter(e => e.BillingPostalCode);
                    storedProcedureBuilder.HasParameter(e => e.Total);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteInvoice",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<InvoiceLine>()
            .InsertUsingStoredProcedure(
                "sproc_InsertInvoiceLine",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.InvoiceId);
                    storedProcedureBuilder.HasParameter(e => e.TrackId);
                    storedProcedureBuilder.HasParameter(e => e.UnitPrice);
                    storedProcedureBuilder.HasParameter(e => e.Quantity);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateInvoiceLine",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.InvoiceId);
                    storedProcedureBuilder.HasParameter(e => e.TrackId);
                    storedProcedureBuilder.HasParameter(e => e.UnitPrice);
                    storedProcedureBuilder.HasParameter(e => e.Quantity);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteInvoiceLine",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<MediaType>()
            .InsertUsingStoredProcedure(
                "sproc_InsertMediaType",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateMediaType",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteMediaType",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        modelBuilder.Entity<Playlist>()
            .InsertUsingStoredProcedure(
                "sproc_InsertPlaylist",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdatePlaylist",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeletePlaylist",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });

        // modelBuilder.Entity<PlaylistTrack>()
        //     .InsertUsingStoredProcedure(
        //         "sproc_InsertPlaylistTrack",
        //         storedProcedureBuilder =>
        //         {
        //             storedProcedureBuilder.HasParameter(e => e.PlaylistId);
        //             storedProcedureBuilder.HasParameter(e => e.TrackId);
        //             storedProcedureBuilder.HasRowsAffectedResultColumn();
        //         })
        //     .UpdateUsingStoredProcedure(
        //         "sproc_UpdatePlaylistTrack",
        //         storedProcedureBuilder =>
        //         {
        //             storedProcedureBuilder.HasParameter(e => e.PlaylistId);
        //             storedProcedureBuilder.HasParameter(e => e.TrackId);
        //             storedProcedureBuilder.HasRowsAffectedResultColumn();
        //         })
        //     .DeleteUsingStoredProcedure(
        //         "sproc_DeletePlaylistTrack",
        //         storedProcedureBuilder =>
        //         {
        //             storedProcedureBuilder.HasOriginalValueParameter(e => e.PlaylistId);
        //             storedProcedureBuilder.HasOriginalValueParameter(e => e.TrackId);
        //             storedProcedureBuilder.HasRowsAffectedResultColumn();
        //         });

        modelBuilder.Entity<Track>()
            .InsertUsingStoredProcedure(
                "sproc_InsertTrack",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasParameter(e => e.AlbumId);
                    storedProcedureBuilder.HasParameter(e => e.MediaTypeId);
                    storedProcedureBuilder.HasParameter(e => e.GenreId);
                    storedProcedureBuilder.HasParameter(e => e.Composer);
                    storedProcedureBuilder.HasParameter(e => e.Milliseconds);
                    storedProcedureBuilder.HasParameter(e => e.Bytes);
                    storedProcedureBuilder.HasParameter(e => e.UnitPrice);
                    storedProcedureBuilder.HasResultColumn(e => e.Id);
                })
            .UpdateUsingStoredProcedure(
                "sproc_UpdateTrack",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasParameter(e => e.Name);
                    storedProcedureBuilder.HasParameter(e => e.AlbumId);
                    storedProcedureBuilder.HasParameter(e => e.MediaTypeId);
                    storedProcedureBuilder.HasParameter(e => e.GenreId);
                    storedProcedureBuilder.HasParameter(e => e.Composer);
                    storedProcedureBuilder.HasParameter(e => e.Milliseconds);
                    storedProcedureBuilder.HasParameter(e => e.Bytes);
                    storedProcedureBuilder.HasParameter(e => e.UnitPrice);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                })
            .DeleteUsingStoredProcedure(
                "sproc_DeleteTrack",
                storedProcedureBuilder =>
                {
                    storedProcedureBuilder.HasOriginalValueParameter(e => e.Id);
                    storedProcedureBuilder.HasRowsAffectedResultColumn();
                });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}