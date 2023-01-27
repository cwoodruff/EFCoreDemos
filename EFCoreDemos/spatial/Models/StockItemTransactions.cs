using System;

namespace spatial.Models;

public partial class StockItemTransactions
{
    public int StockItemTransactionId { get; set; }
    public int StockItemId { get; set; }
    public int TransactionTypeId { get; set; }
    public int? CustomerId { get; set; }
    public int? InvoiceId { get; set; }
    public int? SupplierId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public DateTime TransactionOccurredWhen { get; set; }
    public decimal Quantity { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual Customers Customer { get; set; }
    public virtual Invoices Invoice { get; set; }
    public virtual People LastEditedByNavigation { get; set; }
    public virtual PurchaseOrders PurchaseOrder { get; set; }
    public virtual StockItems StockItem { get; set; }
    public virtual Suppliers Supplier { get; set; }
    public virtual TransactionTypes TransactionType { get; set; }
}