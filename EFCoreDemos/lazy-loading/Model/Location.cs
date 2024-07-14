﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class Location
{
    public Location()
    {
        ProductInventory = new HashSet<ProductInventory>();
        WorkOrderRouting = new HashSet<WorkOrderRouting>();
    }

    public short LocationID { get; set; }
    public string Name { get; set; }
    public decimal CostRate { get; set; }
    public decimal Availability { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<ProductInventory> ProductInventory { get; set; }
    public virtual ICollection<WorkOrderRouting> WorkOrderRouting { get; set; }
}