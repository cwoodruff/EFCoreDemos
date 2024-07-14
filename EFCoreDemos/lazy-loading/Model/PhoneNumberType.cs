using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class PhoneNumberType
{
    public PhoneNumberType()
    {
        PersonPhone = new HashSet<PersonPhone>();
    }

    public int PhoneNumberTypeID { get; set; }
    public string Name { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<PersonPhone> PersonPhone { get; set; }
}