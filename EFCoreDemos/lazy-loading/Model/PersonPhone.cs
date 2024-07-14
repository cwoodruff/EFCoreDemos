﻿using System;

namespace lazy_loading.Model;

public class PersonPhone
{
    public int BusinessEntityID { get; set; }
    public string PhoneNumber { get; set; }
    public int PhoneNumberTypeID { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Person BusinessEntity { get; set; }
    public virtual PhoneNumberType PhoneNumberType { get; set; }
}