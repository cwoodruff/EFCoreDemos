using System;

namespace spatial.Models;

public partial class SystemParameters
{
    public int SystemParameterId { get; set; }
    public string DeliveryAddressLine1 { get; set; }
    public string DeliveryAddressLine2 { get; set; }
    public int DeliveryCityId { get; set; }
    public string DeliveryPostalCode { get; set; }
    public string PostalAddressLine1 { get; set; }
    public string PostalAddressLine2 { get; set; }
    public int PostalCityId { get; set; }
    public string PostalPostalCode { get; set; }
    public string ApplicationSettings { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime LastEditedWhen { get; set; }
    public NetTopologySuite.Geometries.Point Location { get; set; }

    public virtual Cities DeliveryCity { get; set; }
    public virtual People LastEditedByNavigation { get; set; }
    public virtual Cities PostalCity { get; set; }
}