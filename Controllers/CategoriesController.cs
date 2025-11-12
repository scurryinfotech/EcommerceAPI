using Microsoft.AspNetCore.Mvc;
using EcommerceService.Repository.Interface;
using EcommerceAPI.Models;
using EcommerceService.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Http.HttpResults;

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

        [HttpGet("productVariants")]
        public ActionResult<IEnumerable<ProductVariant>> GetProductsVariants( int id)
       {
                     var productVariants = _categoryRepository.GetProductsVariants(id, null);
                        return Ok(productVariants); 
        }

        [HttpPost("placeOrder")]
        public IActionResult PlaceOrder([FromBody] OrderRequest order)
        {
            if (order == null || order.Items == null || !order.Items.Any())
                return BadRequest("Order data is required.");

            var result = _categoryRepository.PlaceOrder(order);

            if (result)
                return Ok(new { success = true, message = "Order placed successfully." });
            else
                return StatusCode(500, new { success = false, message = "Failed to place order." });
        }


    }

}
