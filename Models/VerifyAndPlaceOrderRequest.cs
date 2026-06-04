using EcommerceAPI.Models;
using System.Text.Json.Serialization;

public class VerifyAndPlaceOrderRequest
{
    [JsonPropertyName("razorpayOrderId")]
    public string RazorpayOrderId { get; set; }

    [JsonPropertyName("razorpayPaymentId")]
    public string RazorpayPaymentId { get; set; }

    [JsonPropertyName("razorpaySignature")]
    public string RazorpaySignature { get; set; }

    [JsonPropertyName("order")]          
    public OrderRequest Order { get; set; }
}