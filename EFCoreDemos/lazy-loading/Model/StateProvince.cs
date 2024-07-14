﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class StateProvince
{
    public StateProvince()
    {
        Address = new HashSet<Address>();
        SalesTaxRate = new HashSet<SalesTaxRate>();
    }

    public int StateProvinceID { get; set; }
    public string StateProvinceCode { get; set; }
    public string CountryRegionCode { get; set; }
    public bool IsOnlyStateProvinceFlag { get; set; }
    public string Name { get; set; }
    public int TerritoryID { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<Address> Address { get; set; }
    public virtual ICollection<SalesTaxRate> SalesTaxRate { get; set; }
    public virtual CountryRegion CountryRegionCodeNavigation { get; set; }
    public virtual SalesTerritory Territory { get; set; }
}