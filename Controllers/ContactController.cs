// ============================================================
// Controllers/ContactController.cs
// ============================================================
using EcommerceAPI.Models;
using EcommerceAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceAPI.Controllers
{
    public class ContactController : Controller
    {
        private readonly IContactRepository _contactRepo;
        private readonly ILogger<ContactController> _logger;

        public ContactController(IContactRepository contactRepo,
                                 ILogger<ContactController> logger)
        {
            _contactRepo = contactRepo;
            _logger = logger;
        }

        // GET: /Contact
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Contact/Submit  — called via jQuery $.ajax
        [HttpPost]
        public async Task<IActionResult> Submit([FromForm] ContactModel model)
        {
            // 1. Server-side ModelState validation
           
            // 2. Save via Repository
            var saved = await _contactRepo.SaveContactAsync(model);

            if (saved)
            {
               

                return Json(new
                {
                    success = true,
                    message = "Thank you for contacting us! We will get back to you shortly."
                });
            }

      
            return Json(new
            {
                success = false,
                message = "Something went wrong. Please try again."
            });
        }

        // GET: /Contact/GetAll  — optional: view all submissions
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var contacts = await _contactRepo.GetAllContactsAsync();
            return Json(new { success = true, data = contacts });
        }

        // GET: /Contact/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var contact = await _contactRepo.GetContactByIdAsync(id);

            if (contact == null)
                return Json(new { success = false, message = "Contact not found." });

            return Json(new { success = true, data = contact });
        }
    }
}