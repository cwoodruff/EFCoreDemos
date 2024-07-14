﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class ProductDescription
{
    public ProductDescription()
    {
        ProductModelProductDescriptionCulture = new HashSet<ProductModelProductDescriptionCulture>();
    }

    public int ProductDescriptionID { get; set; }
    public string Description { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<ProductModelProductDescriptionCulture> ProductModelProductDescriptionCulture
    {
        get;
        set;
    }
}