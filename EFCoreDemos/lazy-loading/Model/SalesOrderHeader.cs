﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class SalesOrderHeader
{
    public SalesOrderHeader()
    {
        SalesOrderDetail = new HashSet<SalesOrderDetail>();
        SalesOrderHeaderSalesReason = new HashSet<SalesOrderHeaderSalesReason>();
    }

    public int SalesOrderID { get; set; }
    public byte RevisionNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ShipDate { get; set; }
    public byte Status { get; set; }
    public bool OnlineOrderFlag { get; set; }
    public string SalesOrderNumber { get; set; }
    public string PurchaseOrderNumber { get; set; }
    public string AccountNumber { get; set; }
    public int CustomerID { get; set; }
    public int? SalesPersonID { get; set; }
    public int? TerritoryID { get; set; }
    public int BillToAddressID { get; set; }
    public int ShipToAddressID { get; set; }
    public int ShipMethodID { get; set; }
    public int? CreditCardID { get; set; }
    public string CreditCardApprovalCode { get; set; }
    public int? CurrencyRateID { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmt { get; set; }
    public decimal Freight { get; set; }
    public decimal TotalDue { get; set; }
    public string Comment { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<SalesOrderDetail> SalesOrderDetail { get; set; }
    public virtual ICollection<SalesOrderHeaderSalesReason> SalesOrderHeaderSalesReason { get; set; }
    public virtual Address BillToAddress { get; set; }
    public virtual CreditCard CreditCard { get; set; }
    public virtual CurrencyRate CurrencyRate { get; set; }
    public virtual Customer Customer { get; set; }
    public virtual SalesPerson SalesPerson { get; set; }
    public virtual ShipMethod ShipMethod { get; set; }
    public virtual Address ShipToAddress { get; set; }
    public virtual SalesTerritory Territory { get; set; }
}