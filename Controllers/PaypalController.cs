using EcommerceAPI.Models;
using EcommerceService.Repository.Service;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

[Route("api/[controller]")]
[ApiController]
public class PaypalController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CategoryRepository _repo;

    public PaypalController(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        CategoryRepository repo)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _repo = repo;
    }

    private async Task<string> GetAccessToken()
    {
        var clientId = _config["PayPal:ClientId"];
        var secret = _config["PayPal:ClientSecret"];

        var authToken = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

        var client = _httpClientFactory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", authToken);

        var body = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" }
            });

        var response = await client.PostAsync(
            "https://api-m.sandbox.paypal.com/v1/oauth2/token",
            body);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(json);

        return JObject.Parse(json)["access_token"]?.ToString();
    }

    [HttpPost("CreateOrder")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] PaypalCreateOrderRequest request)
    {
        try
        {
            var token = await GetAccessToken();

            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = request.OrderNumber,
                        amount = new
                        {
                            currency_code = "USD",
                            value = request.Amount.ToString("0.00")
                        }
                    }
                }
            };

            var response = await client.PostAsync(
                "https://api-m.sandbox.paypal.com/v2/checkout/orders",
                new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"));

            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return BadRequest(result);

            var order = JObject.Parse(result);

            string paypalOrderId = order["id"]?.ToString();

            //_repo.InsertPaypalTransaction(
            //    request.DbOrderId,
            //    request.OrderNumber,
            //    paypalOrderId,
            //    request.Amount);
            //Console.WriteLine("PaypalOrderId = " + request.PaypalOrderId);
            return Ok(new
            {
                success = true,
                id = paypalOrderId
            });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("CaptureOrder")]
    public async Task<IActionResult> CaptureOrder(
        [FromBody] PaypalCaptureRequest request)
    {
        try
        {
            var token = await GetAccessToken();

            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var content = new StringContent(
    "{}",
    Encoding.UTF8,
    "application/json");

            var response = await client.PostAsync(
                $"https://api-m.sandbox.paypal.com/v2/checkout/orders/{request.PaypalOrderId}/capture",
                content);

            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Status: " + response.StatusCode);
            Console.WriteLine("Response: " + result);


            if (!response.IsSuccessStatusCode)
            {
                _repo.UpdatePaypalFailed(
                    request.PaypalOrderId,
                    result);

                return BadRequest(result);
            }

            var json = JObject.Parse(result);

            string captureId =
                json["purchase_units"]?[0]?["payments"]?["captures"]?[0]?["id"]?.ToString();

            _repo.UpdatePaypalSuccess(
                request.PaypalOrderId,
                captureId,
                result);

            return Ok(new
            {
                success = true,
                paypalOrderId = request.PaypalOrderId,
                paypalCaptureId = captureId
            });
        }
        catch (Exception ex)
        {
            _repo.UpdatePaypalFailed(
                request.PaypalOrderId,
                ex.Message);

            return BadRequest(ex.Message);
        }
    }
}