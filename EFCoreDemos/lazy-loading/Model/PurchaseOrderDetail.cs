﻿using System;

namespace lazy_loading.Model;

public class PurchaseOrderDetail
{
    public int PurchaseOrderID { get; set; }
    public int PurchaseOrderDetailID { get; set; }
    public DateTime DueDate { get; set; }
    public short OrderQty { get; set; }
    public int ProductID { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal RejectedQty { get; set; }
    public decimal StockedQty { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Product Product { get; set; }
    public virtual PurchaseOrderHeader PurchaseOrder { get; set; }
}