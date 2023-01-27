using System;

namespace spatial.Models;

public partial class SupplierTransactions
{
    public int SupplierTransactionId { get; set; }
    public int SupplierId { get; set; }
    public int TransactionTypeId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int? PaymentMethodId { get; set; }
    public string SupplierInvoiceNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal AmountExcludingTax { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TransactionAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public DateTime? FinalizationDate { get; set; }
    public bool? IsFinalized { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual PaymentMethods PaymentMethod { get; set; }
    public virtual PurchaseOrders PurchaseOrder { get; set; }
    public virtual Suppliers Supplier { get; set; }
    public virtual TransactionTypes TransactionType { get; set; }
}