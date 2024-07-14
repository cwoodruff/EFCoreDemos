﻿using System;

namespace lazy_loading.Model;

public class Password
{
    public int BusinessEntityID { get; set; }
    public string PasswordHash { get; set; }
    public string PasswordSalt { get; set; }
    public Guid rowguid { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Person BusinessEntity { get; set; }
}