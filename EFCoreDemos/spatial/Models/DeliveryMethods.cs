using System;
using System.Collections.Generic;

namespace spatial.Models;

public partial class DeliveryMethods
{
    public DeliveryMethods()
    {
        Customers = new HashSet<Customers>();
        Invoices = new HashSet<Invoices>();
        PurchaseOrders = new HashSet<PurchaseOrders>();
        Suppliers = new HashSet<Suppliers>();
    }

    public int DeliveryMethodId { get; set; }
    public string DeliveryMethodName { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual ICollection<Customers> Customers { get; set; }
    public virtual ICollection<Invoices> Invoices { get; set; }
    public virtual ICollection<PurchaseOrders> PurchaseOrders { get; set; }
    public virtual ICollection<Suppliers> Suppliers { get; set; }
}