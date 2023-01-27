using System;

namespace spatial.Models;

public partial class StockItemHoldings
{
    public int StockItemId { get; set; }
    public int QuantityOnHand { get; set; }
    public string BinLocation { get; set; }
    public int LastStocktakeQuantity { get; set; }
    public decimal LastCostPrice { get; set; }
    public int ReorderLevel { get; set; }
    public int TargetStockLevel { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual StockItems StockItem { get; set; }
}