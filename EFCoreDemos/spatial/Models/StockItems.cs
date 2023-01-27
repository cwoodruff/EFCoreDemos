using System;
using System.Collections.Generic;

namespace spatial.Models;

public partial class StockItems
{
    public StockItems()
    {
        InvoiceLines = new HashSet<InvoiceLines>();
        OrderLines = new HashSet<OrderLines>();
        PurchaseOrderLines = new HashSet<PurchaseOrderLines>();
        SpecialDeals = new HashSet<SpecialDeals>();
        StockItemStockGroups = new HashSet<StockItemStockGroups>();
        StockItemTransactions = new HashSet<StockItemTransactions>();
    }

    public int StockItemId { get; set; }
    public string StockItemName { get; set; }
    public int SupplierId { get; set; }
    public int? ColorId { get; set; }
    public int UnitPackageId { get; set; }
    public int OuterPackageId { get; set; }
    public string Brand { get; set; }
    public string Size { get; set; }
    public int LeadTimeDays { get; set; }
    public int QuantityPerOuter { get; set; }
    public bool IsChillerStock { get; set; }
    public string Barcode { get; set; }
    public decimal TaxRate { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? RecommendedRetailPrice { get; set; }
    public decimal TypicalWeightPerUnit { get; set; }
    public string MarketingComments { get; set; }
    public string InternalComments { get; set; }
    public byte[] Photo { get; set; }
    public string CustomFields { get; set; }
    public string Tags { get; set; }
    public string SearchDetails { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public virtual Colors Color { get; set; }
    public virtual People LastEditedByNavigation { get; set; }
    public virtual PackageTypes OuterPackage { get; set; }
    public virtual Suppliers Supplier { get; set; }
    public virtual PackageTypes UnitPackage { get; set; }
    public virtual StockItemHoldings StockItemHoldings { get; set; }
    public virtual ICollection<InvoiceLines> InvoiceLines { get; set; }
    public virtual ICollection<OrderLines> OrderLines { get; set; }
    public virtual ICollection<PurchaseOrderLines> PurchaseOrderLines { get; set; }
    public virtual ICollection<SpecialDeals> SpecialDeals { get; set; }
    public virtual ICollection<StockItemStockGroups> StockItemStockGroups { get; set; }
    public virtual ICollection<StockItemTransactions> StockItemTransactions { get; set; }
}