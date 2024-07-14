﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class AddressType
{
    public AddressType()
    {
        BusinessEntityAddress = new HashSet<BusinessEntityAddress>();
    }

    public int AddressTypeID { get; set; }
    public string Name { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<BusinessEntityAddress> BusinessEntityAddress { get; set; }
}