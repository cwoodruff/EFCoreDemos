using System;

namespace spatial.Models;

public partial class StockItemStockGroups
{
    public int StockItemStockGroupId { get; set; }
    public int StockItemId { get; set; }
    public int StockGroupId { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual StockGroups StockGroup { get; set; }
    public virtual StockItems StockItem { get; set; }
}