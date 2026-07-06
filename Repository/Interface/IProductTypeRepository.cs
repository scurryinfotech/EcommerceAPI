using EcommerceAPI.Models;

namespace EcommerceService.Repository.Interface
{
    public interface IProductTypeRepository
    {
        List<ProductType> GetAllProductTypes(bool activeOnly = false);
        ProductType GetProductTypeById(int id);
        int AddProductType(ProductType type);
        bool UpdateProductType(ProductType type);
        bool DeleteProductType(int id);
    }
}