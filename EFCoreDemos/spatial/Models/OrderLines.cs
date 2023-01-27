using System;

namespace spatial.Models;

public partial class OrderLines
{
    public int OrderLineId { get; set; }
    public int OrderId { get; set; }
    public int StockItemId { get; set; }
    public string Description { get; set; }
    public int PackageTypeId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public int PickedQuantity { get; set; }
    public DateTime? PickingCompletedWhen { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual Orders Order { get; set; }
    public virtual PackageTypes PackageType { get; set; }
    public virtual StockItems StockItem { get; set; }
}