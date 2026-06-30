using EcommerceAPI.Models;

namespace EcommerceService.Repository.Interface
{
    public interface IBrandRepository
    {
        List<Brand> GetAllBrands(bool activeOnly = false);
        Brand GetBrandById(int brandId);
        int AddBrand(Brand brand);
        bool UpdateBrand(Brand brand);
        bool DeleteBrand(int brandId); // soft delete
        string UpdateBrandLogo(int brandId, string logoPath);
    }
}