using System.Text.Json.Serialization;

namespace EcommerceAPI.Models
{
    public class OtpEntry
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class OtpVerify
    {
        public string SessionId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class MuzztechSendResponse
    {
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("Details")]
        public string? Details { get; set; }

        [JsonPropertyName("OTP")]
        public string? Otp { get; set; }

        [JsonPropertyName("success")]
        public bool? Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class MuzztechOtpResponse : MuzztechSendResponse { }

    public class OtpSendResult
    {
        public bool Success { get; set; }
        public string? SessionId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? DebugOtp { get; set; }
        public string? OtpCode { get; set; }
    }

    public class OtpVerifyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
