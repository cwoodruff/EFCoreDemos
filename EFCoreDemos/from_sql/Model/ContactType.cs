using System;
using System.Collections.Generic;

namespace from_sql.Model;

public class ContactType
{
    public ContactType()
    {
        BusinessEntityContact = new HashSet<BusinessEntityContact>();
    }

    public int ContactTypeID { get; set; }
    public string Name { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<BusinessEntityContact> BusinessEntityContact { get; set; }
}