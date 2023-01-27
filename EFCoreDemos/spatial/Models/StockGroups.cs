using System;
using System.Collections.Generic;

namespace spatial.Models;

public partial class StockGroups
{
    public StockGroups()
    {
        SpecialDeals = new HashSet<SpecialDeals>();
        StockItemStockGroups = new HashSet<StockItemStockGroups>();
    }

    public int StockGroupId { get; set; }
    public string StockGroupName { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual ICollection<SpecialDeals> SpecialDeals { get; set; }
    public virtual ICollection<StockItemStockGroups> StockItemStockGroups { get; set; }
}