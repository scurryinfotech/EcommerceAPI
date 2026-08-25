using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using EcommerceAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _authRepo;
        private readonly IOtpService _otpService;

        public AuthController(IAuthRepository authRepo, IOtpService otpService)
        {
            _authRepo = authRepo;
            _otpService = otpService;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = _authRepo.RegisterCustomer(request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = _authRepo.LoginCustomer(request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var res = await _otpService.SendOtpAsync(request.MobileNumber);
            if (!res.Success)
                return BadRequest(new { success = false, message = res.Message });

            return Ok(new { success = true, message = res.Message, sessionId = res.SessionId });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Verify OTP via Muzztech 2FA API (uses stored session from DB automatically if SessionId is omitted)
            var verifyRes = await _otpService.VerifyOtpAsync(request.MobileNumber, request.Otp, request.SessionId);
            if (!verifyRes.Success)
            {
                return BadRequest(new AuthResult { Success = false, Message = verifyRes.Message, IsMobileVerified = false });
            }

            _authRepo.MarkMobileVerified(request.MobileNumber);
            var user = _authRepo.GetUserByMobile(request.MobileNumber);

            return Ok(new AuthResult
            {
                Success = true,
                Message = "OTP verified successfully.",
                IsMobileVerified = true,
                User = user,
                Customer = user != null ? new CustomerProfile
                {
                    CustomerId = user.UserId,
                    FullName = user.FullName,
                    MobileNumber = user.MobileNumber,
                    Email = user.Email,
                    IsMobileVerified = true,
                    CreatedDate = DateTime.Now
                } : null
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = _authRepo.GetUserByMobile(request.MobileNumber);
            if (user == null)
            {
                return Ok(new { success = true, message = "If that mobile number is registered, an OTP has been sent." });
            }

            var otpRes = await _otpService.SendOtpAsync(request.MobileNumber);
            if (!otpRes.Success)
            {
                return StatusCode(502, new { success = false, message = otpRes.Message });
            }

            return Ok(new { success = true, message = "OTP sent to your registered mobile number.", sessionId = otpRes.SessionId });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var verifyRes = await _otpService.VerifyOtpAsync(request.MobileNumber, request.Otp, request.SessionId);
            if (!verifyRes.Success)
            {
                return BadRequest(new { success = false, message = verifyRes.Message });
            }

            var result = _authRepo.ResetPassword(request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("recover-account")]
        public async Task<IActionResult> RecoverAccount([FromBody] VerifyOtpRequest request)
        {
            var verifyRes = await _otpService.VerifyOtpAsync(request.MobileNumber, request.Otp, request.SessionId);
            if (!verifyRes.Success)
                return BadRequest(new { success = false, message = verifyRes.Message });

            _authRepo.MarkMobileVerified(request.MobileNumber);
            var user = _authRepo.GetUserByMobile(request.MobileNumber);
            return Ok(new { success = true, message = "Account verified.", user });
        }
    }
}
