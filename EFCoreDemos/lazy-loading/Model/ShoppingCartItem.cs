﻿using System;

namespace lazy_loading.Model;

public class ShoppingCartItem
{
    public int ShoppingCartItemID { get; set; }
    public string ShoppingCartID { get; set; }
    public int Quantity { get; set; }
    public int ProductID { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime ModifiedDate { get; set; }

    public virtual Product Product { get; set; }
}