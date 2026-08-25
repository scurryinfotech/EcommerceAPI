using System.ComponentModel.DataAnnotations;

namespace EcommerceAPI.Models
{
    public class RegisterRequest
    {
        [Required, StringLength(150, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [Required, RegularExpression(@"^[0-9]{10,15}$", ErrorMessage = "Enter a valid mobile number.")]
        public string MobileNumber { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string? Password { get; set; }

        public string CompanyName { get; set; } = string.Empty;
        public string? GSTIN { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = "India";
        public string Pincode { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    public class SendOtpRequest
    {
        [Required]
        public string MobileNumber { get; set; } = string.Empty;

        public string Purpose { get; set; } = "otp";
    }

    public class VerifyOtpRequest
    {
        [Required]
        public string MobileNumber { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        [Required, StringLength(10)]
        public string Otp { get; set; } = string.Empty;

        public string Purpose { get; set; } = "otp";
    }

    public class LoginRequest
    {
        [Required]
        public string MobileNumber { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        [Required]
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string MobileNumber { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        [Required, StringLength(10)]
        public string Otp { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AddAddressRequest
    {
        [Required] public string FullName { get; set; } = string.Empty;
        [Required] public string MobileNumber { get; set; } = string.Empty;
        [Required] public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        [Required] public string City { get; set; } = string.Empty;
        [Required] public string State { get; set; } = string.Empty;
        [Required] public string Pincode { get; set; } = string.Empty;
        public string Country { get; set; } = "India";
        public bool IsDefault { get; set; } = false;
    }

    public class CustomerProfile
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsMobileVerified { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    public class CustomerAuthRecord
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsMobileVerified { get; set; }
        public bool IsActive { get; set; }
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? Token { get; set; }
        public CustomerProfile? Customer { get; set; }
        public UserDto? User { get; set; }
        public bool IsMobileVerified { get; set; } = true;
        public string? OtpForDebug { get; set; }
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? Token { get; set; }
        public UserDto? User { get; set; }
        public bool IsMobileVerified { get; set; } = true;
    }

    public class UserDto
    {
        public int UserId { get; set; }
        public string MobileNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? GSTIN { get; set; }
        public string Role { get; set; } = "Customer";
        public bool IsApproved { get; set; } = true;
        public bool IsMobileVerified { get; set; } = false;
        public string? ProfilePhoto { get; set; }
        public string? BusinessAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
    }

    public class UserAddressDto
    {
        public int AddressId { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string? Landmark { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public string Country { get; set; } = "India";
        public bool IsDefault { get; set; }
    }

    public class UpdateProfileRequest
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? GSTIN { get; set; }
        public string? BusinessAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
    }
}
