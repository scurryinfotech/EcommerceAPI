using EcommerceAPI.Models;
using EcommerceService.Models;

namespace EcommerceService.Repository.Interface
{
    public interface ICategoryRepository
    {
        List<Category> GetCategories();
        List<Product> GetProducts();
        List <ProductVariant> GetProductsVariants(int id, List<ProductVariant>? productVariants);
        bool PlaceOrder(OrderRequest order);
    }
}
