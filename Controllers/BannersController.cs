using EcommerceAPI.Repository.Interface;
using EcommerceService.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BannersController : ControllerBase
    {
        private readonly IBannerRepository _bannerRepository;

        public BannersController(IBannerRepository bannerRepository)
        {
            _bannerRepository = bannerRepository;
        }

        // GET api/Banners
        [HttpGet]
        public IActionResult GetActiveBanners()
        {
            var banners = _bannerRepository.GetActiveBanners();
            return Ok(new { success = true, data = banners });
        }
    }
}