using EcommerceAPI.Models;

namespace EcommerceService.Repository.Interface
{
    public interface ICategoryAdminRepository
    {
        List<CategoryDetail> GetAllFlat(bool activeOnly = false);
        List<CategoryDetail> GetTree(int? productTypeId = null, bool activeOnly = true);
        CategoryDetail GetById(int categoryId);
        int AddCategory(CategoryDetail category);
        bool UpdateCategory(CategoryDetail category);
        bool DeleteCategory(int categoryId); // soft delete, blocks if has active children
        bool HasChildren(int categoryId);
        int GetNextCategoryId(); // since category_id is NOT identity
        string UpdateCategoryImage(int categoryId, string imagePath);
        string UpdateCategoryBanner(int categoryId, string bannerPath);
    }
}