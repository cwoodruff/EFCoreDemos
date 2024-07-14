﻿using System;

namespace lazy_loading.Model;

public class EmployeePayHistory
{
    public int BusinessEntityID { get; set; }
    public DateTime RateChangeDate { get; set; }
    public decimal Rate { get; set; }
    public byte PayFrequency { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Employee BusinessEntity { get; set; }
}