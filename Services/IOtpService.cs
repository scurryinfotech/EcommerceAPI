using EcommerceAPI.Models;

namespace EcommerceAPI.Services
{
    public interface IOtpService
    {
        /// <summary>
        /// Triggers an OTP send via Muzztech for the given mobile number.
        /// Stores the Muzztech sessionId in DB and returns the send result.
        /// </summary>
        Task<OtpSendResult> SendOtpAsync(string mobileNumber);

        /// <summary>
        /// Verifies the OTP entered by the customer.
        /// Automatically retrieves the active Muzztech sessionId from DB using mobileNumber if sessionId is null or empty.
        /// </summary>
        Task<OtpVerifyResult> VerifyOtpAsync(string mobileNumber, string otp, string? sessionId = null);
    }
}
