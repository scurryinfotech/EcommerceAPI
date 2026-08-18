using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _authRepo;

        public AuthController(IAuthRepository authRepo)
        {
            _authRepo = authRepo;
        }

        [HttpPost("send-otp")]
        public IActionResult SendOtp([FromBody] SendOtpRequest request)
        {
            var res = _authRepo.SendOtp(request.MobileNumber, request.Purpose);
            if (!res.Success)
                return BadRequest(new { success = false, message = res.Message });

            return Ok(new { success = true, message = res.Message, debugOtp = res.OtpForDebug });
        }

        [HttpPost("verify-otp")]
        public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            var res = _authRepo.VerifyOtp(request.MobileNumber, request.Otp, request.Purpose);
            if (!res.Success)
                return BadRequest(new { success = false, message = res.Message });

            return Ok(new { success = true, message = res.Message, user = res.User });
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            var res = _authRepo.RegisterUser(request);
            if (!res.Success)
                return BadRequest(new { success = false, message = res.Message });

            return Ok(new { success = true, message = res.Message, user = res.User });
        }

        [HttpPost("recover-account")]
        public IActionResult RecoverAccount([FromBody] VerifyOtpRequest request)
        {
            var res = _authRepo.VerifyOtp(request.MobileNumber, request.Otp, "AccountRecovery");
            if (!res.Success)
                return BadRequest(new { success = false, message = res.Message });

            var user = _authRepo.GetUserByMobile(request.MobileNumber);
            return Ok(new { success = true, message = "Account verified.", user });
        }
    }
}
