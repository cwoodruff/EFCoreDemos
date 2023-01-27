using System;
using System.Collections.Generic;

namespace spatial.Models;

public partial class CustomerCategories
{
    public CustomerCategories()
    {
        Customers = new HashSet<Customers>();
        SpecialDeals = new HashSet<SpecialDeals>();
    }

    public int CustomerCategoryId { get; set; }
    public string CustomerCategoryName { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual ICollection<Customers> Customers { get; set; }
    public virtual ICollection<SpecialDeals> SpecialDeals { get; set; }
}