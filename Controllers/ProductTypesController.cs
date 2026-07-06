//using EcommerceAPI.Models;
//using EcommerceService.Repository.Interface;
//using Microsoft.AspNetCore.Mvc;

//namespace EcommerceService.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class ProductTypesController : ControllerBase
//    {
//        private readonly IProductTypeRepository _typeRepository;

//        public ProductTypesController(IProductTypeRepository typeRepository)
//        {
//            _typeRepository = typeRepository;
//        }

//        [HttpGet]
//        public IActionResult GetAll([FromQuery] bool activeOnly = false)
//        {
//            var types = _typeRepository.GetAllProductTypes(activeOnly);
//            return Ok(new { success = true, data = types });
//        }

//        [HttpGet("{id}")]
//        public IActionResult GetById(int id)
//        {
//            var type = _typeRepository.GetProductTypeById(id);
//            if (type == null)
//                return NotFound(new { success = false, message = "Product type not found." });

//            return Ok(new { success = true, data = type });
//        }

//        [HttpPost]
//        public IActionResult Add([FromBody] ProductType type)
//        {
//            if (type == null || string.IsNullOrWhiteSpace(type.ProductTypeName))
//                return BadRequest(new { success = false, message = "ProductTypeName is required." });

//            var newId = _typeRepository.AddProductType(type);
//            if (newId == 0)
//                return StatusCode(500, new { success = false, message = "Failed to create product type." });

//            return Ok(new { success = true, message = "Product type created.", productTypeId = newId });
//        }

//        [HttpPut("{id}")]
//        public IActionResult Update(int id, [FromBody] ProductType type)
//        {
//            if (type == null)
//                return BadRequest(new { success = false, message = "Data is required." });

//            type.ProductTypeId = id;
//            var result = _typeRepository.UpdateProductType(type);

//            if (!result)
//                return NotFound(new { success = false, message = "Product type not found or update failed." });

//            return Ok(new { success = true, message = "Product type updated." });
//        }

//        [HttpDelete("{id}")]
//        public IActionResult Delete(int id)
//        {
//            var result = _typeRepository.DeleteProductType(id);
//            if (!result)
//                return NotFound(new { success = false, message = "Product type not found." });

//            return Ok(new { success = true, message = "Product type deleted." });
//        }
//    }
//}