using System;

namespace spatial.Models;

public partial class SpecialDeals
{
    public int SpecialDealId { get; set; }
    public int? StockItemId { get; set; }
    public int? CustomerId { get; set; }
    public int? BuyingGroupId { get; set; }
    public int? CustomerCategoryId { get; set; }
    public int? StockGroupId { get; set; }
    public string DealDescription { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? UnitPrice { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual BuyingGroups BuyingGroup { get; set; }
    public virtual Customers Customer { get; set; }
    public virtual CustomerCategories CustomerCategory { get; set; }
    public virtual People LastEditedByNavigation { get; set; }
    public virtual StockGroups StockGroup { get; set; }
    public virtual StockItems StockItem { get; set; }
}