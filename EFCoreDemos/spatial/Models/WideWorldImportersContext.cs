using Microsoft.EntityFrameworkCore;

namespace spatial.Models;

public partial class WideWorldImportersContext : DbContext
{
    public WideWorldImportersContext()
    {
    }

    public WideWorldImportersContext(DbContextOptions<WideWorldImportersContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer(
                "Server=.;initial catalog=WideWorldImporters;Trusted_Connection=True;TrustServerCertificate=True;Application Name=EFCoreDemos;",
                x => x.UseNetTopologySuite());
    }

    public virtual DbSet<BuyingGroups> BuyingGroups { get; set; }
    public virtual DbSet<Cities> Cities { get; set; }
    public virtual DbSet<ColdRoomTemperatures> ColdRoomTemperatures { get; set; }
    public virtual DbSet<Colors> Colors { get; set; }
    public virtual DbSet<Countries> Countries { get; set; }
    public virtual DbSet<CustomerCategories> CustomerCategories { get; set; }
    public virtual DbSet<CustomerTransactions> CustomerTransactions { get; set; }
    public virtual DbSet<Customers> Customers { get; set; }
    public virtual DbSet<DeliveryMethods> DeliveryMethods { get; set; }
    public virtual DbSet<InvoiceLines> InvoiceLines { get; set; }
    public virtual DbSet<Invoices> Invoices { get; set; }
    public virtual DbSet<OrderLines> OrderLines { get; set; }
    public virtual DbSet<Orders> Orders { get; set; }
    public virtual DbSet<PackageTypes> PackageTypes { get; set; }
    public virtual DbSet<PaymentMethods> PaymentMethods { get; set; }
    public virtual DbSet<People> People { get; set; }
    public virtual DbSet<PurchaseOrderLines> PurchaseOrderLines { get; set; }
    public virtual DbSet<PurchaseOrders> PurchaseOrders { get; set; }
    public virtual DbSet<SpecialDeals> SpecialDeals { get; set; }
    public virtual DbSet<StateProvinces> StateProvinces { get; set; }
    public virtual DbSet<StockGroups> StockGroups { get; set; }
    public virtual DbSet<StockItemHoldings> StockItemHoldings { get; set; }
    public virtual DbSet<StockItemStockGroups> StockItemStockGroups { get; set; }
    public virtual DbSet<StockItemTransactions> StockItemTransactions { get; set; }
    public virtual DbSet<StockItems> StockItems { get; set; }
    public virtual DbSet<SupplierCategories> SupplierCategories { get; set; }
    public virtual DbSet<SupplierTransactions> SupplierTransactions { get; set; }
    public virtual DbSet<Suppliers> Suppliers { get; set; }
    public virtual DbSet<SystemParameters> SystemParameters { get; set; }
    public virtual DbSet<TransactionTypes> TransactionTypes { get; set; }
    public virtual DbSet<VehicleTemperatures> VehicleTemperatures { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "2.2.1-servicing-10028");

        modelBuilder.Entity<BuyingGroups>(entity =>
        {
            entity.HasKey(e => e.BuyingGroupId)
                .HasName("PK_Sales_BuyingGroups");

            entity.ToTable("BuyingGroups", "Sales");

            entity.HasIndex(e => e.BuyingGroupName)
                .HasDatabaseName("UQ_Sales_BuyingGroups_BuyingGroupName")
                .IsUnique();

            entity.Property(e => e.BuyingGroupId)
                .HasColumnName("BuyingGroupID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[BuyingGroupID])");

            entity.Property(e => e.BuyingGroupName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.BuyingGroups)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_BuyingGroups_Application_People");
        });

        modelBuilder.Entity<Cities>(entity =>
        {
            entity.HasKey(e => e.CityId)
                .HasName("PK_Application_Cities");

            entity.ToTable("Cities", "Application");

            entity.HasIndex(e => e.StateProvinceId)
                .HasDatabaseName("FK_Application_Cities_StateProvinceID");

            entity.Property(e => e.CityId)
                .HasColumnName("CityID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[CityID])");

            entity.Property(e => e.CityName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.StateProvinceId).HasColumnName("StateProvinceID");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.Cities)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_Cities_Application_People");

            entity.HasOne(d => d.StateProvince)
                .WithMany(p => p.Cities)
                .HasForeignKey(d => d.StateProvinceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_Cities_StateProvinceID_Application_StateProvinces");
        });

        modelBuilder.Entity<ColdRoomTemperatures>(entity =>
        {
            entity.HasKey(e => e.ColdRoomTemperatureId)
                .HasName("PK_Warehouse_ColdRoomTemperatures");

            entity.ToTable("ColdRoomTemperatures", "Warehouse");

            entity.HasIndex(e => e.ColdRoomSensorNumber)
                .HasDatabaseName("IX_Warehouse_ColdRoomTemperatures_ColdRoomSensorNumber");

            entity.Property(e => e.ColdRoomTemperatureId).HasColumnName("ColdRoomTemperatureID");

            entity.Property(e => e.Temperature).HasColumnType("decimal(10, 2)");
        });

