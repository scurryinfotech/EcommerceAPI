namespace EcommerceAPI.Models
{
    public class PaypalCaptureRequest
    {
        public int DbOrderId { get; set; }

        public string PaypalOrderId { get; set; }

        public string? PaypalCaptureId { get; set; }

        public OrderRequest? Order { get; set; }
    }
}
