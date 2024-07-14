﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class ProductCategory
{
    public ProductCategory()
    {
        ProductSubcategory = new HashSet<ProductSubcategory>();
    }

    public int ProductCategoryID { get; set; }
    public string Name { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<ProductSubcategory> ProductSubcategory { get; set; }
}