        modelBuilder.Entity<Colors>(entity =>
        {
            entity.HasKey(e => e.ColorId)
                .HasName("PK_Warehouse_Colors");

            entity.ToTable("Colors", "Warehouse");

            entity.HasIndex(e => e.ColorName)
                .HasDatabaseName("UQ_Warehouse_Colors_ColorName")
                .IsUnique();

            entity.Property(e => e.ColorId)
                .HasColumnName("ColorID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[ColorID])");

            entity.Property(e => e.ColorName)
                .IsRequired()
                .HasMaxLength(20);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.Colors)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_Colors_Application_People");
        });

        modelBuilder.Entity<Countries>(entity =>
        {
            entity.HasKey(e => e.CountryId)
                .HasName("PK_Application_Countries");

            entity.ToTable("Countries", "Application");

            entity.HasIndex(e => e.CountryName)
                .HasDatabaseName("UQ_Application_Countries_CountryName")
                .IsUnique();

            entity.HasIndex(e => e.FormalName)
                .HasDatabaseName("UQ_Application_Countries_FormalName")
                .IsUnique();

            entity.Property(e => e.CountryId)
                .HasColumnName("CountryID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[CountryID])");

            entity.Property(e => e.Continent)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(e => e.CountryName)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.CountryType).HasMaxLength(20);

            entity.Property(e => e.FormalName)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.IsoAlpha3Code).HasMaxLength(3);

            entity.Property(e => e.Region)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(e => e.Subregion)
                .IsRequired()
                .HasMaxLength(30);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.Countries)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_Countries_Application_People");
        });

        modelBuilder.Entity<CustomerCategories>(entity =>
        {
            entity.HasKey(e => e.CustomerCategoryId)
                .HasName("PK_Sales_CustomerCategories");

            entity.ToTable("CustomerCategories", "Sales");

            entity.HasIndex(e => e.CustomerCategoryName)
                .HasDatabaseName("UQ_Sales_CustomerCategories_CustomerCategoryName")
                .IsUnique();

            entity.Property(e => e.CustomerCategoryId)
                .HasColumnName("CustomerCategoryID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[CustomerCategoryID])");

            entity.Property(e => e.CustomerCategoryName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.CustomerCategories)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_CustomerCategories_Application_People");
        });

        modelBuilder.Entity<CustomerTransactions>(entity =>
        {
            entity.HasKey(e => e.CustomerTransactionId)
                .HasName("PK_Sales_CustomerTransactions");

            entity.ToTable("CustomerTransactions", "Sales");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("FK_Sales_CustomerTransactions_CustomerID");

            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("FK_Sales_CustomerTransactions_InvoiceID");

            entity.HasIndex(e => e.IsFinalized)
                .HasDatabaseName("IX_Sales_CustomerTransactions_IsFinalized");

            entity.HasIndex(e => e.PaymentMethodId)
                .HasDatabaseName("FK_Sales_CustomerTransactions_PaymentMethodID");

            entity.HasIndex(e => e.TransactionTypeId)
                .HasDatabaseName("FK_Sales_CustomerTransactions_TransactionTypeID");

            entity.Property(e => e.CustomerTransactionId)
                .HasColumnName("CustomerTransactionID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[TransactionID])");

            entity.Property(e => e.AmountExcludingTax).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

            entity.Property(e => e.FinalizationDate).HasColumnType("date");

            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");

            entity.Property(e => e.IsFinalized).HasComputedColumnSql(
                "(case when [FinalizationDate] IS NULL then CONVERT([bit],(0)) else CONVERT([bit],(1)) end)");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.OutstandingBalance).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.PaymentMethodId).HasColumnName("PaymentMethodID");

            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.TransactionDate).HasColumnType("date");

            entity.Property(e => e.TransactionTypeId).HasColumnName("TransactionTypeID");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.CustomerTransactions)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_CustomerTransactions_CustomerID_Sales_Customers");

            entity.HasOne(d => d.Invoice)
                .WithMany(p => p.CustomerTransactions)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK_Sales_CustomerTransactions_InvoiceID_Sales_Invoices");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.CustomerTransactions)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_CustomerTransactions_Application_People");

            entity.HasOne(d => d.PaymentMethod)
                .WithMany(p => p.CustomerTransactions)
                .HasForeignKey(d => d.PaymentMethodId)
                .HasConstraintName("FK_Sales_CustomerTransactions_PaymentMethodID_Application_PaymentMethods");

            entity.HasOne(d => d.TransactionType)
                .WithMany(p => p.CustomerTransactions)
                .HasForeignKey(d => d.TransactionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_CustomerTransactions_TransactionTypeID_Application_TransactionTypes");
        });

        modelBuilder.Entity<Customers>(entity =>
        {
            entity.HasKey(e => e.CustomerId)
                .HasName("PK_Sales_Customers");

            entity.ToTable("Customers", "Sales");

            entity.HasIndex(e => e.AlternateContactPersonId)
                .HasDatabaseName("FK_Sales_Customers_AlternateContactPersonID");

            entity.HasIndex(e => e.BuyingGroupId)
                .HasDatabaseName("FK_Sales_Customers_BuyingGroupID");

            entity.HasIndex(e => e.CustomerCategoryId)
                .HasDatabaseName("FK_Sales_Customers_CustomerCategoryID");

            entity.HasIndex(e => e.CustomerName)
                .HasDatabaseName("UQ_Sales_Customers_CustomerName")
                .IsUnique();

            entity.HasIndex(e => e.DeliveryCityId)
                .HasDatabaseName("FK_Sales_Customers_DeliveryCityID");

            entity.HasIndex(e => e.DeliveryMethodId)
                .HasDatabaseName("FK_Sales_Customers_DeliveryMethodID");

            entity.HasIndex(e => e.PostalCityId)
                .HasDatabaseName("FK_Sales_Customers_PostalCityID");

            entity.HasIndex(e => e.PrimaryContactPersonId)
                .HasDatabaseName("FK_Sales_Customers_PrimaryContactPersonID");

            entity.HasIndex(e => new { e.PrimaryContactPersonId, e.IsOnCreditHold, e.CustomerId, e.BillToCustomerId })
                .HasDatabaseName("IX_Sales_Customers_Perf_20160301_06");

            entity.Property(e => e.CustomerId)
                .HasColumnName("CustomerID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[CustomerID])");

            entity.Property(e => e.AccountOpenedDate).HasColumnType("date");

            entity.Property(e => e.AlternateContactPersonId).HasColumnName("AlternateContactPersonID");

            entity.Property(e => e.BillToCustomerId).HasColumnName("BillToCustomerID");

            entity.Property(e => e.BuyingGroupId).HasColumnName("BuyingGroupID");

            entity.Property(e => e.CreditLimit).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.CustomerCategoryId).HasColumnName("CustomerCategoryID");

            entity.Property(e => e.CustomerName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.DeliveryAddressLine1)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.DeliveryAddressLine2).HasMaxLength(60);

            entity.Property(e => e.DeliveryCityId).HasColumnName("DeliveryCityID");

            entity.Property(e => e.DeliveryMethodId).HasColumnName("DeliveryMethodID");

            entity.Property(e => e.DeliveryPostalCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.DeliveryRun).HasMaxLength(5);

            entity.Property(e => e.FaxNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.PostalAddressLine1)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.PostalAddressLine2).HasMaxLength(60);

            entity.Property(e => e.PostalCityId).HasColumnName("PostalCityID");

            entity.Property(e => e.PostalPostalCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.PrimaryContactPersonId).HasColumnName("PrimaryContactPersonID");

            entity.Property(e => e.RunPosition).HasMaxLength(5);

            entity.Property(e => e.StandardDiscountPercentage).HasColumnType("decimal(18, 3)");

            entity.Property(e => e.WebsiteUrl)
                .IsRequired()
                .HasColumnName("WebsiteURL")
                .HasMaxLength(256);

            entity.HasOne(d => d.AlternateContactPerson)
                .WithMany(p => p.CustomersAlternateContactPerson)
                .HasForeignKey(d => d.AlternateContactPersonId)
                .HasConstraintName("FK_Sales_Customers_AlternateContactPersonID_Application_People");

            entity.HasOne(d => d.BillToCustomer)
                .WithMany(p => p.InverseBillToCustomer)
                .HasForeignKey(d => d.BillToCustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Customers_BillToCustomerID_Sales_Customers");

            entity.HasOne(d => d.BuyingGroup)
                .WithMany(p => p.Customers)
                .HasForeignKey(d => d.BuyingGroupId)
                .HasConstraintName("FK_Sales_Customers_BuyingGroupID_Sales_BuyingGroups");

            entity.HasOne(d => d.CustomerCategory)
                .WithMany(p => p.Customers)
                .HasForeignKey(d => d.CustomerCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Customers_CustomerCategoryID_Sales_CustomerCategories");

            entity.HasOne(d => d.DeliveryCity)
                .WithMany(p => p.CustomersDeliveryCity)
                .HasForeignKey(d => d.DeliveryCityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Customers_DeliveryCityID_Application_Cities");

            entity.HasOne(d => d.DeliveryMethod)
                .WithMany(p => p.Customers)
                .HasForeignKey(d => d.DeliveryMethodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Customers_DeliveryMethodID_Application_DeliveryMethods");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.CustomersLastEditedByNavigation)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Customers_Application_People");

            entity.HasOne(d => d.PostalCity)
                .WithMany(p => p.CustomersPostalCity)
                .HasForeignKey(d => d.PostalCityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Customers_PostalCityID_Application_Cities");

            entity.HasOne(d => d.PrimaryContactPerson)
                .WithMany(p => p.CustomersPrimaryContactPerson)
                .HasForeignKey(d => d.PrimaryContactPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Customers_PrimaryContactPersonID_Application_People");
        });

        modelBuilder.Entity<DeliveryMethods>(entity =>
        {
            entity.HasKey(e => e.DeliveryMethodId)
                .HasName("PK_Application_DeliveryMethods");

            entity.ToTable("DeliveryMethods", "Application");

            entity.HasIndex(e => e.DeliveryMethodName)
                .HasDatabaseName("UQ_Application_DeliveryMethods_DeliveryMethodName")
                .IsUnique();

            entity.Property(e => e.DeliveryMethodId)
                .HasColumnName("DeliveryMethodID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[DeliveryMethodID])");

            entity.Property(e => e.DeliveryMethodName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.DeliveryMethods)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_DeliveryMethods_Application_People");
        });

        modelBuilder.Entity<InvoiceLines>(entity =>
        {
            entity.HasKey(e => e.InvoiceLineId)
                .HasName("PK_Sales_InvoiceLines");

            entity.ToTable("InvoiceLines", "Sales");

            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("FK_Sales_InvoiceLines_InvoiceID");

            entity.HasIndex(e => e.PackageTypeId)
                .HasDatabaseName("FK_Sales_InvoiceLines_PackageTypeID");

            entity.HasIndex(e => e.StockItemId)
                .HasDatabaseName("FK_Sales_InvoiceLines_StockItemID");

            entity.Property(e => e.InvoiceLineId)
                .HasColumnName("InvoiceLineID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[InvoiceLineID])");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ExtendedPrice).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.LineProfit).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.PackageTypeId).HasColumnName("PackageTypeID");

            entity.Property(e => e.StockItemId).HasColumnName("StockItemID");

            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.TaxRate).HasColumnType("decimal(18, 3)");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_InvoiceLines_InvoiceID_Sales_Invoices");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_InvoiceLines_Application_People");

            entity.HasOne(d => d.PackageType)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.PackageTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_InvoiceLines_PackageTypeID_Warehouse_PackageTypes");

            entity.HasOne(d => d.StockItem)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.StockItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_InvoiceLines_StockItemID_Warehouse_StockItems");
        });

        modelBuilder.Entity<Invoices>(entity =>
        {
            entity.HasKey(e => e.InvoiceId)
                .HasName("PK_Sales_Invoices");

            entity.ToTable("Invoices", "Sales");

            entity.HasIndex(e => e.AccountsPersonId)
                .HasDatabaseName("FK_Sales_Invoices_AccountsPersonID");

            entity.HasIndex(e => e.BillToCustomerId)
                .HasDatabaseName("FK_Sales_Invoices_BillToCustomerID");

            entity.HasIndex(e => e.ContactPersonId)
                .HasDatabaseName("FK_Sales_Invoices_ContactPersonID");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("FK_Sales_Invoices_CustomerID");

            entity.HasIndex(e => e.DeliveryMethodId)
                .HasDatabaseName("FK_Sales_Invoices_DeliveryMethodID");

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("FK_Sales_Invoices_OrderID");

            entity.HasIndex(e => e.PackedByPersonId)
                .HasDatabaseName("FK_Sales_Invoices_PackedByPersonID");

            entity.HasIndex(e => e.SalespersonPersonId)
                .HasDatabaseName("FK_Sales_Invoices_SalespersonPersonID");

            entity.HasIndex(e => new { e.ConfirmedReceivedBy, e.ConfirmedDeliveryTime })
                .HasDatabaseName("IX_Sales_Invoices_ConfirmedDeliveryTime");

            entity.Property(e => e.InvoiceId)
                .HasColumnName("InvoiceID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[InvoiceID])");

            entity.Property(e => e.AccountsPersonId).HasColumnName("AccountsPersonID");

            entity.Property(e => e.BillToCustomerId).HasColumnName("BillToCustomerID");

            entity.Property(e => e.ConfirmedDeliveryTime).HasComputedColumnSql(
                "(TRY_CONVERT([datetime2](7),json_value([ReturnedDeliveryData],N'$.DeliveredWhen'),(126)))");

            entity.Property(e => e.ConfirmedReceivedBy)
                .HasMaxLength(4000)
                .HasComputedColumnSql("(json_value([ReturnedDeliveryData],N'$.ReceivedBy'))");

            entity.Property(e => e.ContactPersonId).HasColumnName("ContactPersonID");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

            entity.Property(e => e.CustomerPurchaseOrderNumber).HasMaxLength(20);

            entity.Property(e => e.DeliveryMethodId).HasColumnName("DeliveryMethodID");

            entity.Property(e => e.DeliveryRun).HasMaxLength(5);

            entity.Property(e => e.InvoiceDate).HasColumnType("date");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.OrderId).HasColumnName("OrderID");

            entity.Property(e => e.PackedByPersonId).HasColumnName("PackedByPersonID");

            entity.Property(e => e.RunPosition).HasMaxLength(5);

            entity.Property(e => e.SalespersonPersonId).HasColumnName("SalespersonPersonID");

            entity.HasOne(d => d.AccountsPerson)
                .WithMany(p => p.InvoicesAccountsPerson)
                .HasForeignKey(d => d.AccountsPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_AccountsPersonID_Application_People");

            entity.HasOne(d => d.BillToCustomer)
                .WithMany(p => p.InvoicesBillToCustomer)
                .HasForeignKey(d => d.BillToCustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_BillToCustomerID_Sales_Customers");

            entity.HasOne(d => d.ContactPerson)
                .WithMany(p => p.InvoicesContactPerson)
                .HasForeignKey(d => d.ContactPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_ContactPersonID_Application_People");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.InvoicesCustomer)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_CustomerID_Sales_Customers");

            entity.HasOne(d => d.DeliveryMethod)
                .WithMany(p => p.Invoices)
                .HasForeignKey(d => d.DeliveryMethodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_DeliveryMethodID_Application_DeliveryMethods");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.InvoicesLastEditedByNavigation)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_Application_People");

            entity.HasOne(d => d.Order)
                .WithMany(p => p.Invoices)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_Sales_Invoices_OrderID_Sales_Orders");

            entity.HasOne(d => d.PackedByPerson)
                .WithMany(p => p.InvoicesPackedByPerson)
                .HasForeignKey(d => d.PackedByPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_PackedByPersonID_Application_People");

            entity.HasOne(d => d.SalespersonPerson)
                .WithMany(p => p.InvoicesSalespersonPerson)
                .HasForeignKey(d => d.SalespersonPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Invoices_SalespersonPersonID_Application_People");
        });

        modelBuilder.Entity<OrderLines>(entity =>
        {
            entity.HasKey(e => e.OrderLineId)
                .HasName("PK_Sales_OrderLines");

            entity.ToTable("OrderLines", "Sales");

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("FK_Sales_OrderLines_OrderID");

            entity.HasIndex(e => e.PackageTypeId)
                .HasDatabaseName("FK_Sales_OrderLines_PackageTypeID");

            entity.HasIndex(e => new { e.PickedQuantity, e.StockItemId })
                .HasDatabaseName("IX_Sales_OrderLines_AllocatedStockItems");

            entity.HasIndex(e => new { e.OrderId, e.PickedQuantity, e.StockItemId, e.PickingCompletedWhen })
                .HasDatabaseName("IX_Sales_OrderLines_Perf_20160301_02");

            entity.HasIndex(e => new { e.Quantity, e.StockItemId, e.PickingCompletedWhen, e.OrderId, e.OrderLineId })
                .HasDatabaseName("IX_Sales_OrderLines_Perf_20160301_01");

            entity.Property(e => e.OrderLineId)
                .HasColumnName("OrderLineID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[OrderLineID])");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.OrderId).HasColumnName("OrderID");

            entity.Property(e => e.PackageTypeId).HasColumnName("PackageTypeID");

            entity.Property(e => e.StockItemId).HasColumnName("StockItemID");

            entity.Property(e => e.TaxRate).HasColumnType("decimal(18, 3)");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_OrderLines_Application_People");

            entity.HasOne(d => d.Order)
                .WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_OrderLines_OrderID_Sales_Orders");

            entity.HasOne(d => d.PackageType)
                .WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.PackageTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_OrderLines_PackageTypeID_Warehouse_PackageTypes");

            entity.HasOne(d => d.StockItem)
                .WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.StockItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_OrderLines_StockItemID_Warehouse_StockItems");
        });

        modelBuilder.Entity<Orders>(entity =>
        {
            entity.HasKey(e => e.OrderId)
                .HasName("PK_Sales_Orders");

            entity.ToTable("Orders", "Sales");

            entity.HasIndex(e => e.ContactPersonId)
                .HasDatabaseName("FK_Sales_Orders_ContactPersonID");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("FK_Sales_Orders_CustomerID");

            entity.HasIndex(e => e.PickedByPersonId)
                .HasDatabaseName("FK_Sales_Orders_PickedByPersonID");

            entity.HasIndex(e => e.SalespersonPersonId)
                .HasDatabaseName("FK_Sales_Orders_SalespersonPersonID");

            entity.Property(e => e.OrderId)
                .HasColumnName("OrderID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[OrderID])");

            entity.Property(e => e.BackorderOrderId).HasColumnName("BackorderOrderID");

            entity.Property(e => e.ContactPersonId).HasColumnName("ContactPersonID");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

            entity.Property(e => e.CustomerPurchaseOrderNumber).HasMaxLength(20);

            entity.Property(e => e.ExpectedDeliveryDate).HasColumnType("date");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.OrderDate).HasColumnType("date");

            entity.Property(e => e.PickedByPersonId).HasColumnName("PickedByPersonID");

            entity.Property(e => e.SalespersonPersonId).HasColumnName("SalespersonPersonID");

            entity.HasOne(d => d.BackorderOrder)
                .WithMany(p => p.InverseBackorderOrder)
                .HasForeignKey(d => d.BackorderOrderId)
                .HasConstraintName("FK_Sales_Orders_BackorderOrderID_Sales_Orders");

            entity.HasOne(d => d.ContactPerson)
                .WithMany(p => p.OrdersContactPerson)
                .HasForeignKey(d => d.ContactPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Orders_ContactPersonID_Application_People");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Orders_CustomerID_Sales_Customers");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.OrdersLastEditedByNavigation)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Orders_Application_People");

            entity.HasOne(d => d.PickedByPerson)
                .WithMany(p => p.OrdersPickedByPerson)
                .HasForeignKey(d => d.PickedByPersonId)
                .HasConstraintName("FK_Sales_Orders_PickedByPersonID_Application_People");

            entity.HasOne(d => d.SalespersonPerson)
                .WithMany(p => p.OrdersSalespersonPerson)
                .HasForeignKey(d => d.SalespersonPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_Orders_SalespersonPersonID_Application_People");
        });

        modelBuilder.Entity<PackageTypes>(entity =>
        {
            entity.HasKey(e => e.PackageTypeId)
                .HasName("PK_Warehouse_PackageTypes");

            entity.ToTable("PackageTypes", "Warehouse");

            entity.HasIndex(e => e.PackageTypeName)
                .HasDatabaseName("UQ_Warehouse_PackageTypes_PackageTypeName")
                .IsUnique();

            entity.Property(e => e.PackageTypeId)
                .HasColumnName("PackageTypeID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[PackageTypeID])");

            entity.Property(e => e.PackageTypeName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.PackageTypes)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_PackageTypes_Application_People");
        });

        modelBuilder.Entity<PaymentMethods>(entity =>
        {
            entity.HasKey(e => e.PaymentMethodId)
                .HasName("PK_Application_PaymentMethods");

            entity.ToTable("PaymentMethods", "Application");

            entity.HasIndex(e => e.PaymentMethodName)
                .HasDatabaseName("UQ_Application_PaymentMethods_PaymentMethodName")
                .IsUnique();

            entity.Property(e => e.PaymentMethodId)
                .HasColumnName("PaymentMethodID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[PaymentMethodID])");

            entity.Property(e => e.PaymentMethodName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.PaymentMethods)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_PaymentMethods_Application_People");
        });

        modelBuilder.Entity<People>(entity =>
        {
            entity.HasKey(e => e.PersonId)
                .HasName("PK_Application_People");

            entity.ToTable("People", "Application");

            entity.HasIndex(e => e.FullName)
                .HasDatabaseName("IX_Application_People_FullName");

            entity.HasIndex(e => e.IsEmployee)
                .HasDatabaseName("IX_Application_People_IsEmployee");

            entity.HasIndex(e => e.IsSalesperson)
                .HasDatabaseName("IX_Application_People_IsSalesperson");

            entity.HasIndex(e => new { e.FullName, e.EmailAddress, e.IsPermittedToLogon, e.PersonId })
                .HasDatabaseName("IX_Application_People_Perf_20160301_05");

            entity.Property(e => e.PersonId)
                .HasColumnName("PersonID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[PersonID])");

            entity.Property(e => e.EmailAddress).HasMaxLength(256);

            entity.Property(e => e.FaxNumber).HasMaxLength(20);

            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.LogonName).HasMaxLength(50);

            entity.Property(e => e.OtherLanguages)
                .HasComputedColumnSql("(json_query([CustomFields],N'$.OtherLanguages'))");

            entity.Property(e => e.PhoneNumber).HasMaxLength(20);

            entity.Property(e => e.PreferredName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.SearchName)
                .IsRequired()
                .HasMaxLength(101)
                .HasComputedColumnSql("(concat([PreferredName],N' ',[FullName]))");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.InverseLastEditedByNavigation)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_People_Application_People");
        });

        modelBuilder.Entity<PurchaseOrderLines>(entity =>
        {
            entity.HasKey(e => e.PurchaseOrderLineId)
                .HasName("PK_Purchasing_PurchaseOrderLines");

            entity.ToTable("PurchaseOrderLines", "Purchasing");

            entity.HasIndex(e => e.PackageTypeId)
                .HasDatabaseName("FK_Purchasing_PurchaseOrderLines_PackageTypeID");

            entity.HasIndex(e => e.PurchaseOrderId)
                .HasDatabaseName("FK_Purchasing_PurchaseOrderLines_PurchaseOrderID");

            entity.HasIndex(e => e.StockItemId)
                .HasDatabaseName("FK_Purchasing_PurchaseOrderLines_StockItemID");

            entity.HasIndex(e => new { e.OrderedOuters, e.ReceivedOuters, e.IsOrderLineFinalized, e.StockItemId })
                .HasDatabaseName("IX_Purchasing_PurchaseOrderLines_Perf_20160301_4");

            entity.Property(e => e.PurchaseOrderLineId)
                .HasColumnName("PurchaseOrderLineID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[PurchaseOrderLineID])");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ExpectedUnitPricePerOuter).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.LastReceiptDate).HasColumnType("date");

            entity.Property(e => e.PackageTypeId).HasColumnName("PackageTypeID");

            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");

            entity.Property(e => e.StockItemId).HasColumnName("StockItemID");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrderLines_Application_People");

            entity.HasOne(d => d.PackageType)
                .WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.PackageTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrderLines_PackageTypeID_Warehouse_PackageTypes");

            entity.HasOne(d => d.PurchaseOrder)
                .WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrderLines_PurchaseOrderID_Purchasing_PurchaseOrders");

            entity.HasOne(d => d.StockItem)
                .WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.StockItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrderLines_StockItemID_Warehouse_StockItems");
        });

        modelBuilder.Entity<PurchaseOrders>(entity =>
        {
            entity.HasKey(e => e.PurchaseOrderId)
                .HasName("PK_Purchasing_PurchaseOrders");

            entity.ToTable("PurchaseOrders", "Purchasing");

            entity.HasIndex(e => e.ContactPersonId)
                .HasDatabaseName("FK_Purchasing_PurchaseOrders_ContactPersonID");

            entity.HasIndex(e => e.DeliveryMethodId)
                .HasDatabaseName("FK_Purchasing_PurchaseOrders_DeliveryMethodID");

            entity.HasIndex(e => e.SupplierId)
                .HasDatabaseName("FK_Purchasing_PurchaseOrders_SupplierID");

            entity.Property(e => e.PurchaseOrderId)
                .HasColumnName("PurchaseOrderID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[PurchaseOrderID])");

            entity.Property(e => e.ContactPersonId).HasColumnName("ContactPersonID");

            entity.Property(e => e.DeliveryMethodId).HasColumnName("DeliveryMethodID");

            entity.Property(e => e.ExpectedDeliveryDate).HasColumnType("date");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.OrderDate).HasColumnType("date");

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");

            entity.Property(e => e.SupplierReference).HasMaxLength(20);

            entity.HasOne(d => d.ContactPerson)
                .WithMany(p => p.PurchaseOrdersContactPerson)
                .HasForeignKey(d => d.ContactPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrders_ContactPersonID_Application_People");

            entity.HasOne(d => d.DeliveryMethod)
                .WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.DeliveryMethodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrders_DeliveryMethodID_Application_DeliveryMethods");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.PurchaseOrdersLastEditedByNavigation)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrders_Application_People");

            entity.HasOne(d => d.Supplier)
                .WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_PurchaseOrders_SupplierID_Purchasing_Suppliers");
        });

        modelBuilder.Entity<SpecialDeals>(entity =>
        {
            entity.HasKey(e => e.SpecialDealId)
                .HasName("PK_Sales_SpecialDeals");

            entity.ToTable("SpecialDeals", "Sales");

            entity.HasIndex(e => e.BuyingGroupId)
                .HasDatabaseName("FK_Sales_SpecialDeals_BuyingGroupID");

            entity.HasIndex(e => e.CustomerCategoryId)
                .HasDatabaseName("FK_Sales_SpecialDeals_CustomerCategoryID");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("FK_Sales_SpecialDeals_CustomerID");

            entity.HasIndex(e => e.StockGroupId)
                .HasDatabaseName("FK_Sales_SpecialDeals_StockGroupID");

            entity.HasIndex(e => e.StockItemId)
                .HasDatabaseName("FK_Sales_SpecialDeals_StockItemID");

            entity.Property(e => e.SpecialDealId)
                .HasColumnName("SpecialDealID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[SpecialDealID])");

            entity.Property(e => e.BuyingGroupId).HasColumnName("BuyingGroupID");

            entity.Property(e => e.CustomerCategoryId).HasColumnName("CustomerCategoryID");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

            entity.Property(e => e.DealDescription)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(18, 3)");

            entity.Property(e => e.EndDate).HasColumnType("date");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.StartDate).HasColumnType("date");

            entity.Property(e => e.StockGroupId).HasColumnName("StockGroupID");

            entity.Property(e => e.StockItemId).HasColumnName("StockItemID");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.BuyingGroup)
                .WithMany(p => p.SpecialDeals)
                .HasForeignKey(d => d.BuyingGroupId)
                .HasConstraintName("FK_Sales_SpecialDeals_BuyingGroupID_Sales_BuyingGroups");

            entity.HasOne(d => d.CustomerCategory)
                .WithMany(p => p.SpecialDeals)
                .HasForeignKey(d => d.CustomerCategoryId)
                .HasConstraintName("FK_Sales_SpecialDeals_CustomerCategoryID_Sales_CustomerCategories");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.SpecialDeals)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_Sales_SpecialDeals_CustomerID_Sales_Customers");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.SpecialDeals)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sales_SpecialDeals_Application_People");

            entity.HasOne(d => d.StockGroup)
                .WithMany(p => p.SpecialDeals)
                .HasForeignKey(d => d.StockGroupId)
                .HasConstraintName("FK_Sales_SpecialDeals_StockGroupID_Warehouse_StockGroups");

            entity.HasOne(d => d.StockItem)
                .WithMany(p => p.SpecialDeals)
                .HasForeignKey(d => d.StockItemId)
                .HasConstraintName("FK_Sales_SpecialDeals_StockItemID_Warehouse_StockItems");
        });

        modelBuilder.Entity<StateProvinces>(entity =>
        {
            entity.HasKey(e => e.StateProvinceId)
                .HasName("PK_Application_StateProvinces");

            entity.ToTable("StateProvinces", "Application");

            entity.HasIndex(e => e.CountryId)
                .HasDatabaseName("FK_Application_StateProvinces_CountryID");

            entity.HasIndex(e => e.SalesTerritory)
                .HasDatabaseName("IX_Application_StateProvinces_SalesTerritory");

            entity.HasIndex(e => e.StateProvinceName)
                .HasDatabaseName("UQ_Application_StateProvinces_StateProvinceName")
                .IsUnique();

            entity.Property(e => e.StateProvinceId)
                .HasColumnName("StateProvinceID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[StateProvinceID])");

            entity.Property(e => e.CountryId).HasColumnName("CountryID");

            entity.Property(e => e.SalesTerritory)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.StateProvinceCode)
                .IsRequired()
                .HasMaxLength(5);

            entity.Property(e => e.StateProvinceName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.Country)
                .WithMany(p => p.StateProvinces)
                .HasForeignKey(d => d.CountryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_StateProvinces_CountryID_Application_Countries");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.StateProvinces)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_StateProvinces_Application_People");
        });

        modelBuilder.Entity<StockGroups>(entity =>
        {
            entity.HasKey(e => e.StockGroupId)
                .HasName("PK_Warehouse_StockGroups");

            entity.ToTable("StockGroups", "Warehouse");

            entity.HasIndex(e => e.StockGroupName)
                .HasDatabaseName("UQ_Warehouse_StockGroups_StockGroupName")
                .IsUnique();

            entity.Property(e => e.StockGroupId)
                .HasColumnName("StockGroupID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[StockGroupID])");

            entity.Property(e => e.StockGroupName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.StockGroups)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockGroups_Application_People");
        });

        modelBuilder.Entity<StockItemHoldings>(entity =>
        {
            entity.HasKey(e => e.StockItemId)
                .HasName("PK_Warehouse_StockItemHoldings");

            entity.ToTable("StockItemHoldings", "Warehouse");

            entity.Property(e => e.StockItemId)
                .HasColumnName("StockItemID")
                .ValueGeneratedNever();

            entity.Property(e => e.BinLocation)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.LastCostPrice).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.StockItemHoldings)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItemHoldings_Application_People");

            entity.HasOne(d => d.StockItem)
                .WithOne(p => p.StockItemHoldings)
                .HasForeignKey<StockItemHoldings>(d => d.StockItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("PKFK_Warehouse_StockItemHoldings_StockItemID_Warehouse_StockItems");
        });

        modelBuilder.Entity<StockItemStockGroups>(entity =>
        {
            entity.HasKey(e => e.StockItemStockGroupId)
                .HasName("PK_Warehouse_StockItemStockGroups");

            entity.ToTable("StockItemStockGroups", "Warehouse");

            entity.HasIndex(e => new { e.StockGroupId, e.StockItemId })
                .HasDatabaseName("UQ_StockItemStockGroups_StockGroupID_Lookup")
                .IsUnique();

            entity.HasIndex(e => new { e.StockItemId, e.StockGroupId })
                .HasDatabaseName("UQ_StockItemStockGroups_StockItemID_Lookup")
                .IsUnique();

            entity.Property(e => e.StockItemStockGroupId)
                .HasColumnName("StockItemStockGroupID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[StockItemStockGroupID])");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.StockGroupId).HasColumnName("StockGroupID");

            entity.Property(e => e.StockItemId).HasColumnName("StockItemID");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.StockItemStockGroups)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItemStockGroups_Application_People");

            entity.HasOne(d => d.StockGroup)
                .WithMany(p => p.StockItemStockGroups)
                .HasForeignKey(d => d.StockGroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItemStockGroups_StockGroupID_Warehouse_StockGroups");

            entity.HasOne(d => d.StockItem)
                .WithMany(p => p.StockItemStockGroups)
                .HasForeignKey(d => d.StockItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItemStockGroups_StockItemID_Warehouse_StockItems");
        });

        modelBuilder.Entity<StockItemTransactions>(entity =>
        {
            entity.HasKey(e => e.StockItemTransactionId)
                .HasName("PK_Warehouse_StockItemTransactions");

            entity.ToTable("StockItemTransactions", "Warehouse");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("FK_Warehouse_StockItemTransactions_CustomerID");

            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("FK_Warehouse_StockItemTransactions_InvoiceID");

            entity.HasIndex(e => e.PurchaseOrderId)
                .HasDatabaseName("FK_Warehouse_StockItemTransactions_PurchaseOrderID");

            entity.HasIndex(e => e.StockItemId)
                .HasDatabaseName("FK_Warehouse_StockItemTransactions_StockItemID");

            entity.HasIndex(e => e.SupplierId)
                .HasDatabaseName("FK_Warehouse_StockItemTransactions_SupplierID");

            entity.HasIndex(e => e.TransactionTypeId)
                .HasDatabaseName("FK_Warehouse_StockItemTransactions_TransactionTypeID");

            entity.Property(e => e.StockItemTransactionId)
                .HasColumnName("StockItemTransactionID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[TransactionID])");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");

            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");

            entity.Property(e => e.StockItemId).HasColumnName("StockItemID");

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");

            entity.Property(e => e.TransactionTypeId).HasColumnName("TransactionTypeID");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.StockItemTransactions)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_Warehouse_StockItemTransactions_CustomerID_Sales_Customers");

            entity.HasOne(d => d.Invoice)
                .WithMany(p => p.StockItemTransactions)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK_Warehouse_StockItemTransactions_InvoiceID_Sales_Invoices");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.StockItemTransactions)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItemTransactions_Application_People");

            entity.HasOne(d => d.PurchaseOrder)
                .WithMany(p => p.StockItemTransactions)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("FK_Warehouse_StockItemTransactions_PurchaseOrderID_Purchasing_PurchaseOrders");

            entity.HasOne(d => d.StockItem)
                .WithMany(p => p.StockItemTransactions)
                .HasForeignKey(d => d.StockItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItemTransactions_StockItemID_Warehouse_StockItems");

            entity.HasOne(d => d.Supplier)
                .WithMany(p => p.StockItemTransactions)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_Warehouse_StockItemTransactions_SupplierID_Purchasing_Suppliers");

            entity.HasOne(d => d.TransactionType)
                .WithMany(p => p.StockItemTransactions)
                .HasForeignKey(d => d.TransactionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName(
                    "FK_Warehouse_StockItemTransactions_TransactionTypeID_Application_TransactionTypes");
        });

        modelBuilder.Entity<StockItems>(entity =>
        {
            entity.HasKey(e => e.StockItemId)
                .HasName("PK_Warehouse_StockItems");

            entity.ToTable("StockItems", "Warehouse");

            entity.HasIndex(e => e.ColorId)
                .HasDatabaseName("FK_Warehouse_StockItems_ColorID");

            entity.HasIndex(e => e.OuterPackageId)
                .HasDatabaseName("FK_Warehouse_StockItems_OuterPackageID");

            entity.HasIndex(e => e.StockItemName)
                .HasDatabaseName("UQ_Warehouse_StockItems_StockItemName")
                .IsUnique();

            entity.HasIndex(e => e.SupplierId)
                .HasDatabaseName("FK_Warehouse_StockItems_SupplierID");

            entity.HasIndex(e => e.UnitPackageId)
                .HasDatabaseName("FK_Warehouse_StockItems_UnitPackageID");

            entity.Property(e => e.StockItemId)
                .HasColumnName("StockItemID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[StockItemID])");

            entity.Property(e => e.Barcode).HasMaxLength(50);

            entity.Property(e => e.Brand).HasMaxLength(50);

            entity.Property(e => e.ColorId).HasColumnName("ColorID");

            entity.Property(e => e.OuterPackageId).HasColumnName("OuterPackageID");

            entity.Property(e => e.RecommendedRetailPrice).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.SearchDetails)
                .IsRequired()
                .HasComputedColumnSql("(concat([StockItemName],N' ',[MarketingComments]))");

            entity.Property(e => e.Size).HasMaxLength(20);

            entity.Property(e => e.StockItemName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");

            entity.Property(e => e.Tags).HasComputedColumnSql("(json_query([CustomFields],N'$.Tags'))");

            entity.Property(e => e.TaxRate).HasColumnType("decimal(18, 3)");

            entity.Property(e => e.TypicalWeightPerUnit).HasColumnType("decimal(18, 3)");

            entity.Property(e => e.UnitPackageId).HasColumnName("UnitPackageID");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Color)
                .WithMany(p => p.StockItems)
                .HasForeignKey(d => d.ColorId)
                .HasConstraintName("FK_Warehouse_StockItems_ColorID_Warehouse_Colors");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.StockItems)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItems_Application_People");

            entity.HasOne(d => d.OuterPackage)
                .WithMany(p => p.StockItemsOuterPackage)
                .HasForeignKey(d => d.OuterPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItems_OuterPackageID_Warehouse_PackageTypes");

            entity.HasOne(d => d.Supplier)
                .WithMany(p => p.StockItems)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItems_SupplierID_Purchasing_Suppliers");

            entity.HasOne(d => d.UnitPackage)
                .WithMany(p => p.StockItemsUnitPackage)
                .HasForeignKey(d => d.UnitPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehouse_StockItems_UnitPackageID_Warehouse_PackageTypes");
        });

        modelBuilder.Entity<SupplierCategories>(entity =>
        {
            entity.HasKey(e => e.SupplierCategoryId)
                .HasName("PK_Purchasing_SupplierCategories");

            entity.ToTable("SupplierCategories", "Purchasing");

            entity.HasIndex(e => e.SupplierCategoryName)
                .HasDatabaseName("UQ_Purchasing_SupplierCategories_SupplierCategoryName")
                .IsUnique();

            entity.Property(e => e.SupplierCategoryId)
                .HasColumnName("SupplierCategoryID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[SupplierCategoryID])");

            entity.Property(e => e.SupplierCategoryName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.SupplierCategories)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_SupplierCategories_Application_People");
        });

        modelBuilder.Entity<SupplierTransactions>(entity =>
        {
            entity.HasKey(e => e.SupplierTransactionId)
                .HasName("PK_Purchasing_SupplierTransactions");

            entity.ToTable("SupplierTransactions", "Purchasing");

            entity.HasIndex(e => e.IsFinalized)
                .HasDatabaseName("IX_Purchasing_SupplierTransactions_IsFinalized");

            entity.HasIndex(e => e.PaymentMethodId)
                .HasDatabaseName("FK_Purchasing_SupplierTransactions_PaymentMethodID");

            entity.HasIndex(e => e.PurchaseOrderId)
                .HasDatabaseName("FK_Purchasing_SupplierTransactions_PurchaseOrderID");

            entity.HasIndex(e => e.SupplierId)
                .HasDatabaseName("FK_Purchasing_SupplierTransactions_SupplierID");

            entity.HasIndex(e => e.TransactionTypeId)
                .HasDatabaseName("FK_Purchasing_SupplierTransactions_TransactionTypeID");

            entity.Property(e => e.SupplierTransactionId)
                .HasColumnName("SupplierTransactionID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[TransactionID])");

            entity.Property(e => e.AmountExcludingTax).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.FinalizationDate).HasColumnType("date");

            entity.Property(e => e.IsFinalized).HasComputedColumnSql(
                "(case when [FinalizationDate] IS NULL then CONVERT([bit],(0)) else CONVERT([bit],(1)) end)");

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.OutstandingBalance).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.PaymentMethodId).HasColumnName("PaymentMethodID");

            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");

            entity.Property(e => e.SupplierInvoiceNumber).HasMaxLength(20);

            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18, 2)");

            entity.Property(e => e.TransactionDate).HasColumnType("date");

            entity.Property(e => e.TransactionTypeId).HasColumnName("TransactionTypeID");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.SupplierTransactions)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_SupplierTransactions_Application_People");

            entity.HasOne(d => d.PaymentMethod)
                .WithMany(p => p.SupplierTransactions)
                .HasForeignKey(d => d.PaymentMethodId)
                .HasConstraintName("FK_Purchasing_SupplierTransactions_PaymentMethodID_Application_PaymentMethods");

            entity.HasOne(d => d.PurchaseOrder)
                .WithMany(p => p.SupplierTransactions)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("FK_Purchasing_SupplierTransactions_PurchaseOrderID_Purchasing_PurchaseOrders");

            entity.HasOne(d => d.Supplier)
                .WithMany(p => p.SupplierTransactions)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_SupplierTransactions_SupplierID_Purchasing_Suppliers");

            entity.HasOne(d => d.TransactionType)
                .WithMany(p => p.SupplierTransactions)
                .HasForeignKey(d => d.TransactionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName(
                    "FK_Purchasing_SupplierTransactions_TransactionTypeID_Application_TransactionTypes");
        });

        modelBuilder.Entity<Suppliers>(entity =>
        {
            entity.HasKey(e => e.SupplierId)
                .HasName("PK_Purchasing_Suppliers");

            entity.ToTable("Suppliers", "Purchasing");

            entity.HasIndex(e => e.AlternateContactPersonId)
                .HasDatabaseName("FK_Purchasing_Suppliers_AlternateContactPersonID");

            entity.HasIndex(e => e.DeliveryCityId)
                .HasDatabaseName("FK_Purchasing_Suppliers_DeliveryCityID");

            entity.HasIndex(e => e.DeliveryMethodId)
                .HasDatabaseName("FK_Purchasing_Suppliers_DeliveryMethodID");

            entity.HasIndex(e => e.PostalCityId)
                .HasDatabaseName("FK_Purchasing_Suppliers_PostalCityID");

            entity.HasIndex(e => e.PrimaryContactPersonId)
                .HasDatabaseName("FK_Purchasing_Suppliers_PrimaryContactPersonID");

            entity.HasIndex(e => e.SupplierCategoryId)
                .HasDatabaseName("FK_Purchasing_Suppliers_SupplierCategoryID");

            entity.HasIndex(e => e.SupplierName)
                .HasDatabaseName("UQ_Purchasing_Suppliers_SupplierName")
                .IsUnique();

            entity.Property(e => e.SupplierId)
                .HasColumnName("SupplierID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[SupplierID])");

            entity.Property(e => e.AlternateContactPersonId).HasColumnName("AlternateContactPersonID");

            entity.Property(e => e.BankAccountBranch).HasMaxLength(50);

            entity.Property(e => e.BankAccountCode).HasMaxLength(20);

            entity.Property(e => e.BankAccountName).HasMaxLength(50);

            entity.Property(e => e.BankAccountNumber).HasMaxLength(20);

            entity.Property(e => e.BankInternationalCode).HasMaxLength(20);

            entity.Property(e => e.DeliveryAddressLine1)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.DeliveryAddressLine2).HasMaxLength(60);

            entity.Property(e => e.DeliveryCityId).HasColumnName("DeliveryCityID");

            entity.Property(e => e.DeliveryMethodId).HasColumnName("DeliveryMethodID");

            entity.Property(e => e.DeliveryPostalCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.FaxNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.PostalAddressLine1)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.PostalAddressLine2).HasMaxLength(60);

            entity.Property(e => e.PostalCityId).HasColumnName("PostalCityID");

            entity.Property(e => e.PostalPostalCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.PrimaryContactPersonId).HasColumnName("PrimaryContactPersonID");

            entity.Property(e => e.SupplierCategoryId).HasColumnName("SupplierCategoryID");

            entity.Property(e => e.SupplierName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.SupplierReference).HasMaxLength(20);

            entity.Property(e => e.WebsiteUrl)
                .IsRequired()
                .HasColumnName("WebsiteURL")
                .HasMaxLength(256);

            entity.HasOne(d => d.AlternateContactPerson)
                .WithMany(p => p.SuppliersAlternateContactPerson)
                .HasForeignKey(d => d.AlternateContactPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_Suppliers_AlternateContactPersonID_Application_People");

            entity.HasOne(d => d.DeliveryCity)
                .WithMany(p => p.SuppliersDeliveryCity)
                .HasForeignKey(d => d.DeliveryCityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_Suppliers_DeliveryCityID_Application_Cities");

            entity.HasOne(d => d.DeliveryMethod)
                .WithMany(p => p.Suppliers)
                .HasForeignKey(d => d.DeliveryMethodId)
                .HasConstraintName("FK_Purchasing_Suppliers_DeliveryMethodID_Application_DeliveryMethods");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.SuppliersLastEditedByNavigation)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_Suppliers_Application_People");

            entity.HasOne(d => d.PostalCity)
                .WithMany(p => p.SuppliersPostalCity)
                .HasForeignKey(d => d.PostalCityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_Suppliers_PostalCityID_Application_Cities");

            entity.HasOne(d => d.PrimaryContactPerson)
                .WithMany(p => p.SuppliersPrimaryContactPerson)
                .HasForeignKey(d => d.PrimaryContactPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_Suppliers_PrimaryContactPersonID_Application_People");

            entity.HasOne(d => d.SupplierCategory)
                .WithMany(p => p.Suppliers)
                .HasForeignKey(d => d.SupplierCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasing_Suppliers_SupplierCategoryID_Purchasing_SupplierCategories");
        });

        modelBuilder.Entity<SystemParameters>(entity =>
        {
            entity.HasKey(e => e.SystemParameterId)
                .HasName("PK_Application_SystemParameters");

            entity.ToTable("SystemParameters", "Application");

            entity.HasIndex(e => e.DeliveryCityId)
                .HasDatabaseName("FK_Application_SystemParameters_DeliveryCityID");

            entity.HasIndex(e => e.PostalCityId)
                .HasDatabaseName("FK_Application_SystemParameters_PostalCityID");

            entity.Property(e => e.SystemParameterId)
                .HasColumnName("SystemParameterID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[SystemParameterID])");

            entity.Property(e => e.ApplicationSettings).IsRequired();

            entity.Property(e => e.DeliveryAddressLine1)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.DeliveryAddressLine2).HasMaxLength(60);

            entity.Property(e => e.DeliveryCityId).HasColumnName("DeliveryCityID");

            entity.Property(e => e.DeliveryPostalCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.LastEditedWhen).HasDefaultValueSql("(sysdatetime())");

            entity.Property(e => e.PostalAddressLine1)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.PostalAddressLine2).HasMaxLength(60);

            entity.Property(e => e.PostalCityId).HasColumnName("PostalCityID");

            entity.Property(e => e.PostalPostalCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.HasOne(d => d.DeliveryCity)
                .WithMany(p => p.SystemParametersDeliveryCity)
                .HasForeignKey(d => d.DeliveryCityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_SystemParameters_DeliveryCityID_Application_Cities");

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.SystemParameters)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_SystemParameters_Application_People");

            entity.HasOne(d => d.PostalCity)
                .WithMany(p => p.SystemParametersPostalCity)
                .HasForeignKey(d => d.PostalCityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_SystemParameters_PostalCityID_Application_Cities");
        });

        modelBuilder.Entity<TransactionTypes>(entity =>
        {
            entity.HasKey(e => e.TransactionTypeId)
                .HasName("PK_Application_TransactionTypes");

            entity.ToTable("TransactionTypes", "Application");

            entity.HasIndex(e => e.TransactionTypeName)
                .HasDatabaseName("UQ_Application_TransactionTypes_TransactionTypeName")
                .IsUnique();

            entity.Property(e => e.TransactionTypeId)
                .HasColumnName("TransactionTypeID")
                .HasDefaultValueSql("(NEXT VALUE FOR [Sequences].[TransactionTypeID])");

            entity.Property(e => e.TransactionTypeName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.LastEditedByNavigation)
                .WithMany(p => p.TransactionTypes)
                .HasForeignKey(d => d.LastEditedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_TransactionTypes_Application_People");
        });

        modelBuilder.Entity<VehicleTemperatures>(entity =>
        {
            entity.HasKey(e => e.VehicleTemperatureId)
                .HasName("PK_Warehouse_VehicleTemperatures");

            entity.ToTable("VehicleTemperatures", "Warehouse");

            entity.Property(e => e.VehicleTemperatureId).HasColumnName("VehicleTemperatureID");

            entity.Property(e => e.FullSensorData).HasMaxLength(1000);

            entity.Property(e => e.Temperature).HasColumnType("decimal(10, 2)");

            entity.Property(e => e.VehicleRegistration)
                .IsRequired()
                .HasMaxLength(20);
        });

        modelBuilder.HasSequence<int>("BuyingGroupID").StartsAt(3);

        modelBuilder.HasSequence<int>("CityID").StartsAt(38187);

        modelBuilder.HasSequence<int>("ColorID").StartsAt(37);

        modelBuilder.HasSequence<int>("CountryID").StartsAt(242);

        modelBuilder.HasSequence<int>("CustomerCategoryID").StartsAt(9);

        modelBuilder.HasSequence<int>("CustomerID").StartsAt(1062);

        modelBuilder.HasSequence<int>("DeliveryMethodID").StartsAt(11);

        modelBuilder.HasSequence<int>("InvoiceID").StartsAt(70511);

        modelBuilder.HasSequence<int>("InvoiceLineID").StartsAt(228266);

        modelBuilder.HasSequence<int>("OrderID").StartsAt(73596);

        modelBuilder.HasSequence<int>("OrderLineID").StartsAt(231413);

        modelBuilder.HasSequence<int>("PackageTypeID").StartsAt(15);

        modelBuilder.HasSequence<int>("PaymentMethodID").StartsAt(5);

        modelBuilder.HasSequence<int>("PersonID").StartsAt(3262);

        modelBuilder.HasSequence<int>("PurchaseOrderID").StartsAt(2075);

        modelBuilder.HasSequence<int>("PurchaseOrderLineID").StartsAt(8368);

        modelBuilder.HasSequence<int>("SpecialDealID").StartsAt(3);

        modelBuilder.HasSequence<int>("StateProvinceID").StartsAt(54);

        modelBuilder.HasSequence<int>("StockGroupID").StartsAt(11);

        modelBuilder.HasSequence<int>("StockItemID").StartsAt(228);

        modelBuilder.HasSequence<int>("StockItemStockGroupID").StartsAt(443);

        modelBuilder.HasSequence<int>("SupplierCategoryID").StartsAt(10);

        modelBuilder.HasSequence<int>("SupplierID").StartsAt(14);

        modelBuilder.HasSequence<int>("SystemParameterID").StartsAt(2);

        modelBuilder.HasSequence<int>("TransactionID").StartsAt(336253);

        modelBuilder.HasSequence<int>("TransactionTypeID").StartsAt(14);
    }
}