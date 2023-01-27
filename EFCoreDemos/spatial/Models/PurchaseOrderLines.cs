using System;

namespace spatial.Models;

public partial class PurchaseOrderLines
{
    public int PurchaseOrderLineId { get; set; }
    public int PurchaseOrderId { get; set; }
    public int StockItemId { get; set; }
    public int OrderedOuters { get; set; }
    public string Description { get; set; }
    public int ReceivedOuters { get; set; }
    public int PackageTypeId { get; set; }
    public decimal? ExpectedUnitPricePerOuter { get; set; }
    public DateTime? LastReceiptDate { get; set; }
    public bool IsOrderLineFinalized { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual PackageTypes PackageType { get; set; }
    public virtual PurchaseOrders PurchaseOrder { get; set; }
    public virtual StockItems StockItem { get; set; }
}