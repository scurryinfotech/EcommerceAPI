using EcommerceAPI.Models;
using EcommerceService.Models;
using EcommerceService.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoriesController(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        [HttpGet("categories")]
        public ActionResult<IEnumerable<Category>> GetAllCategories()
        {
            var categories = _categoryRepository.GetCategories();
            return Ok(categories);
        }

        
        [HttpGet("products")]
        public ActionResult<IEnumerable<Product>> GetProducts()
        {
            var products = _categoryRepository.GetProducts();
            return Ok(products);
        }
        [HttpGet("productImages")]
        public ActionResult<IEnumerable<ProductImage>> GetProductImages(int id)
        {
            var images = _categoryRepository.GetProductImages(id);
            return Ok(images);
        }

        [HttpPost("UploadProductImages")]
        [ApiExplorerSettings(IgnoreApi = true)]

        [RequestSizeLimit(50_000_000)] 
        public async Task<IActionResult> UploadProductImages(
                [FromForm] int productId,
                  [FromForm] List<IFormFile> files)
        {
            if (productId <= 0)
                return BadRequest(new { success = false, message = "Valid productId is required." });

            if (files == null || files.Count == 0)
                return BadRequest(new { success = false, message = "At least one file is required." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var savedImages = new List<object>();
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "images");

            if (!Directory.Exists(uploadsRoot))
                Directory.CreateDirectory(uploadsRoot);

            
            bool productHasMainImage = _categoryRepository.ProductHasMainImage(productId);
            bool isFirstImageOfBatch = !productHasMainImage;

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                    continue; 

                
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var fileName = $"product_{productId}_{timestamp}{ext}";
                var fullPath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/images/{fileName}";
                bool setAsMain = isFirstImageOfBatch; 

                var newImageId = _categoryRepository.AddProductImage(productId, relativePath, 0, setAsMain);

                savedImages.Add(new
                {
                    imageId = newImageId,
                    imagePath = relativePath,
                    isMain = setAsMain
                });

                isFirstImageOfBatch = false;
            }

            if (savedImages.Count == 0)
                return BadRequest(new { success = false, message = "No valid image files were uploaded." });

            return Ok(new
            {
                success = true,
                message = $"{savedImages.Count} image(s) uploaded successfully.",
                images = savedImages
            });
        }
        // GET api/Categories/productVariants?id=101
        [HttpGet("productVariants")]
        public ActionResult<IEnumerable<ProductVariant>> GetProductsVariants(int id)
        {
            var productVariants = _categoryRepository.GetProductsVariants(id, null);
            return Ok(productVariants);
        }


        [HttpGet("activeCategories")]
        public ActionResult<IEnumerable<Category>> GetActiveCategories()
        {
            var categories = _categoryRepository.GetActiveCategoriesOrdered();
            return Ok(categories);
        }


        [HttpGet("productDetail")]
        public IActionResult GetProductDetail([FromQuery] int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Valid product id is required." });

            var detail = _categoryRepository.GetProductDetailPage(id);

            if (detail == null)
                return NotFound(new { success = false, message = "Product not found." });

            return Ok(new { success = true, data = detail });
        }

        [HttpGet("search")]
        public IActionResult SearchProducts([FromQuery] string keyword, [FromQuery] int top = 50)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(new { success = false, message = "Search keyword is required." });

            var results = _categoryRepository.SearchProducts(keyword, top);
            return Ok(new { success = true, data = results });
        }


        [HttpGet("orderStatus")]
        public IActionResult GetOrderStatus([FromQuery] string orderNumber, [FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(email))
                return BadRequest(new { success = false, message = "Order number and email are required." });

            var order = _categoryRepository.GetOrderStatus(orderNumber, email);

            if (order == null)
                return NotFound(new { success = false, message = "No order found with the given order number and email." });

            return Ok(new { success = true, data = order });
        }
        [HttpPost("placeOrder")]
        public IActionResult PlaceOrder([FromBody] OrderRequest order)
        {
            if (order == null || order.Items == null || !order.Items.Any())
                return BadRequest(new { success = false, message = "Order data is required." });

            // Only allow COD through this endpoint
            if (order.PaymentMode != null && order.PaymentMode.ToLower() == "razorpay")
                return BadRequest(new { success = false, message = "Use /api/Razorpay/VerifyAndPlaceOrder for online payments." });

            var result = _categoryRepository.PlaceOrder(order);

            if (result)
                return Ok(new
                {
                    success = true,
                    message = "Order placed successfully.",
                    orderId = order.DbOrderId,
                    orderNumber = order.OrderNumber
                });
            else
                return StatusCode(500, new { success = false, message = "Failed to place order." });
        }
    }
}