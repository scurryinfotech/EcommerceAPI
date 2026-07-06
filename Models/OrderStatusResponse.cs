namespace EcommerceAPI.Models
{
    public class OrderStatusResponse
    {
        public string OrderNumber { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMode { get; set; }
        public string PaymentStatus { get; set; }
        public string OrderStatus { get; set; }
        public string RefundStatus { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? PaymentCompletedAt { get; set; }
        public List<OrderItemView> Items { get; set; } = new List<OrderItemView>();
    }
    public class OrderItemView
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

}
