using System;

namespace linq_groupby.Model;

public class ProductListPriceHistory
{
    public int ProductID { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal ListPrice { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Product Product { get; set; }
}