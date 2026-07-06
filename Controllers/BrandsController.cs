using EcommerceAPI.Models;
using EcommerceService.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandsController : ControllerBase
    {
        private readonly IBrandRepository _brandRepository;

        public BrandsController(IBrandRepository brandRepository)
        {
            _brandRepository = brandRepository;
        }

        // GET api/Brands?activeOnly=true
        [HttpGet]
        public IActionResult GetAllBrands([FromQuery] bool activeOnly = false)
        {
            var brands = _brandRepository.GetAllBrands(activeOnly);
            return Ok(new { success = true, data = brands });
        }

        // GET api/Brands/5
        [HttpGet("{id}")]
        public IActionResult GetBrandById(int id)
        {
            var brand = _brandRepository.GetBrandById(id);
            if (brand == null)
                return NotFound(new { success = false, message = "Brand not found." });

            return Ok(new { success = true, data = brand });
        }

        // POST api/Brands
        [HttpPost]
        public IActionResult AddBrand([FromBody] Brand brand)
        {
            if (brand == null || string.IsNullOrWhiteSpace(brand.BrandName))
                return BadRequest(new { success = false, message = "BrandName is required." });

            var newId = _brandRepository.AddBrand(brand);
            if (newId == 0)
                return StatusCode(500, new { success = false, message = "Failed to create brand." });

            return Ok(new { success = true, message = "Brand created successfully.", brandId = newId });
        }

        // PUT api/Brands/5
        [HttpPut("{id}")]
        public IActionResult UpdateBrand(int id, [FromBody] Brand brand)
        {
            if (brand == null)
                return BadRequest(new { success = false, message = "Brand data is required." });

            brand.BrandId = id;
            var result = _brandRepository.UpdateBrand(brand);

            if (!result)
                return NotFound(new { success = false, message = "Brand not found or update failed." });

            return Ok(new { success = true, message = "Brand updated successfully." });
        }

        // DELETE api/Brands/5  (soft delete)
        [HttpDelete("{id}")]
        public IActionResult DeleteBrand(int id)
        {
            var result = _brandRepository.DeleteBrand(id);
            if (!result)
                return NotFound(new { success = false, message = "Brand not found." });

            return Ok(new { success = true, message = "Brand deleted successfully." });
        }

        [HttpPost("{id}/UploadLogo")]
        [ApiExplorerSettings(IgnoreApi = true)]

        [RequestSizeLimit(5_000_000)]
        public async Task<IActionResult> UploadLogo(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Logo file is required." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { success = false, message = "Invalid file type." });

            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "brands");
            if (!Directory.Exists(uploadsRoot))
                Directory.CreateDirectory(uploadsRoot);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var fileName = $"brand_{id}_{timestamp}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            var relativePath = $"/uploads/brands/{fileName}";
            _brandRepository.UpdateBrandLogo(id, relativePath);

            return Ok(new { success = true, message = "Brand logo uploaded.", path = relativePath });
        }
    }
}