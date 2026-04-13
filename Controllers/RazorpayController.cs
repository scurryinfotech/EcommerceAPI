using Microsoft.AspNetCore.Mvc;
using EcommerceService.Repository.Interface;
using Razorpay.Api;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EcommerceAPI.Models;

namespace EcommerceService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RazorpayController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ICategoryRepository _categoryRepository;

        public RazorpayController(IConfiguration config, ICategoryRepository categoryRepository)
        {
            _config = config;
            _categoryRepository = categoryRepository;
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/Razorpay/CreateOrder
        // Creates a Razorpay order and logs the payment attempt in DB
        // ─────────────────────────────────────────────────────────────
        [HttpPost("CreateOrder")]
        public IActionResult CreateOrder([FromBody] CreateOrderRequest req)
        {
            try
            {
                if (req == null)
                    return BadRequest(new { message = "Request body is required." });

                if (string.IsNullOrWhiteSpace(req.OrderNumber))
                    return BadRequest(new { message = "OrderNumber is required." });

                // Ensure we have a valid DB OrderId. If frontend passed 0, try to resolve by OrderNumber.
                if (req.OrderId <= 0)
                {
                    int resolved = _categoryRepository.GetOrderIdByOrderNumber(req.OrderNumber);
                    if (resolved <= 0)
                        return BadRequest(new { message = "A valid DB OrderId is required. Ensure PlaceOrder ran before CreateOrder." });

                    req.OrderId = resolved;
                }

                string keyId = _config["Razorpay:KeyId"];
                string keySecret = _config["Razorpay:KeySecret"];

                if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
                    return StatusCode(500, new { message = "Razorpay credentials are not configured on the server." });

                // Create order on Razorpay
                RazorpayClient client = new RazorpayClient(keyId, keySecret);
                Dictionary<string, object> options = new Dictionary<string, object>
                {
                    { "amount",          (int)(req.Amount * 100) }, // paise
                    { "currency",        "INR" },
                    { "receipt",         req.OrderNumber },
                    { "payment_capture", 1 }
                };
                Order rzpOrder = client.Order.Create(options);
                string razorpayOrderId = rzpOrder["id"].ToString();

                // Log payment attempt in DB
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                string userAgent = Request.Headers["User-Agent"].ToString();

                _categoryRepository.InsertPaymentTransaction(
                    req.OrderId, req.OrderNumber, razorpayOrderId,
                    req.Amount, ip, userAgent);

                return Ok(new
                {
                    razorpayOrderId,
                    amount = req.Amount,
                    currency = "INR",
                    keyId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/Razorpay/VerifyPayment
        // Verifies HMAC signature and updates DB
        // ─────────────────────────────────────────────────────────────
        [HttpPost("VerifyPayment")]
        public IActionResult VerifyPayment([FromBody] VerifyPaymentRequest req)
        {
            try
            {
                string keySecret = _config["Razorpay:KeySecret"];

                // HMAC-SHA256 signature check
                string payload = req.RazorpayOrderId + "|" + req.RazorpayPaymentId;
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                string calcSig = BitConverter.ToString(hash).Replace("-", "").ToLower();

                if (calcSig == req.RazorpaySignature)
                {
                    // Try to get payment method from Razorpay (non-critical)
                    string paymentMethod = null;
                    try
                    {
                        RazorpayClient client = new RazorpayClient(_config["Razorpay:KeyId"], keySecret);
                        Payment payment = client.Payment.Fetch(req.RazorpayPaymentId);
                        paymentMethod = payment["method"]?.ToString();
                    }
                    catch { /* non-critical, ignore */ }

                    _categoryRepository.UpdatePaymentSuccess(
                        req.RazorpayOrderId, req.RazorpayPaymentId,
                        req.RazorpaySignature, paymentMethod,
                        JsonSerializer.Serialize(req));

                    return Ok(new { success = true, message = "Payment verified successfully." });
                }
                else
                {
                    _categoryRepository.UpdatePaymentFailed(
                        req.RazorpayOrderId, req.RazorpayPaymentId,
                        "Signature mismatch", "SIGNATURE_MISMATCH",
                        JsonSerializer.Serialize(req));

                    return BadRequest(new { success = false, message = "Payment verification failed." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpPost("PaymentFailed")]
        public IActionResult PaymentFailed([FromBody] PaymentFailedRequest req)
        {
            try
            {
                _categoryRepository.UpdatePaymentFailed(
                    req.RazorpayOrderId, req.RazorpayPaymentId,
                    req.FailureReason, req.FailureCode,
                    JsonSerializer.Serialize(req));

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


        // ─────────────────────────────────────────────────────────────
        [HttpPost("Webhook")]
        public async Task<IActionResult> Webhook()
        {
            string rawBody = string.Empty;
            using (var reader = new StreamReader(Request.Body))
                rawBody = await reader.ReadToEndAsync();

            string eventId = null;
            try
            {
                // Verify webhook signature
                string webhookSecret = _config["Razorpay:WebhookSecret"];
                string signature = Request.Headers["X-Razorpay-Signature"].ToString();

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
                string calcSig = BitConverter.ToString(hash).Replace("-", "").ToLower();

                if (calcSig != signature)
                    return BadRequest("Invalid webhook signature.");

                // Parse event
                var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                eventId = root.GetProperty("id").GetString();
                string evType = root.GetProperty("event").GetString();

                var payEntity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
                string rzpOrderId = payEntity.TryGetProperty("order_id", out var oid) ? oid.GetString() : null;
                string rzpPaymentId = payEntity.TryGetProperty("id", out var pid) ? pid.GetString() : null;
                decimal amount = payEntity.TryGetProperty("amount", out var amt) ? amt.GetDecimal() / 100 : 0;

                string rzpRefundId = null;
                if (root.GetProperty("payload").TryGetProperty("refund", out var refEl))
                    rzpRefundId = refEl.GetProperty("entity").GetProperty("id").GetString();

                // Log webhook (duplicate-safe — SP ignores if EventId already exists)
                _categoryRepository.InsertWebhookLog(
                    eventId, evType, rzpOrderId, rzpPaymentId, rzpRefundId, amount, rawBody);

                // Handle event
                switch (evType)
                {
                    case "payment.captured":
                        _categoryRepository.UpdatePaymentSuccess(
                            rzpOrderId, rzpPaymentId, null, null, rawBody);
                        break;

                    case "payment.failed":
                        string reason = payEntity.TryGetProperty("error_description", out var ed) ? ed.GetString() : null;
                        string code = payEntity.TryGetProperty("error_code", out var ec) ? ec.GetString() : null;
                        _categoryRepository.UpdatePaymentFailed(
                            rzpOrderId, rzpPaymentId, reason, code, rawBody);
                        break;
                }

                _categoryRepository.UpdateWebhookProcessed(eventId, null);
                return Ok();
            }
            catch (Exception ex)
            {
                if (eventId != null)
                    _categoryRepository.UpdateWebhookProcessed(eventId, ex.Message);

                return StatusCode(500, new { message = ex.Message });
            }
        }
    }

    

}