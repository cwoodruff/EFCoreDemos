using System;
using System.Collections.Generic;

namespace interception_db_ops.Model;

public class SalesReason
{
    public SalesReason()
    {
        SalesOrderHeaderSalesReason = new HashSet<SalesOrderHeaderSalesReason>();
    }

    public int SalesReasonID { get; set; }
    public string Name { get; set; }
    public string ReasonType { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<SalesOrderHeaderSalesReason> SalesOrderHeaderSalesReason { get; set; }
}