namespace EcommerceAPI.Models
{
    public class PaymentRequest
    {
        public string TransactionId { get; set; }
        public int Amount { get; set; }
        public string UserId { get; set; }
    }
}
