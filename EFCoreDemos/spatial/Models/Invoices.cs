using System;
using System.Collections.Generic;

namespace spatial.Models;

public partial class Invoices
{
    public Invoices()
    {
        CustomerTransactions = new HashSet<CustomerTransactions>();
        InvoiceLines = new HashSet<InvoiceLines>();
        StockItemTransactions = new HashSet<StockItemTransactions>();
    }

    public int InvoiceId { get; set; }
    public int CustomerId { get; set; }
    public int BillToCustomerId { get; set; }
    public int? OrderId { get; set; }
    public int DeliveryMethodId { get; set; }
    public int ContactPersonId { get; set; }
    public int AccountsPersonId { get; set; }
    public int SalespersonPersonId { get; set; }
    public int PackedByPersonId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string CustomerPurchaseOrderNumber { get; set; }
    public bool IsCreditNote { get; set; }
    public string CreditNoteReason { get; set; }
    public string Comments { get; set; }
    public string DeliveryInstructions { get; set; }
    public string InternalComments { get; set; }
    public int TotalDryItems { get; set; }
    public int TotalChillerItems { get; set; }
    public string DeliveryRun { get; set; }
    public string RunPosition { get; set; }
    public string ReturnedDeliveryData { get; set; }
    public DateTime? ConfirmedDeliveryTime { get; set; }
    public string ConfirmedReceivedBy { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }

    public virtual People AccountsPerson { get; set; }
    public virtual Customers BillToCustomer { get; set; }
    public virtual People ContactPerson { get; set; }
    public virtual Customers Customer { get; set; }
    public virtual DeliveryMethods DeliveryMethod { get; set; }
    public virtual People LastEditedByNavigation { get; set; }
    public virtual Orders Order { get; set; }
    public virtual People PackedByPerson { get; set; }
    public virtual People SalespersonPerson { get; set; }
    public virtual ICollection<CustomerTransactions> CustomerTransactions { get; set; }
    public virtual ICollection<InvoiceLines> InvoiceLines { get; set; }
    public virtual ICollection<StockItemTransactions> StockItemTransactions { get; set; }
}