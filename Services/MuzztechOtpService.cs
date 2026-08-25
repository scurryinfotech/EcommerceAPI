using EcommerceAPI.Models;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace EcommerceAPI.Services
{
    public class MuzztechOtpService : IOtpService
    {
        private static readonly ConcurrentDictionary<string, (string SessionId, DateTime Expiry)> _sessionCache = new();

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
                MuzztechOtpResponse? muzzRes = null;

                // 1. JSON Payload (Documented format)
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

                _logger.LogInformation("Muzztech SendOtp JSON Response (HTTP {StatusCode}): {Body}", (int)response.StatusCode, responseBody);

                try { muzzRes = JsonSerializer.Deserialize<MuzztechOtpResponse>(responseBody); } catch { }

                if (response.IsSuccessStatusCode && muzzRes != null && string.Equals(muzzRes.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    string muzzSessionId = muzzRes.Details ?? string.Empty;
                    if (!string.IsNullOrEmpty(muzzSessionId))
                    {
                        StoreSessionInCache(mobileNumber, cleanMobile, muzzSessionId);
                        SaveOtpSessionToDb(cleanMobile, muzzSessionId);
                        return new OtpSendResult
                        {
                            Success = true,
                            SessionId = muzzSessionId,
                            Message = "OTP sent successfully to your mobile number."
                        };
                    }
                }

                // 2. FormUrlEncoded Fallback
                var formPayload = new Dictionary<string, string>
                {
                    { "api_key", ApiKey },
                    { "phone_number", cleanMobile },
                    { "otp_template_name", OtpTemplateName }
                };

                _logger.LogInformation("Posting FormUrlEncoded Fallback to Muzztech SendOtp API ({Url}) for Mobile: {Mobile}", $"{BaseUrl}/api/V1", cleanMobile);

                var formContent = new FormUrlEncodedContent(formPayload);
                var formResp = await _httpClient.PostAsync($"{BaseUrl}/api/V1", formContent);
                var formResponseBody = await formResp.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech SendOtp Form Response (HTTP {StatusCode}): {Body}", (int)formResp.StatusCode, formResponseBody);

                try { muzzRes = JsonSerializer.Deserialize<MuzztechOtpResponse>(formResponseBody); } catch { }

                if (formResp.IsSuccessStatusCode && muzzRes != null && string.Equals(muzzRes.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    string muzzSessionId = muzzRes.Details ?? string.Empty;
                    if (!string.IsNullOrEmpty(muzzSessionId))
                    {
                        StoreSessionInCache(mobileNumber, cleanMobile, muzzSessionId);
                        SaveOtpSessionToDb(cleanMobile, muzzSessionId);
                        return new OtpSendResult
                        {
                            Success = true,
                            SessionId = muzzSessionId,
                            Message = "OTP sent successfully to your mobile number."
                        };
                    }
                }

                string errorMessage = muzzRes?.Message ?? muzzRes?.Details ?? $"Muzztech rejected request with HTTP status {(int)response.StatusCode}. Details: {responseBody}";
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

        public async Task<OtpVerifyResult> VerifyOtpAsync(string mobileNumber, string otp, string? sessionId = null)
        {
            if (string.IsNullOrWhiteSpace(otp))
            {
                return new OtpVerifyResult { Success = false, Message = "OTP is required." };
            }

            if (string.IsNullOrWhiteSpace(mobileNumber) && string.IsNullOrWhiteSpace(sessionId))
            {
                return new OtpVerifyResult { Success = false, Message = "Mobile number or Session ID is required." };
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                _logger.LogError("Muzztech API Key is missing.");
                return new OtpVerifyResult { Success = false, Message = "SMS gateway configuration error." };
            }

            // Step 1: Check sessionId passed directly
            var targetSessionId = sessionId;

            // Step 2: Check In-Memory Cache
            if (string.IsNullOrWhiteSpace(targetSessionId) && !string.IsNullOrWhiteSpace(mobileNumber))
            {
                targetSessionId = GetSessionFromCache(mobileNumber);
            }

            // Step 3: Check SQL Database
            if (string.IsNullOrWhiteSpace(targetSessionId) && !string.IsNullOrWhiteSpace(mobileNumber))
            {
                targetSessionId = GetLatestSessionIdFromDb(mobileNumber);
            }

            if (string.IsNullOrWhiteSpace(targetSessionId))
            {
                _logger.LogWarning("No active OTP session found in Memory/DB for Mobile: {Mobile}", mobileNumber);
                return new OtpVerifyResult { Success = false, Message = "No active OTP session found. Please request a new OTP." };
            }

            var cleanSessionId = targetSessionId.Trim();
            var cleanOtp = otp.Trim();

            try
            {
                MuzztechOtpResponse? muzzRes = null;

                // 1. JSON Payload (Documented format)
                var verifyObj = new
                {
                    api_key = ApiKey,
                    otp_session = cleanSessionId,
                    otp_entered_by_user = cleanOtp
                };

                var jsonString = JsonSerializer.Serialize(verifyObj);
                var jsonContent = new StringContent(jsonString, Encoding.UTF8, "application/json");

                _logger.LogInformation("Posting JSON to Muzztech VerifyOtp API ({Url}) for Session {SessionId}: {Body}", $"{BaseUrl}/api/V1", cleanSessionId, jsonString);

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/V1", jsonContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech VerifyOtp JSON Response (HTTP {StatusCode}): {Body}", (int)response.StatusCode, responseBody);

                try { muzzRes = JsonSerializer.Deserialize<MuzztechOtpResponse>(responseBody); } catch { }

                if (response.IsSuccessStatusCode && muzzRes != null && string.Equals(muzzRes.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    MarkOtpVerifiedInDb(cleanSessionId);
                    RemoveSessionFromCache(mobileNumber);
                    return new OtpVerifyResult
                    {
                        Success = true,
                        Message = "OTP verified successfully."
                    };
                }

                // 2. FormUrlEncoded Fallback
                var formPayload = new Dictionary<string, string>
                {
                    { "api_key", ApiKey },
                    { "otp_session", cleanSessionId },
                    { "otp_entered_by_user", cleanOtp }
                };

                _logger.LogInformation("Posting FormUrlEncoded Fallback to Muzztech VerifyOtp API ({Url}) - Session: {SessionId}, OTP: {Otp}", $"{BaseUrl}/api/V1", cleanSessionId, cleanOtp);

                var formContent = new FormUrlEncodedContent(formPayload);
                var formResp = await _httpClient.PostAsync($"{BaseUrl}/api/V1", formContent);
                var formResponseBody = await formResp.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech VerifyOtp Form Response (HTTP {StatusCode}): {Body}", (int)formResp.StatusCode, formResponseBody);

                try
                {
                    var formMuzzRes = JsonSerializer.Deserialize<MuzztechOtpResponse>(formResponseBody);
                    if (formMuzzRes != null) muzzRes = formMuzzRes;
                }
                catch { }

                if (formResp.IsSuccessStatusCode && muzzRes != null && string.Equals(muzzRes.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    MarkOtpVerifiedInDb(cleanSessionId);
                    RemoveSessionFromCache(mobileNumber);
                    return new OtpVerifyResult
                    {
                        Success = true,
                        Message = "OTP verified successfully."
                    };
                }

                string errorMessage = muzzRes?.Message ?? muzzRes?.Details ?? $"Invalid or expired OTP. Response: {responseBody}";
                _logger.LogWarning("Muzztech VerifyOtp failed for Session {SessionId}: {Error}", cleanSessionId, errorMessage);

                return new OtpVerifyResult
                {
                    Success = false,
                    Message = errorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception verifying Muzztech OTP for Mobile {Mobile}, Session {SessionId}", mobileNumber, targetSessionId);
                return new OtpVerifyResult
                {
                    Success = false,
                    Message = "An error occurred during OTP verification."
                };
            }
        }

        private void StoreSessionInCache(string rawMobile, string cleanMobile, string sessionId)
        {
            var entry = (SessionId: sessionId, Expiry: DateTime.UtcNow.AddMinutes(15));
            _sessionCache[cleanMobile] = entry;
            _sessionCache[rawMobile] = entry;
        }

        private string? GetSessionFromCache(string mobileNumber)
        {
            var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);

            if (_sessionCache.TryGetValue(cleanMobile, out var val) && val.Expiry > DateTime.UtcNow)
            {
                return val.SessionId;
            }
            if (_sessionCache.TryGetValue(mobileNumber, out var val2) && val2.Expiry > DateTime.UtcNow)
            {
                return val2.SessionId;
            }
            return null;
        }

        private void RemoveSessionFromCache(string mobileNumber)
        {
            var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);
            _sessionCache.TryRemove(cleanMobile, out _);
            _sessionCache.TryRemove(mobileNumber, out _);
        }

        private string? GetLatestSessionIdFromDb(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(DbConnStr)) return null;
            try
            {
                var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
                if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);

                using var con = new SqlConnection(DbConnStr);
                con.Open();
                string sql = @"SELECT TOP 1 OtpHash FROM OtpRequests 
                               WHERE (MobileNumber = @RawMobile OR MobileNumber = @CleanMobile OR MobileNumber LIKE '%' + @CleanMobile)
                               AND IsVerified = 0 
                               ORDER BY CreatedAt DESC";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@RawMobile", mobileNumber);
                cmd.Parameters.AddWithValue("@CleanMobile", cleanMobile);
                var result = cmd.ExecuteScalar();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve latest OTP sessionId from DB for mobile {Mobile}", mobileNumber);
                return null;
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

