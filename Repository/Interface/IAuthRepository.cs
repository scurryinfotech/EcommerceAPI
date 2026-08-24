using EcommerceAPI.Models;

namespace EcommerceAPI.Repository.Interface
{
    public interface IAuthRepository
    {
        (bool Success, string Message, string? OtpForDebug) SendOtp(string mobileNumber, string purpose);
        (bool Success, string Message, UserDto? User) VerifyOtp(string mobileNumber, string otp, string purpose);
        AuthResult VerifyOtpV2(VerifyOtpRequest request);
        (bool Success, string Message, UserDto? User) RegisterUser(RegisterRequest request);
        AuthResult RegisterCustomer(RegisterRequest request);
        AuthResult LoginCustomer(LoginRequest request);
        AuthResult ForgotPassword(ForgotPasswordRequest request);
        AuthResult ResetPassword(ResetPasswordRequest request);
        UserDto? GetUserById(int userId);
        UserDto? GetUserByMobile(string mobileNumber);
        UserDto GetOrCreateGuestUser();
        bool MarkMobileVerified(string mobileNumber);
        bool UpdateUserProfile(UpdateProfileRequest request);
        List<UserAddressDto> GetUserAddresses(int userId);
        UserAddressDto? GetAddressById(int addressId, int userId);
        int AddUserAddress(UserAddressDto address);
        bool UpdateUserAddress(UserAddressDto address);
        bool DeleteUserAddress(int addressId, int userId);
        bool SetDefaultAddress(int addressId, int userId);
    }
}
