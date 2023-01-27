using System;
using System.Collections.Generic;

namespace spatial.Models;

public partial class People
{
    public People()
    {
        BuyingGroups = new HashSet<BuyingGroups>();
        Cities = new HashSet<Cities>();
        Colors = new HashSet<Colors>();
        Countries = new HashSet<Countries>();
        CustomerCategories = new HashSet<CustomerCategories>();
        CustomerTransactions = new HashSet<CustomerTransactions>();
        CustomersAlternateContactPerson = new HashSet<Customers>();
        CustomersLastEditedByNavigation = new HashSet<Customers>();
        CustomersPrimaryContactPerson = new HashSet<Customers>();
        DeliveryMethods = new HashSet<DeliveryMethods>();
        InverseLastEditedByNavigation = new HashSet<People>();
        InvoiceLines = new HashSet<InvoiceLines>();
        InvoicesAccountsPerson = new HashSet<Invoices>();
        InvoicesContactPerson = new HashSet<Invoices>();
        InvoicesLastEditedByNavigation = new HashSet<Invoices>();
        InvoicesPackedByPerson = new HashSet<Invoices>();
        InvoicesSalespersonPerson = new HashSet<Invoices>();
        OrderLines = new HashSet<OrderLines>();
        OrdersContactPerson = new HashSet<Orders>();
        OrdersLastEditedByNavigation = new HashSet<Orders>();
        OrdersPickedByPerson = new HashSet<Orders>();
        OrdersSalespersonPerson = new HashSet<Orders>();
        PackageTypes = new HashSet<PackageTypes>();
        PaymentMethods = new HashSet<PaymentMethods>();
        PurchaseOrderLines = new HashSet<PurchaseOrderLines>();
        PurchaseOrdersContactPerson = new HashSet<PurchaseOrders>();
        PurchaseOrdersLastEditedByNavigation = new HashSet<PurchaseOrders>();
        SpecialDeals = new HashSet<SpecialDeals>();
        StateProvinces = new HashSet<StateProvinces>();
        StockGroups = new HashSet<StockGroups>();
        StockItemHoldings = new HashSet<StockItemHoldings>();
        StockItemStockGroups = new HashSet<StockItemStockGroups>();
        StockItemTransactions = new HashSet<StockItemTransactions>();
        StockItems = new HashSet<StockItems>();
        SupplierCategories = new HashSet<SupplierCategories>();
        SupplierTransactions = new HashSet<SupplierTransactions>();
        SuppliersAlternateContactPerson = new HashSet<Suppliers>();
        SuppliersLastEditedByNavigation = new HashSet<Suppliers>();
        SuppliersPrimaryContactPerson = new HashSet<Suppliers>();
        SystemParameters = new HashSet<SystemParameters>();
        TransactionTypes = new HashSet<TransactionTypes>();
    }

    public int PersonId { get; set; }
    public string FullName { get; set; }
    public string PreferredName { get; set; }
    public string SearchName { get; set; }
    public bool IsPermittedToLogon { get; set; }
    public string LogonName { get; set; }
    public bool IsExternalLogonProvider { get; set; }
    public byte[] HashedPassword { get; set; }
    public bool IsSystemUser { get; set; }
    public bool IsEmployee { get; set; }
    public bool IsSalesperson { get; set; }
    public string UserPreferences { get; set; }
    public string PhoneNumber { get; set; }
    public string FaxNumber { get; set; }
    public string EmailAddress { get; set; }
    public byte[] Photo { get; set; }
    public string CustomFields { get; set; }
    public string OtherLanguages { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual ICollection<BuyingGroups> BuyingGroups { get; set; }
    public virtual ICollection<Cities> Cities { get; set; }
    public virtual ICollection<Colors> Colors { get; set; }
    public virtual ICollection<Countries> Countries { get; set; }
    public virtual ICollection<CustomerCategories> CustomerCategories { get; set; }
    public virtual ICollection<CustomerTransactions> CustomerTransactions { get; set; }
    public virtual ICollection<Customers> CustomersAlternateContactPerson { get; set; }
    public virtual ICollection<Customers> CustomersLastEditedByNavigation { get; set; }
    public virtual ICollection<Customers> CustomersPrimaryContactPerson { get; set; }
    public virtual ICollection<DeliveryMethods> DeliveryMethods { get; set; }
    public virtual ICollection<People> InverseLastEditedByNavigation { get; set; }
    public virtual ICollection<InvoiceLines> InvoiceLines { get; set; }
    public virtual ICollection<Invoices> InvoicesAccountsPerson { get; set; }
    public virtual ICollection<Invoices> InvoicesContactPerson { get; set; }
    public virtual ICollection<Invoices> InvoicesLastEditedByNavigation { get; set; }
    public virtual ICollection<Invoices> InvoicesPackedByPerson { get; set; }
    public virtual ICollection<Invoices> InvoicesSalespersonPerson { get; set; }
    public virtual ICollection<OrderLines> OrderLines { get; set; }
    public virtual ICollection<Orders> OrdersContactPerson { get; set; }
    public virtual ICollection<Orders> OrdersLastEditedByNavigation { get; set; }
    public virtual ICollection<Orders> OrdersPickedByPerson { get; set; }
    public virtual ICollection<Orders> OrdersSalespersonPerson { get; set; }
    public virtual ICollection<PackageTypes> PackageTypes { get; set; }
    public virtual ICollection<PaymentMethods> PaymentMethods { get; set; }
    public virtual ICollection<PurchaseOrderLines> PurchaseOrderLines { get; set; }
    public virtual ICollection<PurchaseOrders> PurchaseOrdersContactPerson { get; set; }
    public virtual ICollection<PurchaseOrders> PurchaseOrdersLastEditedByNavigation { get; set; }
    public virtual ICollection<SpecialDeals> SpecialDeals { get; set; }
    public virtual ICollection<StateProvinces> StateProvinces { get; set; }
    public virtual ICollection<StockGroups> StockGroups { get; set; }
    public virtual ICollection<StockItemHoldings> StockItemHoldings { get; set; }
    public virtual ICollection<StockItemStockGroups> StockItemStockGroups { get; set; }
    public virtual ICollection<StockItemTransactions> StockItemTransactions { get; set; }
    public virtual ICollection<StockItems> StockItems { get; set; }
    public virtual ICollection<SupplierCategories> SupplierCategories { get; set; }
    public virtual ICollection<SupplierTransactions> SupplierTransactions { get; set; }
    public virtual ICollection<Suppliers> SuppliersAlternateContactPerson { get; set; }
    public virtual ICollection<Suppliers> SuppliersLastEditedByNavigation { get; set; }
    public virtual ICollection<Suppliers> SuppliersPrimaryContactPerson { get; set; }
    public virtual ICollection<SystemParameters> SystemParameters { get; set; }
    public virtual ICollection<TransactionTypes> TransactionTypes { get; set; }
}