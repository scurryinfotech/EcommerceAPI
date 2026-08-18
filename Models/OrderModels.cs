namespace EcommerceAPI.Models
{
    public class OrderDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerMobile { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string ShippingAddressJson { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Tax { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; } = "COD";
        public string PaymentStatus { get; set; } = "Pending";
        public string OrderStatus { get; set; } = "Pending";
        public string? TrackingNumber { get; set; }
        public string? CourierName { get; set; }
        public string? InternalNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
        public List<OrderStatusHistoryDto> Timeline { get; set; } = new();
    }

    public class OrderItemDto
    {
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int PackQuantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class OrderStatusHistoryDto
    {
        public int HistoryId { get; set; }
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string ChangedBy { get; set; } = "System";
        public DateTime CreatedAt { get; set; }
    }

    public class PlaceOrderRequest
    {
        public int UserId { get; set; }
        public int AddressId { get; set; }
        public string PaymentMethod { get; set; } = "COD"; // COD, Razorpay, Paypal, BankTransfer
        public List<CartItemInput> Items { get; set; } = new();
    }

    public class CartItemInput
    {
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PaymentStatus { get; set; }
        public string? TrackingNumber { get; set; }
        public string? CourierName { get; set; }
        public string? Notes { get; set; }
        public string ChangedBy { get; set; } = "Admin";
    }
}
