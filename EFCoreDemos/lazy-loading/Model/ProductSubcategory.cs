﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class ProductSubcategory
{
    public ProductSubcategory()
    {
        Product = new HashSet<Product>();
    }

    public int ProductSubcategoryID { get; set; }
    public int ProductCategoryID { get; set; }
    public string Name { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<Product> Product { get; set; }
    public virtual ProductCategory ProductCategory { get; set; }
}