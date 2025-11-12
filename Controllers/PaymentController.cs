using EcommerceAPI.Models;
using EcommerceService.Models;
using EcommerceService.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceService.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentRepository _paymentRepo;

        public PaymentController(IPaymentRepository paymentRepo)
        {
            _paymentRepo = paymentRepo;
        }

        [HttpPost]
        public async Task<IActionResult> InitiatePhonePePayments([FromBody] OrderRequest order)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var response = await _paymentRepo.InitiatePayment(order, baseUrl);
            return Content(response, "application/json");
        }

        [HttpPost]
        public IActionResult Callback([FromBody] OrderRequest order)
        {
            bool success = _paymentRepo.SaveOrderAfterPayment(order, "Success");
            return Ok(new { message = success ? "Order placed after payment success" : "Payment failed" });
        }

        [HttpPost]
        public IActionResult PlaceCODOrder([FromBody] OrderRequest order)
        {
            bool success = _paymentRepo.SaveOrderAfterPayment(order, "Pending");
            return Ok(new { message = success ? "Order placed via COD" : "Error placing COD order" });
        }
    }
}
