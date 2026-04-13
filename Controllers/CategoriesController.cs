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

        // GET api/Categories/categories
        [HttpGet("categories")]
        public ActionResult<IEnumerable<Category>> GetAllCategories()
        {
            var categories = _categoryRepository.GetCategories();
            return Ok(categories);
        }

        // GET api/Categories/products
        [HttpGet("products")]
        public ActionResult<IEnumerable<Product>> GetProducts()
        {
            var products = _categoryRepository.GetProducts();
            return Ok(products);
        }

        // GET api/Categories/productVariants?id=101
        [HttpGet("productVariants")]
        public ActionResult<IEnumerable<ProductVariant>> GetProductsVariants(int id)
        {
            var productVariants = _categoryRepository.GetProductsVariants(id, null);
            return Ok(productVariants);
        }

        // POST api/Categories/placeOrder
        // Now returns orderId + orderNumber so Razorpay flow can use them
        [HttpPost("placeOrder")]
        public IActionResult PlaceOrder([FromBody] OrderRequest order)
        {
            if (order == null || order.Items == null || !order.Items.Any())
                return BadRequest(new { success = false, message = "Order data is required." });

            var result = _categoryRepository.PlaceOrder(order);

            if (result)
                return Ok(new
                {
                    success = true,
                    message = "Order placed successfully.",
                    orderId = order.DbOrderId,        // DB-generated OrderId
                    orderNumber = order.OrderNumber       // for display to user
                });
            else
                return StatusCode(500, new { success = false, message = "Failed to place order." });
        }
    }
}