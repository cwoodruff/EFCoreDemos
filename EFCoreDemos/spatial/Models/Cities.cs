using System;
using System.Collections.Generic;

namespace spatial.Models;

public partial class Cities
{
    public Cities()
    {
        CustomersDeliveryCity = new HashSet<Customers>();
        CustomersPostalCity = new HashSet<Customers>();
        SuppliersDeliveryCity = new HashSet<Suppliers>();
        SuppliersPostalCity = new HashSet<Suppliers>();
        SystemParametersDeliveryCity = new HashSet<SystemParameters>();
        SystemParametersPostalCity = new HashSet<SystemParameters>();
    }

    public int CityId { get; set; }
    public string CityName { get; set; }
    public int StateProvinceId { get; set; }
    public long? LatestRecordedPopulation { get; set; }
    public int LastEditedBy { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public NetTopologySuite.Geometries.Point Location { get; set; }

    public virtual People LastEditedByNavigation { get; set; }
    public virtual StateProvinces StateProvince { get; set; }
    public virtual ICollection<Customers> CustomersDeliveryCity { get; set; }
    public virtual ICollection<Customers> CustomersPostalCity { get; set; }
    public virtual ICollection<Suppliers> SuppliersDeliveryCity { get; set; }
    public virtual ICollection<Suppliers> SuppliersPostalCity { get; set; }
    public virtual ICollection<SystemParameters> SystemParametersDeliveryCity { get; set; }
    public virtual ICollection<SystemParameters> SystemParametersPostalCity { get; set; }
}