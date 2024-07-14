﻿using System;

namespace lazy_loading.Model;

public class EmailAddress
{
    public int BusinessEntityID { get; set; }
    public int EmailAddressID { get; set; }
    public string EmailAddress1 { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Person BusinessEntity { get; set; }
}