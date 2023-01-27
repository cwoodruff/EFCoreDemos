using System;
using System.Collections.Generic;
using query_tags.Model;

namespace query_tags.Model;

public class Department
{
    public Department()
    {
        EmployeeDepartmentHistory = new HashSet<EmployeeDepartmentHistory>();
    }

    public short DepartmentID { get; set; }
    public string Name { get; set; }
    public string GroupName { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<EmployeeDepartmentHistory> EmployeeDepartmentHistory { get; set; }
}