using EcommerceAPI.Models;
using EcommerceService.Models;

namespace EcommerceService.Repository.Interface
{
    public interface ICategoryRepository
    {
        // Existing
        List<Category> GetCategories();
        List<Product> GetProducts();
        List<ProductVariant> GetProductsVariants(int id, List<ProductVariant>? productVariants);
        bool PlaceOrder(OrderRequest order);

        int InsertPaymentTransaction(int orderId, string orderNumber, string razorpayOrderId, decimal amount, string ipAddress, string userAgent);
        // Returns OrderId for a given OrderNumber, or 0 if not found
        int GetOrderIdByOrderNumber(string orderNumber);
        bool UpdatePaymentSuccess(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature, string paymentMethod, string rawResponse);
        bool UpdatePaymentFailed(string razorpayOrderId, string razorpayPaymentId, string failureReason, string failureCode, string rawResponse);
        bool InsertWebhookLog(string eventId, string eventType, string razorpayOrderId, string razorpayPaymentId, string razorpayRefundId, decimal amount, string rawPayload);
        bool UpdateWebhookProcessed(string eventId, string errorMessage);
        List<ProductImage> GetProductImages(int productId);

        int AddProductImage(int productId, string imagePath, int displayOrder, bool isMain);
        bool ProductHasMainImage(int productId);

        List<Category> GetActiveCategoriesOrdered();
        OrderStatusResponse GetOrderStatus(string orderNumber, string email);
        List<ProductCard> SearchProducts(string keyword, int top = 50);
        ProductDetailPage GetProductDetailPage(int productId);


    }
}