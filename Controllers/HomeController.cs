using EcommerceAPI.Repository.Interface;
using EcommerceService.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly IHomeRepository _homeRepository;

        public HomeController(IHomeRepository homeRepository)
        {
            _homeRepository = homeRepository;
        }

        // GET api/Home/featured?top=20
        [HttpGet("featured")]
        public IActionResult GetFeatured([FromQuery] int top = 20)
        {
            var products = _homeRepository.GetFeaturedProducts(top);
            return Ok(new { success = true, data = products });
        }

        // GET api/Home/trending?top=20
        [HttpGet("trending")]
        public IActionResult GetTrending([FromQuery] int top = 20)
        {
            var products = _homeRepository.GetTrendingProducts(top);
            return Ok(new { success = true, data = products });
        }

        // GET api/Home/newarrivals?top=20
        [HttpGet("newarrivals")]
        public IActionResult GetNewArrivals([FromQuery] int top = 20)
        {
            var products = _homeRepository.GetNewArrivals(top);
            return Ok(new { success = true, data = products });
        }

        // GET api/Home/category/3?top=50
        [HttpGet("category/{categoryId}")]
        public IActionResult GetByCategory(int categoryId, [FromQuery] int top = 50)
        {
            var products = _homeRepository.GetProductsByCategory(categoryId, top);
            return Ok(new { success = true, data = products });
        }

        // GET api/Home/brand/2?top=50
        [HttpGet("brand/{brandId}")]
        public IActionResult GetByBrand(int brandId, [FromQuery] int top = 50)
        {
            var products = _homeRepository.GetProductsByBrand(brandId, top);
            return Ok(new { success = true, data = products });
        }
    }
}   