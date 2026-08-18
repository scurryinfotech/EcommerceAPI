using EcommerceAPI.Models;

namespace EcommerceAPI.Repository.Interface
{
    public interface IAuthRepository
    {
        (bool Success, string Message, string? OtpForDebug) SendOtp(string mobileNumber, string purpose);
        (bool Success, string Message, UserDto? User) VerifyOtp(string mobileNumber, string otp, string purpose);
        (bool Success, string Message, UserDto? User) RegisterUser(RegisterRequest request);
        UserDto? GetUserById(int userId);
        UserDto? GetUserByMobile(string mobileNumber);
        UserDto GetOrCreateGuestUser();
        bool UpdateUserProfile(UpdateProfileRequest request);
        List<UserAddressDto> GetUserAddresses(int userId);
        UserAddressDto? GetAddressById(int addressId, int userId);
        int AddUserAddress(UserAddressDto address);
        bool UpdateUserAddress(UserAddressDto address);
        bool DeleteUserAddress(int addressId, int userId);
        bool SetDefaultAddress(int addressId, int userId);
    }
}
