using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAuthRepository _authRepo;

        public AccountController(IAuthRepository authRepo)
        {
            _authRepo = authRepo;
        }

        [HttpGet("profile/{userId}")]
        public IActionResult GetProfile(int userId)
        {
            var user = _authRepo.GetUserById(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            return Ok(new { success = true, user });
        }

        [HttpPut("profile")]
        public IActionResult UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            bool updated = _authRepo.UpdateUserProfile(request);
            if (!updated)
                return BadRequest(new { success = false, message = "Failed to update profile." });

            var user = _authRepo.GetUserById(request.UserId);
            return Ok(new { success = true, message = "Profile updated successfully.", user });
        }

        [HttpGet("addresses/{userId}")]
        public IActionResult GetAddresses(int userId)
        {
            var addresses = _authRepo.GetUserAddresses(userId);
            return Ok(new { success = true, addresses });
        }

        [HttpPost("addresses")]
        public IActionResult AddAddress([FromBody] UserAddressDto address)
        {
            int id = _authRepo.AddUserAddress(address);
            if (id <= 0)
                return BadRequest(new { success = false, message = "Failed to add address." });

            address.AddressId = id;
            return Ok(new { success = true, message = "Address added successfully.", address });
        }

        [HttpPut("addresses/{id}")]
        public IActionResult UpdateAddress(int id, [FromBody] UserAddressDto address)
        {
            address.AddressId = id;
            bool ok = _authRepo.UpdateUserAddress(address);
            if (!ok)
                return BadRequest(new { success = false, message = "Failed to update address." });

            return Ok(new { success = true, message = "Address updated successfully." });
        }

        [HttpDelete("addresses/{id}")]
        public IActionResult DeleteAddress(int id, [FromQuery] int userId)
        {
            bool ok = _authRepo.DeleteUserAddress(id, userId);
            if (!ok)
                return BadRequest(new { success = false, message = "Failed to delete address." });

            return Ok(new { success = true, message = "Address deleted successfully." });
        }

        [HttpPost("addresses/{id}/set-default")]
        public IActionResult SetDefaultAddress(int id, [FromQuery] int userId)
        {
            bool ok = _authRepo.SetDefaultAddress(id, userId);
            if (!ok)
                return BadRequest(new { success = false, message = "Failed to set default address." });

            return Ok(new { success = true, message = "Default address updated." });
        }
    }
}
