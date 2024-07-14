﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class Shift
{
    public Shift()
    {
        EmployeeDepartmentHistory = new HashSet<EmployeeDepartmentHistory>();
    }

    public byte ShiftID { get; set; }
    public string Name { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<EmployeeDepartmentHistory> EmployeeDepartmentHistory { get; set; }
}