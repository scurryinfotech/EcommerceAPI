using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using EcommerceAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerAuthController : ControllerBase
    {
        private readonly IAuthRepository _repo;
        private readonly IOtpService _otpService;
        private readonly ILogger<CustomerAuthController> _logger;

        public CustomerAuthController(
            IAuthRepository repo,
            IOtpService otpService,
            ILogger<CustomerAuthController> logger)
        {
            _repo = repo;
            _otpService = otpService;
            _logger = logger;
        }

        // ---------------------------------------------------------------
        // POST api/CustomerAuth/register
        // ---------------------------------------------------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = _repo.GetUserByMobile(req.MobileNumber);
            if (existing != null)
                return Conflict(new { success = false, message = "This mobile number is already registered. Try logging in instead." });

            var regRes = _repo.RegisterCustomer(req);
            if (!regRes.Success)
                return BadRequest(new { success = false, message = regRes.Message });

            var otpResult = await _otpService.SendOtpAsync(req.MobileNumber);
            if (!otpResult.Success)
                return StatusCode(502, new { success = false, message = otpResult.Message });

            return Ok(new
            {
                success = true,
                message = "Enter the OTP sent to your mobile number to verify it.",
                customerId = regRes.User?.UserId ?? regRes.Customer?.CustomerId ?? 0,
                sessionId = otpResult.SessionId,
                debugOtp = otpResult.DebugOtp
            });
        }

        // ---------------------------------------------------------------
        // POST api/CustomerAuth/send-otp
        // ---------------------------------------------------------------
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest req)
        {
            var otpResult = await _otpService.SendOtpAsync(req.MobileNumber);
            if (!otpResult.Success)
                return StatusCode(502, new { success = false, message = otpResult.Message });

            return Ok(new
            {
                success = true,
                message = "OTP sent to your mobile number.",
                sessionId = otpResult.SessionId,
                debugOtp = otpResult.DebugOtp
            });
        }

        // ---------------------------------------------------------------
        // POST api/CustomerAuth/verify-otp
        // ---------------------------------------------------------------
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
        {
            var verifyResult = await _otpService.VerifyOtpAsync(req.MobileNumber, req.Otp, req.SessionId);
            if (!verifyResult.Success)
                return BadRequest(new { success = false, message = verifyResult.Message });

            var user = _repo.GetUserByMobile(req.MobileNumber);
            if (user == null)
                return NotFound(new { success = false, message = "Account not found." });

            if (req.Purpose == "otp" || string.IsNullOrEmpty(req.Purpose))
            {
                var vRes = _repo.VerifyOtp(req.MobileNumber, req.Otp, "SignupVerification");
                user = vRes.User ?? _repo.GetUserByMobile(req.MobileNumber);
            }

            string token = "EU_TOKEN_" + Guid.NewGuid().ToString("N");

            return Ok(new
            {
                success = true,
                message = "Mobile number verified.",
                token,
                user,
                customer = new CustomerProfile
                {
                    CustomerId = user?.UserId ?? 0,
                    FullName = user?.FullName ?? "",
                    MobileNumber = user?.MobileNumber ?? req.MobileNumber,
                    Email = user?.Email,
                    IsMobileVerified = true,
                    CreatedDate = DateTime.Now
                }
            });
        }

        // ---------------------------------------------------------------
        // POST api/CustomerAuth/login
        // ---------------------------------------------------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var loginRes = _repo.LoginCustomer(req);
            if (!loginRes.Success)
            {
                if (!loginRes.IsMobileVerified)
                {
                    var otpResult = await _otpService.SendOtpAsync(req.MobileNumber);
                    return StatusCode(403, new
                    {
                        success = false,
                        requiresVerification = true,
                        message = "Please verify your mobile number first. A new OTP has been sent.",
                        sessionId = otpResult.SessionId
                    });
                }
                return Unauthorized(new { success = false, message = loginRes.Message });
            }

            string token = loginRes.Token ?? ("EU_TOKEN_" + Guid.NewGuid().ToString("N"));

            return Ok(new
            {
                success = true,
                message = "Login successful.",
                token,
                user = loginRes.User,
                customer = loginRes.Customer
            });
        }

        // ---------------------------------------------------------------
        // POST api/CustomerAuth/forgot-password
        // ---------------------------------------------------------------
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            var user = _repo.GetUserByMobile(req.MobileNumber);

            if (user != null)
            {
                var otpResult = await _otpService.SendOtpAsync(req.MobileNumber);
                if (!otpResult.Success)
                    return StatusCode(502, new { success = false, message = otpResult.Message });

                return Ok(new
                {
                    success = true,
                    message = "If that mobile number is registered, an OTP has been sent.",
                    sessionId = otpResult.SessionId
                });
            }

            return Ok(new { success = true, message = "If that mobile number is registered, an OTP has been sent." });
        }

        // ---------------------------------------------------------------
        // POST api/CustomerAuth/reset-password
        // ---------------------------------------------------------------
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            var verifyResult = await _otpService.VerifyOtpAsync(req.SessionId, req.Otp);
            if (!verifyResult.Success)
                return BadRequest(new { success = false, message = verifyResult.Message });

            var resetRes = _repo.ResetPassword(req);
            if (!resetRes.Success) return BadRequest(new { success = false, message = resetRes.Message });

            return Ok(new { success = true, message = resetRes.Message });
        }

        // ---------------------------------------------------------------
        // GET api/CustomerAuth/profile/{userId}
        // ---------------------------------------------------------------
        [HttpGet("profile/{userId}")]
        public IActionResult GetProfile(int userId)
        {
            var user = _repo.GetUserById(userId);
            if (user == null) return NotFound(new { success = false, message = "Profile not found." });
            return Ok(new { success = true, data = user });
        }

        // ---------------------------------------------------------------
        // PUT api/CustomerAuth/profile
        // ---------------------------------------------------------------
        [HttpPut("profile")]
        public IActionResult UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            bool ok = _repo.UpdateUserProfile(req);
            if (!ok) return BadRequest(new { success = false, message = "Failed to update profile." });

            return Ok(new { success = true, data = _repo.GetUserById(req.UserId) });
        }

        // ---------------------------------------------------------------
        // Address book
        // ---------------------------------------------------------------
        [HttpGet("addresses/{userId}")]
        public IActionResult GetAddresses(int userId)
        {
            return Ok(new { success = true, data = _repo.GetUserAddresses(userId) });
        }

        [HttpPost("addresses")]
        public IActionResult AddAddress([FromBody] UserAddressDto req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            int addressId = _repo.AddUserAddress(req);
            return Ok(new { success = true, addressId, data = _repo.GetUserAddresses(req.UserId) });
        }

        [HttpDelete("addresses/{addressId}")]
        public IActionResult DeleteAddress(int addressId, [FromQuery] int userId)
        {
            _repo.DeleteUserAddress(addressId, userId);
            return Ok(new { success = true, data = _repo.GetUserAddresses(userId) });
        }
    }
}
