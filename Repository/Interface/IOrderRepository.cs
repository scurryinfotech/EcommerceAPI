using EcommerceAPI.Models;

namespace EcommerceAPI.Repository.Interface
{
    public interface IOrderRepository
    {
        (bool Success, string Message, OrderDto? Order) PlaceOrder(PlaceOrderRequest request);
        List<OrderDto> GetCustomerOrders(int userId);
        OrderDto? GetOrderDetails(int orderId, int? userId = null);
        OrderDto? GetOrderByNumber(string orderNumber);
        List<OrderDto> GetAllOrdersAdmin(string? status = null);
        bool UpdateOrderStatus(UpdateOrderStatusRequest request);
        List<OrderStatusHistoryDto> GetOrderTimeline(int orderId);
    }
}
