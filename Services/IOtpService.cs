using EcommerceAPI.Models;

namespace EcommerceAPI.Services
{
    public interface IOtpService
    {
        /// <summary>
        /// Triggers an OTP send via Muzztech for the given mobile number.
        /// Returns the Muzztech sessionId that must be sent back on verify.
        /// </summary>
        Task<OtpSendResult> SendOtpAsync(string mobileNumber);

        /// <summary>
        /// Verifies the OTP entered by the customer against the given
        /// Muzztech sessionId.
        /// </summary>
        Task<OtpVerifyResult> VerifyOtpAsync(string sessionId, string otp);
    }
}
