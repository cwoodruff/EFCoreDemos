﻿using System;
using System.Collections.Generic;

namespace lazy_loading.Model;

public class ScrapReason
{
    public ScrapReason()
    {
        WorkOrder = new HashSet<WorkOrder>();
    }

    public short ScrapReasonID { get; set; }
    public string Name { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<WorkOrder> WorkOrder { get; set; }
}