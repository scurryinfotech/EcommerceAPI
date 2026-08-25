using Microsoft.AspNetCore.Mvc;
using EcommerceAPI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcommerceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OtpController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OtpController> _logger;

        public OtpController(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<OtpController> logger)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // STEP 1: SEND OTP
        [HttpPost("send")]
        public async Task<IActionResult> SendOtp([FromBody] OtpEntry request)
        {
            string phone = !string.IsNullOrWhiteSpace(request.PhoneNumber) ? request.PhoneNumber : request.MobileNumber;
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { success = false, message = "Phone number is required" });
            }

            var cleanPhone = phone.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (cleanPhone.Length > 10) cleanPhone = cleanPhone.Substring(cleanPhone.Length - 10);

            var apiKey = _config["Muzztech:ApiKey"] ?? string.Empty;
            var templateName = _config["Muzztech:OtpTemplateName"] ?? "OTP";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Muzztech API key is missing in configuration.");
                return StatusCode(500, new { success = false, message = "SMS gateway configuration missing" });
            }

            var url = "https://connect.muzztech.com/api/V1";
            var http = _httpClientFactory.CreateClient();

            var bodyObj = new
            {
                api_key = apiKey,
                phone_number = cleanPhone,
                otp_template_name = templateName
            };

            var jsonBody = JsonSerializer.Serialize(bodyObj);

            var msg = new HttpRequestMessage(HttpMethod.Post, url);
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            msg.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                var response = await http.SendAsync(msg);
                var apiResponse = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech SendOtp API Response ({StatusCode}): {Body}", response.StatusCode, apiResponse);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, new { success = false, message = "OTP sending failed", apiResponse });

                var json = JsonSerializer.Deserialize<MuzztechSendResponse>(apiResponse);

                var sessionId = json?.Details ?? string.Empty;
                if (string.IsNullOrEmpty(sessionId))
                {
                    return StatusCode(500, new { success = false, message = "Invalid response from SMS gateway", apiResponse });
                }

                return Ok(new
                {
                    success = true,
                    message = "OTP sent successfully",
                    session_id = sessionId,
                    sessionId = sessionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SendOtp for phone {Phone}", phone);
                return StatusCode(500, new { success = false, message = "Error connecting to SMS gateway: " + ex.Message });
            }
        }

        // STEP 2: VERIFY OTP
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerify request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                return BadRequest(new { success = false, message = "Session ID is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Otp))
            {
                return BadRequest(new { success = false, message = "OTP is required" });
            }

            var apiKey = _config["Muzztech:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return StatusCode(500, new { success = false, message = "SMS gateway configuration missing" });
            }

            var url = "https://connect.muzztech.com/api/V1";
            var http = _httpClientFactory.CreateClient();

            var bodyObj = new
            {
                api_key = apiKey,
                otp_session = request.SessionId.Trim(),
                otp_entered_by_user = request.Otp.Trim()
            };

            var jsonBody = JsonSerializer.Serialize(bodyObj);

            var msg = new HttpRequestMessage(HttpMethod.Post, url);
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            msg.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                var response = await http.SendAsync(msg);
                var apiResponse = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Muzztech VerifyOtp API Response ({StatusCode}): {Body}", response.StatusCode, apiResponse);

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new { success = false, message = "OTP verification failed", apiResponse });

                MuzztechSendResponse? resObj = null;
                try { resObj = JsonSerializer.Deserialize<MuzztechSendResponse>(apiResponse); } catch { }

                if (resObj != null && !string.Equals(resObj.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = resObj.Details ?? "OTP verification failed", apiResponse });
                }

                return Ok(new
                {
                    success = true,
                    message = "OTP verified successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in VerifyOtp for SessionId {SessionId}", request.SessionId);
                return StatusCode(500, new { success = false, message = "Error verifying OTP: " + ex.Message });
            }
        }
    }
}
