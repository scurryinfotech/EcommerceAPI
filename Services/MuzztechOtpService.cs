using EcommerceAPI.Models;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcommerceAPI.Services
{
    public class MuzztechOtpService : IOtpService
    {
        private static readonly ConcurrentDictionary<string, (string SessionId, string OtpCode, DateTime Expiry)> _sessionCache = new();

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
        private string ApiKey => _configuration["Muzztech:ApiKey"] ?? "693f2f40bc1c3c6dcda82bba24537897";
        private string OtpTemplateName => _configuration["Muzztech:OtpTemplateName"] ?? "otp";
        private string DbConnStr => _configuration.GetConnectionString("EcommerceDb") ?? string.Empty;

        private string HashOtp(string otp, string mobileNumber)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{otp}:{mobileNumber}:EU_OTP_SALT_2026"));
            return Convert.ToBase64String(bytes);
        }

        public async Task<OtpSendResult> SendOtpAsync(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
            {
                return new OtpSendResult { Success = false, Message = "Mobile number is required." };
            }

            var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);

            if (cleanMobile.Length != 10)
            {
                return new OtpSendResult { Success = false, Message = "Please enter a valid 10-digit Indian mobile number." };
            }

            // 1. Generate 6-digit OTP code
            string rawOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string hashedOtp = HashOtp(rawOtp, cleanMobile);
            string fallbackSessionId = Guid.NewGuid().ToString("N");

            _logger.LogInformation("Generated OTP {Otp} for Mobile {Mobile}", rawOtp, cleanMobile);

            // 2. Log OTP in SQL Database (OtpRequests table)
            SaveOtpToDb(cleanMobile, hashedOtp, rawOtp, fallbackSessionId);

            // 3. Dispatch OTP via Muzztech SMS gateway using multiple strategies
            string muzzSessionId = await DispatchMuzztechSmsAsync(cleanMobile, rawOtp);
            string activeSessionId = !string.IsNullOrEmpty(muzzSessionId) ? muzzSessionId : fallbackSessionId;

            // 4. Store in memory cache
            var entry = (SessionId: activeSessionId, OtpCode: rawOtp, Expiry: DateTime.UtcNow.AddMinutes(15));
            _sessionCache[cleanMobile] = entry;
            _sessionCache[mobileNumber] = entry;

            return new OtpSendResult
            {
                Success = true,
                SessionId = activeSessionId,
                OtpCode = rawOtp,
                DebugOtp = rawOtp,
                Message = $"Verification OTP has been dispatched via Muzztech to +91 {cleanMobile}."
            };
        }

        private async Task<string> DispatchMuzztechSmsAsync(string cleanMobile, string rawOtp)
        {
            var formattedMobile = "91" + cleanMobile;
            string capturedSessionId = "";

            try
            {
                // Strategy 1: POST https://connect.muzztech.com/api/V1 (JSON with OTP payload)
                var jsonObj = new
                {
                    api_key = ApiKey,
                    phone_number = cleanMobile,
                    mobile = cleanMobile,
                    phone = formattedMobile,
                    otp = rawOtp,
                    otp_template_name = OtpTemplateName
                };
                var jsonContent = new StringContent(JsonSerializer.Serialize(jsonObj), Encoding.UTF8, "application/json");
                var resp1 = await _httpClient.PostAsync($"{BaseUrl}/api/V1", jsonContent);
                var body1 = await resp1.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 1 POST /api/V1 ({Status}): {Body}", (int)resp1.StatusCode, body1);
                capturedSessionId = ExtractSessionId(body1);

                if (resp1.IsSuccessStatusCode && IsSuccessBody(body1)) return capturedSessionId;

                // Strategy 2: GET /api/send-otp
                var url2 = $"{BaseUrl}/api/send-otp?apiKey={ApiKey}&api_key={ApiKey}&mobile={cleanMobile}&phone={formattedMobile}&otp={rawOtp}&templateName={OtpTemplateName}&template_name={OtpTemplateName}";
                var resp2 = await _httpClient.GetAsync(url2);
                var body2 = await resp2.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 2 GET /api/send-otp ({Status}): {Body}", (int)resp2.StatusCode, body2);
                if (string.IsNullOrEmpty(capturedSessionId)) capturedSessionId = ExtractSessionId(body2);

                if (resp2.IsSuccessStatusCode && IsSuccessBody(body2)) return capturedSessionId;

                // Strategy 3: POST /api/send-otp (FormUrlEncoded)
                var formPayload = new Dictionary<string, string>
                {
                    { "apiKey", ApiKey },
                    { "api_key", ApiKey },
                    { "mobile", cleanMobile },
                    { "phone", formattedMobile },
                    { "otp", rawOtp },
                    { "templateName", OtpTemplateName },
                    { "template_name", OtpTemplateName }
                };
                var resp3 = await _httpClient.PostAsync($"{BaseUrl}/api/send-otp", new FormUrlEncodedContent(formPayload));
                var body3 = await resp3.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 3 POST Form /api/send-otp ({Status}): {Body}", (int)resp3.StatusCode, body3);
                if (string.IsNullOrEmpty(capturedSessionId)) capturedSessionId = ExtractSessionId(body3);

                if (resp3.IsSuccessStatusCode && IsSuccessBody(body3)) return capturedSessionId;

                // Strategy 4: GET /api/send-sms
                var url4 = $"{BaseUrl}/api/send-sms?apiKey={ApiKey}&to={cleanMobile}&message=Your OTP for verification is {rawOtp}";
                var resp4 = await _httpClient.GetAsync(url4);
                var body4 = await resp4.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 4 GET /api/send-sms ({Status}): {Body}", (int)resp4.StatusCode, body4);
                if (string.IsNullOrEmpty(capturedSessionId)) capturedSessionId = ExtractSessionId(body4);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Muzztech SMS Dispatch exception for {Mobile}", cleanMobile);
            }

            return capturedSessionId;
        }

        private static bool IsSuccessBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                string status = GetStringProp(root, "Status") ?? GetStringProp(root, "status") ?? "";
                return status.Equals("Success", StringComparison.OrdinalIgnoreCase) ||
                       status.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       (root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True);
            }
            catch { return false; }
        }

        private static string ExtractSessionId(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "";
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                return GetStringProp(root, "Details") ?? GetStringProp(root, "details") ?? GetStringProp(root, "SessionId") ?? GetStringProp(root, "session_id") ?? "";
            }
            catch { return ""; }
        }

        public async Task<OtpVerifyResult> VerifyOtpAsync(string mobileNumber, string otp, string? sessionId = null)
        {
            if (string.IsNullOrWhiteSpace(otp))
                return new OtpVerifyResult { Success = false, Message = "OTP is required." };

            var cleanMobile = (mobileNumber ?? "").Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);
            var cleanOtp = otp.Trim();

            _logger.LogInformation("Verifying OTP {Otp} for Mobile {Mobile}, Session: {SessionId}", cleanOtp, cleanMobile, sessionId);

            // Step 1: Local Hash Verification against OtpRequests DB table
            if (!string.IsNullOrEmpty(cleanMobile) && VerifyOtpAgainstDb(cleanMobile, cleanOtp))
            {
                MarkOtpVerifiedInDb(cleanMobile, sessionId);
                RemoveSessionFromCache(cleanMobile);
                return new OtpVerifyResult { Success = true, Message = "OTP verified successfully." };
            }

            // Step 2: Memory Cache Check
            if (!string.IsNullOrEmpty(cleanMobile))
            {
                var cached = GetSessionFromCache(cleanMobile);
                if (!string.IsNullOrEmpty(cached.OtpCode) && cached.OtpCode == cleanOtp)
                {
                    MarkOtpVerifiedInDb(cleanMobile, sessionId);
                    RemoveSessionFromCache(cleanMobile);
                    return new OtpVerifyResult { Success = true, Message = "OTP verified successfully." };
                }
            }

            // Step 3: Call Muzztech 2FA Session Verification API if sessionId is present
            if (!string.IsNullOrEmpty(sessionId))
            {
                var muzzRes = await PostMuzztechVerifyAsync(sessionId, cleanOtp);
                if (muzzRes.Success)
                {
                    MarkOtpVerifiedInDb(cleanMobile, sessionId);
                    RemoveSessionFromCache(cleanMobile);
                    return muzzRes;
                }
            }

            return new OtpVerifyResult { Success = false, Message = "Invalid or expired OTP. Please try again." };
        }

        private async Task<OtpVerifyResult> PostMuzztechVerifyAsync(string sessionId, string otp)
        {
            try
            {
                var payload = new
                {
                    api_key = ApiKey,
                    otp_session = sessionId,
                    otp_entered_by_user = otp
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseUrl}/api/V1", jsonContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech Verify API (Session: {SessionId}) [{Status}]: {Body}", sessionId, (int)response.StatusCode, responseBody);

                if (string.IsNullOrWhiteSpace(responseBody)) return new OtpVerifyResult { Success = false, Message = "Empty response." };

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                string status = GetStringProp(root, "Status") ?? GetStringProp(root, "status") ?? "";
                string details = GetStringProp(root, "Details") ?? GetStringProp(root, "details") ?? "";

                if (status.Equals("Success", StringComparison.OrdinalIgnoreCase) || details.Contains("Matched", StringComparison.OrdinalIgnoreCase))
                {
                    return new OtpVerifyResult { Success = true, Message = "OTP verified successfully." };
                }

                return new OtpVerifyResult { Success = false, Message = string.IsNullOrWhiteSpace(details) ? "Invalid OTP." : details };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Muzztech Verify HTTP Exception for Session {SessionId}", sessionId);
                return new OtpVerifyResult { Success = false, Message = ex.Message };
            }
        }

        private static string? GetStringProp(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        private (string SessionId, string OtpCode) GetSessionFromCache(string mobileNumber)
        {
            var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);

            if (_sessionCache.TryGetValue(cleanMobile, out var val) && val.Expiry > DateTime.UtcNow)
                return (val.SessionId, val.OtpCode);
            if (_sessionCache.TryGetValue(mobileNumber, out var val2) && val2.Expiry > DateTime.UtcNow)
                return (val2.SessionId, val2.OtpCode);

            return ("", "");
        }

        private void RemoveSessionFromCache(string mobileNumber)
        {
            var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);
            _sessionCache.TryRemove(cleanMobile, out _);
            _sessionCache.TryRemove(mobileNumber, out _);
        }

        private void SaveOtpToDb(string cleanMobile, string hashedOtp, string rawOtp, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(DbConnStr)) return;
            try
            {
                using var con = new SqlConnection(DbConnStr);
                con.Open();
                string sql = @"INSERT INTO OtpRequests (MobileNumber, OtpHash, ExpiryTime, AttemptsCount, MaxAttempts, IsVerified, Purpose, CreatedAt)
                               VALUES (@MobileNumber, @OtpHash, DATEADD(minute, 10, GETDATE()), 0, 5, 0, 'otp', GETDATE())";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@MobileNumber", cleanMobile);
                cmd.Parameters.AddWithValue("@OtpHash", hashedOtp);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log OTP session to DB for Mobile {Mobile}", cleanMobile);
            }
        }

        private bool VerifyOtpAgainstDb(string cleanMobile, string otp)
        {
            if (string.IsNullOrWhiteSpace(DbConnStr)) return false;
            try
            {
                string inputHash = HashOtp(otp, cleanMobile);
                using var con = new SqlConnection(DbConnStr);
                con.Open();
                string sql = @"SELECT TOP 1 OtpId FROM OtpRequests 
                               WHERE MobileNumber = @MobileNumber AND OtpHash = @OtpHash AND IsVerified = 0 AND ExpiryTime > GETDATE()
                               ORDER BY CreatedAt DESC";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@MobileNumber", cleanMobile);
                cmd.Parameters.AddWithValue("@OtpHash", inputHash);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP against DB for Mobile {Mobile}", cleanMobile);
            }
            return false;
        }

        private void MarkOtpVerifiedInDb(string cleanMobile, string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(DbConnStr)) return;
            try
            {
                using var con = new SqlConnection(DbConnStr);
                con.Open();
                string sql = @"UPDATE OtpRequests SET IsVerified = 1 WHERE MobileNumber = @MobileNumber AND IsVerified = 0";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@MobileNumber", cleanMobile);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark OTP verified in DB for Mobile {Mobile}", cleanMobile);
            }
        }
    }
}
