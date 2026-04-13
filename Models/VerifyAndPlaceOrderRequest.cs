namespace EcommerceAPI.Models
{
    public class VerifyAndPlaceOrderRequest
    {
        public string RazorpayOrderId { get; set; }
        public string RazorpayPaymentId { get; set; }
        public string RazorpaySignature { get; set; }
        public OrderRequest Order { get; set; }
    }
}
