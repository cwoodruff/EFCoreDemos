using System;

namespace spatial.Models;

public partial class InvoiceLines
{
    public int InvoiceLineId { get; set; }
    public int InvoiceId { get; set; }
    public int StockItemId { get; set; }
    public string Description { get; set; }
    public int PackageTypeId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineProfit { get; set; }
    public decimal ExtendedPrice { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual Invoices Invoice { get; set; }
    public virtual People LastEditedByNavigation { get; set; }
    public virtual PackageTypes PackageType { get; set; }
    public virtual StockItems StockItem { get; set; }
}