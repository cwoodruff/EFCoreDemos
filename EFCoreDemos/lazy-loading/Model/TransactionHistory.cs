﻿using System;

namespace lazy_loading.Model;

public class TransactionHistory
{
    public int TransactionID { get; set; }
    public int ProductID { get; set; }
    public int ReferenceOrderID { get; set; }
    public int ReferenceOrderLineID { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; }
    public int Quantity { get; set; }
    public decimal ActualCost { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Product Product { get; set; }
}