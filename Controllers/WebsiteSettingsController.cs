using EcommerceAPI.Models;
using EcommerceService.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsiteSettingsController : ControllerBase
    {
        private readonly IWebsiteSettingsRepository _settingsRepository;

        public WebsiteSettingsController(IWebsiteSettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        // GET api/WebsiteSettings
        [HttpGet]
        public IActionResult GetSettings()
        {
            var settings = _settingsRepository.GetSettings();

            if (settings == null)
                return Ok(new { success = true, data = (WebsiteSetting)null, message = "No settings configured yet." });

            return Ok(new { success = true, data = settings });
        }

        // PUT api/WebsiteSettings
        //[HttpPut]
        //public IActionResult UpdateSettings([FromBody] WebsiteSetting settings)
        //{
        //    if (settings == null)
        //        return BadRequest(new { success = false, message = "Settings data is required." });

        //    var result = _settingsRepository.UpdateSettings(settings);

        //    if (result)
        //        return Ok(new { success = true, message = "Website settings updated successfully." });
        //    else
        //        return StatusCode(500, new { success = false, message = "Failed to update website settings." });
        //}

        // POST api/WebsiteSettings/UploadLogo
        //[HttpPost("UploadLogo")]
        //[RequestSizeLimit(5_000_000)] // 5 MB cap for logo
        //public async Task<IActionResult> UploadLogo([FromForm] IFormFile file)
        //{
        //    var path = await SaveBrandingFile(file, "logo");
        //    if (path == null)
        //        return BadRequest(new { success = false, message = "Valid image file is required (.jpg, .jpeg, .png, .webp, .svg)." });

        //    _settingsRepository.UpdateLogo(path);

        //    return Ok(new { success = true, message = "Logo uploaded successfully.", path });
        //}

        //// POST api/WebsiteSettings/UploadFavicon
        //[HttpPost("UploadFavicon")]
        //[RequestSizeLimit(2_000_000)] // 2 MB cap for favicon
        //public async Task<IActionResult> UploadFavicon([FromForm] IFormFile file)
        //{
        //    var path = await SaveBrandingFile(file, "favicon");
        //    if (path == null)
        //        return BadRequest(new { success = false, message = "Valid image file is required (.ico, .png, .jpg, .jpeg, .svg)." });

        //    _settingsRepository.UpdateFavicon(path);

        //    return Ok(new { success = true, message = "Favicon uploaded successfully.", path });
        //}

        //// Shared save logic for logo/favicon — same upload convention as product images
        //private async Task<string> SaveBrandingFile(IFormFile file, string prefix)
        //{
        //    if (file == null || file.Length == 0)
        //        return null;

        //    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg", ".ico", ".gif" };
        //    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        //    if (!allowedExtensions.Contains(ext))
        //        return null;

        //    var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "branding");
        //    if (!Directory.Exists(uploadsRoot))
        //        Directory.CreateDirectory(uploadsRoot);

        //    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        //    var fileName = $"{prefix}_{timestamp}{ext}";
        //    var fullPath = Path.Combine(uploadsRoot, fileName);

        //    using (var stream = new FileStream(fullPath, FileMode.Create))
        //    {
        //        await file.CopyToAsync(stream);
        //    }

        //    return $"/uploads/branding/{fileName}";
        //}
    }
}