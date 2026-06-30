using EcommerceAPI.Models;

namespace EcommerceAPI.Repository.Interface
{
    public interface IBannerRepository
    {
        List<Banner> GetActiveBanners();
    }
}
