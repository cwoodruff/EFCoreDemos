﻿namespace sproc_mapping.Chinook;

public sealed class Invoice : BaseEntity
{
    public Invoice()
    {
        InvoiceLines = new HashSet<InvoiceLine>();
    }

    public int CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string? BillingAddress { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingCountry { get; set; }
    public string? BillingPostalCode { get; set; }
    public decimal Total { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<InvoiceLine> InvoiceLines { get; set; }
}