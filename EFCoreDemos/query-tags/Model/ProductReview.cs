using System;

namespace query_tags.Model;

public class ProductReview
{
    public int ProductReviewID { get; set; }
    public int ProductID { get; set; }
    public string ReviewerName { get; set; }
    public DateTime ReviewDate { get; set; }
    public string EmailAddress { get; set; }
    public int Rating { get; set; }
    public string Comments { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Product Product { get; set; }
}