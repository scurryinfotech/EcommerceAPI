using EcommerceAPI.Models;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace EcommerceAPI.Services
{
    public class MuzztechOtpService : IOtpService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MuzztechOtpService> _logger;

        public MuzztechOtpService(HttpClient httpClient, IConfiguration configuration, ILogger<MuzztechOtpService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        private string BaseUrl => (_configuration["Muzztech:BaseUrl"] ?? "https://connect.muzztech.com").TrimEnd('/');
        private string ApiKey => _configuration["Muzztech:ApiKey"] ?? string.Empty;
        private string OtpTemplateName => _configuration["Muzztech:OtpTemplateName"] ?? "otp";
        private string DbConnStr => _configuration.GetConnectionString("EcommerceDb") ?? string.Empty;

        public async Task<OtpSendResult> SendOtpAsync(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
            {
                return new OtpSendResult { Success = false, Message = "Mobile number is required." };
            }

            var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                _logger.LogError("Muzztech API Key is missing in appsettings.json.");
                return new OtpSendResult { Success = false, Message = "SMS gateway API key is not configured." };
            }

            try
            {
                // Muzztech Send OTP Endpoint: POST https://connect.muzztech.com/api/V1
                var jsonObj = new
                {
                    api_key = ApiKey,
                    phone_number = cleanMobile,
                    otp_template_name = OtpTemplateName
                };

                var jsonString = JsonSerializer.Serialize(jsonObj);
                var jsonContent = new StringContent(jsonString, Encoding.UTF8, "application/json");

                _logger.LogInformation("Posting JSON to Muzztech SendOtp API ({Url}): {Body}", $"{BaseUrl}/api/V1", jsonString);

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/V1", jsonContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech SendOtp Response ({StatusCode}): {Body}", response.StatusCode, responseBody);

                MuzztechOtpResponse? muzzRes = null;
                try
                {
                    muzzRes = JsonSerializer.Deserialize<MuzztechOtpResponse>(responseBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Muzztech SendOtp JSON response: {Body}", responseBody);
                }

                if (response.IsSuccessStatusCode && muzzRes != null && string.Equals(muzzRes.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    string muzzSessionId = muzzRes.Details ?? string.Empty;
                    if (!string.IsNullOrEmpty(muzzSessionId))
                    {
                        SaveOtpSessionToDb(cleanMobile, muzzSessionId);
                        return new OtpSendResult
                        {
                            Success = true,
                            SessionId = muzzSessionId,
                            Message = "OTP sent successfully to your mobile number."
                        };
                    }
                }

                // Failed response from Muzztech
                string errorMessage = muzzRes?.Message ?? muzzRes?.Details ?? $"Muzztech rejected request with status HTTP {(int)response.StatusCode}.";
                _logger.LogWarning("Muzztech SendOtp failed for {Mobile}: {Error}", cleanMobile, errorMessage);

                return new OtpSendResult
                {
                    Success = false,
                    Message = $"Failed to send OTP: {errorMessage}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SendOtpAsync for {Mobile}", mobileNumber);
                return new OtpSendResult
                {
                    Success = false,
                    Message = "An unexpected error occurred while sending OTP. Please try again."
                };
            }
        }

        public async Task<OtpVerifyResult> VerifyOtpAsync(string sessionId, string otp)
        {
            if (string.IsNullOrWhiteSpace(otp))
            {
                return new OtpVerifyResult { Success = false, Message = "OTP is required." };
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return new OtpVerifyResult { Success = false, Message = "Session ID is required." };
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                _logger.LogError("Muzztech API Key is missing.");
                return new OtpVerifyResult { Success = false, Message = "SMS gateway configuration error." };
            }

            try
            {
                // Muzztech Verify OTP Endpoint: POST https://connect.muzztech.com/api/V1
                var verifyObj = new
                {
                    api_key = ApiKey,
                    otp_session = sessionId,
                    otp_entered_by_user = otp
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(verifyObj), Encoding.UTF8, "application/json");

                _logger.LogInformation("Posting JSON to Muzztech VerifyOtp API ({Url}) for Session: {SessionId}", $"{BaseUrl}/api/V1", sessionId);

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/V1", jsonContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech VerifyOtp Response ({StatusCode}): {Body}", response.StatusCode, responseBody);

                MuzztechOtpResponse? muzzRes = null;
                try
                {
                    muzzRes = JsonSerializer.Deserialize<MuzztechOtpResponse>(responseBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Muzztech VerifyOtp JSON response: {Body}", responseBody);
                }

                if (response.IsSuccessStatusCode && muzzRes != null && string.Equals(muzzRes.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    MarkOtpVerifiedInDb(sessionId);
                    return new OtpVerifyResult
                    {
                        Success = true,
                        Message = "OTP verified successfully."
                    };
                }

                string errorMessage = muzzRes?.Message ?? muzzRes?.Details ?? "Invalid or expired OTP. Please try again.";
                _logger.LogWarning("Muzztech VerifyOtp failed for Session {SessionId}: {Error}", sessionId, errorMessage);

                return new OtpVerifyResult
                {
                    Success = false,
                    Message = errorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception verifying Muzztech OTP for Session {SessionId}", sessionId);
                return new OtpVerifyResult
                {
                    Success = false,
                    Message = "An error occurred during OTP verification."
                };
            }
        }

        private void SaveOtpSessionToDb(string mobileNumber, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(DbConnStr)) return;
            try
            {
                using var con = new SqlConnection(DbConnStr);
                con.Open();
                string sql = @"INSERT INTO OtpRequests (MobileNumber, OtpHash, ExpiryTime, AttemptsCount, MaxAttempts, IsVerified, Purpose, CreatedAt)
                               VALUES (@MobileNumber, @SessionId, DATEADD(minute, 10, GETDATE()), 0, 5, 0, 'otp', GETDATE())";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@MobileNumber", mobileNumber);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log OTP session to DB for SessionId {SessionId}", sessionId);
            }
        }

        private void MarkOtpVerifiedInDb(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(DbConnStr)) return;
            try
            {
                using var con = new SqlConnection(DbConnStr);
                con.Open();
                string sql = @"UPDATE OtpRequests SET IsVerified = 1 WHERE OtpHash = @SessionId";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark OTP session verified in DB for SessionId {SessionId}", sessionId);
            }
        }
    }
}

