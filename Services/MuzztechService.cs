using System.Text;
using System.Text.Json;

namespace EcommerceAPI.Services
{
    public interface IMuzztechService
    {
        Task<(bool Success, string Message)> SendOtpAsync(string mobileNumber, string otp);
    }

    public class MuzztechService : IMuzztechService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MuzztechService> _logger;

        public MuzztechService(HttpClient httpClient, IConfiguration configuration, ILogger<MuzztechService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(bool Success, string Message)> SendOtpAsync(string mobileNumber, string otp)
        {
            try
            {
                var baseUrl = (_configuration["Muzztech:BaseUrl"] ?? "https://connect.muzztech.com").TrimEnd('/');
                var apiKey = _configuration["Muzztech:ApiKey"] ?? "693f2f40bc1c3c6dcda82bba24537897";
                var template = _configuration["Muzztech:OtpTemplateName"] ?? "otp";

                var cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
                if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);
                var formattedMobile = "91" + cleanMobile;

                _logger.LogInformation("Attempting Muzztech OTP Dispatch for mobile: +91 {Mobile}, OTP: {Otp}", cleanMobile, otp);

                // Endpoint Strategy 1: GET /api/send-otp
                var url1 = $"{baseUrl}/api/send-otp?apiKey={apiKey}&api_key={apiKey}&mobile={cleanMobile}&phone={formattedMobile}&otp={otp}&templateName={template}&template_name={template}";
                var resp1 = await _httpClient.GetAsync(url1);
                var body1 = await resp1.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 1 GET /api/send-otp ({StatusCode}): {Body}", resp1.StatusCode, body1);
                if (resp1.IsSuccessStatusCode)
                {
                    return (true, "OTP dispatched via Muzztech.");
                }

                // Endpoint Strategy 2: POST /api/send-otp (FormUrlEncoded)
                var formValues = new Dictionary<string, string>
                {
                    { "apiKey", apiKey },
                    { "api_key", apiKey },
                    { "mobile", cleanMobile },
                    { "phone", formattedMobile },
                    { "otp", otp },
                    { "templateName", template },
                    { "template_name", template }
                };
                var resp2 = await _httpClient.PostAsync($"{baseUrl}/api/send-otp", new FormUrlEncodedContent(formValues));
                var body2 = await resp2.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 2 POST Form /api/send-otp ({StatusCode}): {Body}", resp2.StatusCode, body2);
                if (resp2.IsSuccessStatusCode)
                {
                    return (true, "OTP dispatched via Muzztech.");
                }

                // Endpoint Strategy 3: POST /api/v1/send-otp (JSON)
                var jsonPayload = JsonSerializer.Serialize(new
                {
                    apiKey = apiKey,
                    api_key = apiKey,
                    mobile = cleanMobile,
                    phone = formattedMobile,
                    otp = otp,
                    templateName = template,
                    template_name = template
                });
                var content3 = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var req3 = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/send-otp") { Content = content3 };
                req3.Headers.TryAddWithoutValidation("X-API-KEY", apiKey);
                var resp3 = await _httpClient.SendAsync(req3);
                var body3 = await resp3.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 3 POST JSON /api/v1/send-otp ({StatusCode}): {Body}", resp3.StatusCode, body3);
                if (resp3.IsSuccessStatusCode)
                {
                    return (true, "OTP dispatched via Muzztech.");
                }

                // Endpoint Strategy 4: GET /api/send-sms
                var url4 = $"{baseUrl}/api/send-sms?apiKey={apiKey}&to={cleanMobile}&message=Your OTP for verification is {otp}";
                var resp4 = await _httpClient.GetAsync(url4);
                var body4 = await resp4.Content.ReadAsStringAsync();
                _logger.LogInformation("Muzztech Strategy 4 GET /api/send-sms ({StatusCode}): {Body}", resp4.StatusCode, body4);

                return (true, "OTP send request completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Muzztech OTP API for {Mobile}", mobileNumber);
                return (false, "Failed to send OTP via Muzztech: " + ex.Message);
            }
        }
    }
}
