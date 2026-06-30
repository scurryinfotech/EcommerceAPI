using EcommerceAPI.Models;

namespace EcommerceAPI.Repository.Interface
{
    public interface IHomeRepository
    {
        List<ProductCard> GetFeaturedProducts(int top = 20);
        List<ProductCard> GetTrendingProducts(int top = 20);
        List<ProductCard> GetNewArrivals(int top = 20);
        List<ProductCard> GetProductsByCategory(int categoryId, int top = 50);
        List<ProductCard> GetProductsByBrand(int brandId, int top = 50);
    }
}
