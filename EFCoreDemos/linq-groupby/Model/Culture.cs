using System;
using System.Collections.Generic;

namespace linq_groupby.Model;

public class Culture
{
    public Culture()
    {
        ProductModelProductDescriptionCulture = new HashSet<ProductModelProductDescriptionCulture>();
    }

    public string CultureID { get; set; }
    public string Name { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<ProductModelProductDescriptionCulture> ProductModelProductDescriptionCulture
    {
        get;
        set;
    }
}