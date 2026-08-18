using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderRepository _orderRepo;

        public OrdersController(IOrderRepository orderRepo)
        {
            _orderRepo = orderRepo;
        }

        [HttpPost]
        public IActionResult PlaceOrder([FromBody] PlaceOrderRequest request)
        {
            var res = _orderRepo.PlaceOrder(request);
            if (!res.Success)
                return BadRequest(new { success = false, message = res.Message });

            return Ok(new { success = true, message = res.Message, order = res.Order });
        }

        [HttpGet("user/{userId}")]
        public IActionResult GetCustomerOrders(int userId)
        {
            var orders = _orderRepo.GetCustomerOrders(userId);
            return Ok(new { success = true, orders });
        }

        [HttpGet("{id}")]
        public IActionResult GetOrderDetails(int id, [FromQuery] int? userId)
        {
            var order = _orderRepo.GetOrderDetails(id, userId);
            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

            return Ok(new { success = true, order });
        }

        [HttpGet("number/{orderNumber}")]
        public IActionResult GetOrderByNumber(string orderNumber)
        {
            var order = _orderRepo.GetOrderByNumber(orderNumber);
            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

            return Ok(new { success = true, order });
        }

        // --- Admin Endpoints ---

        [HttpGet("admin/all")]
        public IActionResult GetAllOrdersAdmin([FromQuery] string? status)
        {
            var orders = _orderRepo.GetAllOrdersAdmin(status);
            return Ok(new { success = true, orders });
        }

        [HttpPut("admin/status")]
        public IActionResult UpdateOrderStatus([FromBody] UpdateOrderStatusRequest request)
        {
            bool ok = _orderRepo.UpdateOrderStatus(request);
            if (!ok)
                return BadRequest(new { success = false, message = "Failed to update order status." });

            var updatedOrder = _orderRepo.GetOrderDetails(request.OrderId);
            return Ok(new { success = true, message = "Order status updated successfully.", order = updatedOrder });
        }

        [HttpGet("timeline/{orderId}")]
        public IActionResult GetOrderTimeline(int orderId)
        {
            var timeline = _orderRepo.GetOrderTimeline(orderId);
            return Ok(new { success = true, timeline });
        }
    }
}
