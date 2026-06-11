using EcommerceAPI.Models;
using EcommerceAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IContactRepository _contactRepo;
        private readonly ILogger<ContactController> _logger;

        public ContactController(
            IContactRepository contactRepo,
            ILogger<ContactController> logger)
        {
            _contactRepo = contactRepo;
            _logger = logger;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] ContactModel model)
        {
            var saved = await _contactRepo.SaveContactAsync(model);

            if (saved)
            {
                return Ok(new
                {
                    success = true,
                    message = "Thank you for contacting us!"
                });
            }

            return BadRequest(new
            {
                success = false,
                message = "Something went wrong."
            });
        }

        //[HttpGet("all")]
        //public async Task<IActionResult> GetAll()
        //{
        //    var contacts = await _contactRepo.GetAllContactsAsync();
        //    return Ok(new { success = true, data = contacts });
        //}

        //[HttpGet("{id}")]
        //public async Task<IActionResult> GetById(int id)
        //{
        //    var contact = await _contactRepo.GetContactByIdAsync(id);

        //    if (contact == null)
        //        return NotFound(new
        //        {
        //            success = false,
        //            message = "Contact not found."
        //        });

        //    return Ok(new { success = true, data = contact });
        //}
    }
}