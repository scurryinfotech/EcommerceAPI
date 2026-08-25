using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using EcommerceAPI.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceAPI.Repository.Service
{
    public class AuthRepository : IAuthRepository
    {
        private readonly IConfiguration _configuration;
        private readonly IMuzztechService _muzztechService;

        public AuthRepository(IConfiguration configuration, IMuzztechService muzztechService)
        {
            _configuration = configuration;
            _muzztechService = muzztechService;
        }

        private SqlConnection GetConnection()
        {
            string constr = _configuration.GetConnectionString("EcommerceDb") ?? "";
            var con = new SqlConnection(constr);
            con.Open();
            EnsureMobileVerifiedColumnExists(con);
            return con;
        }

        private void EnsureMobileVerifiedColumnExists(SqlConnection con)
        {
            try
            {
                string sql = @"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsMobileVerified')
                    BEGIN
                        ALTER TABLE Users ADD IsMobileVerified BIT NOT NULL DEFAULT 0;
                    END";
                using var cmd = new SqlCommand(sql, con);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private string HashOtp(string otp, string mobileNumber)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{mobileNumber}:{otp}:EU_SALT_2026"));
            return Convert.ToBase64String(bytes);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{password}:EU_PASS_SALT_2026"));
            return Convert.ToBase64String(bytes);
        }

        private bool IsValidPassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return false;
            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            return hasUpper && hasLower && hasDigit;
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return true;
            return HashPassword(password) == storedHash;
        }

        public (bool Success, string Message, string? OtpForDebug) SendOtp(string mobileNumber, string purpose)
        {
            purpose = "otp";
            if (string.IsNullOrWhiteSpace(mobileNumber))
                return (false, "Mobile number is required.", null);

            mobileNumber = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (mobileNumber.Length > 10) mobileNumber = mobileNumber.Substring(mobileNumber.Length - 10);

            try
            {
                using var con = GetConnection();

                // Check recent cooldown
                string cooldownCheck = @"SELECT TOP 1 CreatedAt FROM OtpRequests 
                                         WHERE MobileNumber = @MobileNumber AND Purpose = @Purpose 
                                         ORDER BY CreatedAt DESC";
                using (var cmdCheck = new SqlCommand(cooldownCheck, con))
                {
                    cmdCheck.Parameters.AddWithValue("@MobileNumber", mobileNumber);
                    cmdCheck.Parameters.AddWithValue("@Purpose", purpose);
                    var lastCreatedObj = cmdCheck.ExecuteScalar();
                    if (lastCreatedObj != null && lastCreatedObj != DBNull.Value)
                    {
                        DateTime lastCreated = Convert.ToDateTime(lastCreatedObj);
                        if ((DateTime.Now - lastCreated).TotalSeconds < 180)
                        {
                            int leftSec = 180 - (int)(DateTime.Now - lastCreated).TotalSeconds;
                            return (false, $"Please wait {leftSec} seconds before requesting another OTP.", null);
                        }
                    }
                }

                // Generate 6-digit random OTP
                string rawOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                string hashedOtp = HashOtp(rawOtp, mobileNumber);
                DateTime expiry = DateTime.Now.AddMinutes(5);

                string insertSql = @"INSERT INTO OtpRequests (MobileNumber, OtpHash, ExpiryTime, AttemptsCount, MaxAttempts, IsVerified, Purpose, CreatedAt)
                                     VALUES (@MobileNumber, @OtpHash, @ExpiryTime, 0, 5, 0, @Purpose, GETDATE())";

                using (var cmdInsert = new SqlCommand(insertSql, con))
                {
                    cmdInsert.Parameters.AddWithValue("@MobileNumber", mobileNumber);
                    cmdInsert.Parameters.AddWithValue("@OtpHash", hashedOtp);
                    cmdInsert.Parameters.AddWithValue("@ExpiryTime", expiry);
                    cmdInsert.Parameters.AddWithValue("@Purpose", purpose);
                    cmdInsert.ExecuteNonQuery();
                }

                // Call Muzztech API to dispatch OTP to mobile number
                _muzztechService.SendOtpAsync(mobileNumber, rawOtp);

                return (true, $"OTP sent successfully to +91 {mobileNumber} via Muzztech.", null);
            }
            catch (Exception ex)
            {
                return (false, "Failed to send OTP: " + ex.Message, null);
            }
        }

        public (bool Success, string Message, UserDto? User) VerifyOtp(string mobileNumber, string otp, string purpose)
        {
            purpose = "otp";
            if (string.IsNullOrWhiteSpace(mobileNumber) || string.IsNullOrWhiteSpace(otp))
                return (false, "Mobile number and OTP are required.", null);

            mobileNumber = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (mobileNumber.Length > 10) mobileNumber = mobileNumber.Substring(mobileNumber.Length - 10);

            try
            {
                using var con = GetConnection();
                string query = @"SELECT TOP 1 OtpId, OtpHash, ExpiryTime, AttemptsCount, MaxAttempts, IsVerified
                                 FROM OtpRequests 
                                 WHERE MobileNumber = @MobileNumber AND Purpose = @Purpose AND IsVerified = 0
                                 ORDER BY CreatedAt DESC";

                int otpId = 0;
                string expectedHash = "";
                DateTime expiry = DateTime.MinValue;
                int attempts = 0;
                int maxAttempts = 5;

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@MobileNumber", mobileNumber);
                    cmd.Parameters.AddWithValue("@Purpose", purpose);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        otpId = Convert.ToInt32(reader["OtpId"]);
                        expectedHash = reader["OtpHash"].ToString() ?? "";
                        expiry = Convert.ToDateTime(reader["ExpiryTime"]);
                        attempts = Convert.ToInt32(reader["AttemptsCount"]);
                        maxAttempts = Convert.ToInt32(reader["MaxAttempts"]);
                    }
                    else
                    {
                        return (false, "No active OTP found. Please request a new OTP.", null);
                    }
                }

                if (DateTime.Now > expiry)
                {
                    return (false, "OTP has expired. Please request a new OTP.", null);
                }

                if (attempts >= maxAttempts)
                {
                    return (false, "Maximum OTP attempts exceeded. Please request a new OTP.", null);
                }

                string providedHash = HashOtp(otp, mobileNumber);
                if (providedHash != expectedHash)
                {
                    using var updateAttemptsCmd = new SqlCommand("UPDATE OtpRequests SET AttemptsCount = AttemptsCount + 1 WHERE OtpId = @OtpId", con);
                    updateAttemptsCmd.Parameters.AddWithValue("@OtpId", otpId);
                    updateAttemptsCmd.ExecuteNonQuery();

                    int remaining = maxAttempts - (attempts + 1);
                    return (false, $"Invalid OTP. {remaining} attempt(s) remaining.", null);
                }

                // Mark OTP as verified
                using (var verifyCmd = new SqlCommand("UPDATE OtpRequests SET IsVerified = 1 WHERE OtpId = @OtpId", con))
                {
                    verifyCmd.Parameters.AddWithValue("@OtpId", otpId);
                    verifyCmd.ExecuteNonQuery();
                }

                // Mark Mobile Verified in Users table
                using (var markVerifiedCmd = new SqlCommand("UPDATE Users SET IsMobileVerified = 1 WHERE MobileNumber = @MobileNumber", con))
                {
                    markVerifiedCmd.Parameters.AddWithValue("@MobileNumber", mobileNumber);
                    markVerifiedCmd.ExecuteNonQuery();
                }

                var user = GetUserByMobile(mobileNumber);
                return (true, "Mobile number verified successfully.", user);
            }
            catch (Exception ex)
            {
                return (false, "OTP verification failed: " + ex.Message, null);
            }
        }

        public AuthResult VerifyOtpV2(VerifyOtpRequest request)
        {
            var res = VerifyOtp(request.MobileNumber, request.Otp, request.Purpose);
            if (!res.Success)
            {
                return new AuthResult { Success = false, Message = res.Message, IsMobileVerified = false };
            }

            return new AuthResult
            {
                Success = true,
                Message = res.Message,
                IsMobileVerified = true,
                User = res.User,
                Customer = res.User != null ? new CustomerProfile
                {
                    CustomerId = res.User.UserId,
                    FullName = res.User.FullName,
                    MobileNumber = res.User.MobileNumber,
                    Email = res.User.Email,
                    IsMobileVerified = true,
                    CreatedDate = DateTime.Now
                } : null
            };
        }

        public (bool Success, string Message, UserDto? User) RegisterUser(RegisterRequest request)
        {
            var res = RegisterCustomer(request);
            return (res.Success, res.Message, res.User);
        }

        public AuthResult RegisterCustomer(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MobileNumber) || string.IsNullOrWhiteSpace(request.FullName))
                return new AuthResult { Success = false, Message = "Full Name and Mobile Number are required." };

            if (!string.IsNullOrEmpty(request.Password) && !IsValidPassword(request.Password))
            {
                return new AuthResult { Success = false, Message = "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, and one number." };
            }

            string mobile = request.MobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (mobile.Length > 10) mobile = mobile.Substring(mobile.Length - 10);

            var existingUser = GetUserByMobile(mobile);
            if (existingUser != null)
            {
                return new AuthResult { Success = false, Message = "An account with this mobile number already exists. Please login." };
            }

            try
            {
                using var con = GetConnection();
                using var transaction = con.BeginTransaction();

                string passwordHash = !string.IsNullOrEmpty(request.Password) ? HashPassword(request.Password) : "";

                string insertUser = @"INSERT INTO Users (MobileNumber, Email, FullName, CompanyName, GSTIN, PasswordHash, Role, IsActive, IsApproved, IsMobileVerified, CreatedAt, UpdatedAt)
                                      OUTPUT INSERTED.UserId
                                      VALUES (@MobileNumber, @Email, @FullName, @CompanyName, @GSTIN, @PasswordHash, 'Customer', 1, 1, 1, GETDATE(), GETDATE())";

                int newUserId = 0;
                using (var cmd = new SqlCommand(insertUser, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@MobileNumber", mobile);
                    cmd.Parameters.AddWithValue("@Email", (object?)request.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FullName", request.FullName);
                    cmd.Parameters.AddWithValue("@CompanyName", (object?)request.CompanyName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@GSTIN", (object?)request.GSTIN ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    newUserId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                string insertProfile = @"INSERT INTO UserProfiles (UserId, BusinessAddress, City, State, Country, Pincode, UpdatedAt)
                                         VALUES (@UserId, @BusinessAddress, @City, @State, @Country, @Pincode, GETDATE())";

                using (var cmdProf = new SqlCommand(insertProfile, con, transaction))
                {
                    cmdProf.Parameters.AddWithValue("@UserId", newUserId);
                    cmdProf.Parameters.AddWithValue("@BusinessAddress", (object?)request.AddressLine1 ?? DBNull.Value);
                    cmdProf.Parameters.AddWithValue("@City", (object?)request.City ?? DBNull.Value);
                    cmdProf.Parameters.AddWithValue("@State", (object?)request.State ?? DBNull.Value);
                    cmdProf.Parameters.AddWithValue("@Country", string.IsNullOrEmpty(request.Country) ? "India" : request.Country);
                    cmdProf.Parameters.AddWithValue("@Pincode", (object?)request.Pincode ?? DBNull.Value);
                    cmdProf.ExecuteNonQuery();
                }

                if (!string.IsNullOrWhiteSpace(request.AddressLine1))
                {
                    string insertAddress = @"INSERT INTO UserAddresses (UserId, FullName, Mobile, AddressLine1, AddressLine2, City, State, Pincode, Country, IsDefault, CreatedAt, UpdatedAt)
                                             VALUES (@UserId, @FullName, @Mobile, @AddressLine1, @AddressLine2, @City, @State, @Pincode, @Country, 1, GETDATE(), GETDATE())";

                    using (var cmdAddr = new SqlCommand(insertAddress, con, transaction))
                    {
                        cmdAddr.Parameters.AddWithValue("@UserId", newUserId);
                        cmdAddr.Parameters.AddWithValue("@FullName", request.FullName);
                        cmdAddr.Parameters.AddWithValue("@Mobile", mobile);
                        cmdAddr.Parameters.AddWithValue("@AddressLine1", request.AddressLine1);
                        cmdAddr.Parameters.AddWithValue("@AddressLine2", (object?)request.AddressLine2 ?? DBNull.Value);
                        cmdAddr.Parameters.AddWithValue("@City", (object?)request.City ?? "");
                        cmdAddr.Parameters.AddWithValue("@State", (object?)request.State ?? "");
                        cmdAddr.Parameters.AddWithValue("@Pincode", (object?)request.Pincode ?? "");
                        cmdAddr.Parameters.AddWithValue("@Country", string.IsNullOrEmpty(request.Country) ? "India" : request.Country);
                        cmdAddr.ExecuteNonQuery();
                    }
                }

                transaction.Commit();

                var userDto = GetUserById(newUserId);
                return new AuthResult
                {
                    Success = true,
                    Message = "Account created successfully.",
                    IsMobileVerified = true,
                    User = userDto,
                    Customer = new CustomerProfile
                    {
                        CustomerId = newUserId,
                        FullName = request.FullName,
                        MobileNumber = mobile,
                        Email = request.Email,
                        IsMobileVerified = true,
                        CreatedDate = DateTime.Now
                    }
                };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = "Failed to create account: " + ex.Message };
            }
        }

        public AuthResult LoginCustomer(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MobileNumber) || string.IsNullOrWhiteSpace(request.Password))
                return new AuthResult { Success = false, Message = "Mobile Number and Password are required." };

            string mobile = request.MobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (mobile.Length > 10) mobile = mobile.Substring(mobile.Length - 10);

            try
            {
                using var con = GetConnection();
                string sql = @"SELECT u.UserId, u.MobileNumber, u.Email, u.FullName, u.CompanyName, u.GSTIN, u.PasswordHash, u.IsMobileVerified, u.IsActive, u.CreatedAt
                               FROM Users u WHERE u.MobileNumber = @MobileNumber AND u.IsActive = 1";

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@MobileNumber", mobile);
                using var r = cmd.ExecuteReader();
                if (!r.Read())
                {
                    return new AuthResult { Success = false, Message = "Account not found with this mobile number. Please register first." };
                }

                int userId = Convert.ToInt32(r["UserId"]);
                string dbPasswordHash = r["PasswordHash"]?.ToString() ?? "";
                bool isMobileVerified = r["IsMobileVerified"] != DBNull.Value && Convert.ToBoolean(r["IsMobileVerified"]);
                string fullName = r["FullName"]?.ToString() ?? "";
                string email = r["Email"]?.ToString() ?? "";

                r.Close();

                if (!string.IsNullOrEmpty(dbPasswordHash) && !VerifyPassword(request.Password, dbPasswordHash))
                {
                    return new AuthResult { Success = false, Message = "Invalid mobile number or password." };
                }

                if (!isMobileVerified)
                {
                    var sendOtpRes = SendOtp(mobile, "SignupVerification");
                    return new AuthResult
                    {
                        Success = false,
                        IsMobileVerified = false,
                        Message = "Your mobile number is not verified yet. A Muzztech OTP has been sent to +91 " + mobile + " to complete verification.",
                        SessionId = Guid.NewGuid().ToString(),
                        OtpForDebug = sendOtpRes.OtpForDebug
                    };
                }

                var userDto = GetUserById(userId);
                return new AuthResult
                {
                    Success = true,
                    Message = "Login successful.",
                    Token = "EU_TOKEN_" + Guid.NewGuid().ToString("N"),
                    IsMobileVerified = true,
                    User = userDto,
                    Customer = new CustomerProfile
                    {
                        CustomerId = userId,
                        FullName = fullName,
                        MobileNumber = mobile,
                        Email = email,
                        IsMobileVerified = true,
                        CreatedDate = DateTime.Now
                    }
                };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = "Login error: " + ex.Message };
            }
        }

        public AuthResult ForgotPassword(ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MobileNumber))
                return new AuthResult { Success = false, Message = "Mobile number is required." };

            var user = GetUserByMobile(request.MobileNumber);
            if (user == null)
            {
                return new AuthResult { Success = false, Message = "No registered account found for this mobile number." };
            }

            var res = SendOtp(request.MobileNumber, "PasswordReset");
            return new AuthResult
            {
                Success = res.Success,
                Message = res.Message,
                SessionId = Guid.NewGuid().ToString(),
                OtpForDebug = res.OtpForDebug
            };
        }

        public AuthResult ResetPassword(ResetPasswordRequest request)
        {
            var verifyRes = VerifyOtp(request.MobileNumber, request.Otp, "PasswordReset");
            if (!verifyRes.Success)
            {
                return new AuthResult { Success = false, Message = verifyRes.Message };
            }

            string mobile = request.MobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (mobile.Length > 10) mobile = mobile.Substring(mobile.Length - 10);

            string newHash = HashPassword(request.NewPassword);

            try
            {
                using var con = GetConnection();
                string sql = @"UPDATE Users SET PasswordHash = @PasswordHash, IsMobileVerified = 1, UpdatedAt = GETDATE() WHERE MobileNumber = @MobileNumber";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@PasswordHash", newHash);
                cmd.Parameters.AddWithValue("@MobileNumber", mobile);
                cmd.ExecuteNonQuery();

                var user = GetUserByMobile(mobile);
                return new AuthResult
                {
                    Success = true,
                    Message = "Password reset successfully. You can now login with your new password.",
                    IsMobileVerified = true,
                    User = user
                };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = "Failed to reset password: " + ex.Message };
            }
        }

        public UserDto? GetUserByMobile(string mobileNumber)
        {
            mobileNumber = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
            if (mobileNumber.Length > 10) mobileNumber = mobileNumber.Substring(mobileNumber.Length - 10);

            try
            {
                using var con = GetConnection();
                string sql = @"SELECT u.UserId, u.MobileNumber, u.Email, u.FullName, u.CompanyName, u.GSTIN, u.Role, u.IsApproved, u.IsMobileVerified,
                                      p.ProfilePhoto, p.BusinessAddress, p.City, p.State, p.Country, p.Pincode
                               FROM Users u
                               LEFT JOIN UserProfiles p ON u.UserId = p.UserId
                               WHERE u.MobileNumber = @MobileNumber AND u.IsActive = 1";

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@MobileNumber", mobileNumber);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    return MapUserDto(r);
                }
            }
            catch { }
            return null;
        }

        public UserDto? GetUserById(int userId)
        {
            try
            {
                using var con = GetConnection();
                string sql = @"SELECT u.UserId, u.MobileNumber, u.Email, u.FullName, u.CompanyName, u.GSTIN, u.Role, u.IsApproved, u.IsMobileVerified,
                                      p.ProfilePhoto, p.BusinessAddress, p.City, p.State, p.Country, p.Pincode
                               FROM Users u
                               LEFT JOIN UserProfiles p ON u.UserId = p.UserId
                               WHERE u.UserId = @UserId AND u.IsActive = 1";

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    return MapUserDto(r);
                }
            }
            catch { }
            return null;
        }

        public UserDto GetOrCreateGuestUser()
        {
            var existing = GetUserById(1) ?? GetUserByMobile("9925364108");
            if (existing != null) return existing;

            try
            {
                using var con = GetConnection();
                string sql = @"INSERT INTO Users (MobileNumber, FullName, CompanyName, Email, Role, IsActive, IsApproved, IsMobileVerified, CreatedAt)
                               VALUES ('9925364108', 'Test Wholesale Customer', 'Test Enterprise LLC', 'testbuyer@euphoriacreation.com', 'Customer', 1, 1, 1, GETDATE());
                               SELECT SCOPE_IDENTITY();";
                using var cmd = new SqlCommand(sql, con);
                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                return GetUserById(newId) ?? new UserDto { UserId = newId, FullName = "Test Wholesale Customer", MobileNumber = "9925364108" };
            }
            catch
            {
                return new UserDto { UserId = 1, FullName = "Test Wholesale Customer", MobileNumber = "9925364108" };
            }
        }

        public bool UpdateUserProfile(UpdateProfileRequest request)
        {
            try
            {
                using var con = GetConnection();
                using var tx = con.BeginTransaction();

                string updateUser = @"UPDATE Users SET FullName = @FullName, Email = @Email, CompanyName = @CompanyName, GSTIN = @GSTIN, UpdatedAt = GETDATE()
                                      WHERE UserId = @UserId";
                using (var cmd = new SqlCommand(updateUser, con, tx))
                {
                    cmd.Parameters.AddWithValue("@FullName", request.FullName);
                    cmd.Parameters.AddWithValue("@Email", (object?)request.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CompanyName", (object?)request.CompanyName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@GSTIN", (object?)request.GSTIN ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserId", request.UserId);
                    cmd.ExecuteNonQuery();
                }

                string updateProfile = @"IF EXISTS (SELECT 1 FROM UserProfiles WHERE UserId = @UserId)
                                         BEGIN
                                            UPDATE UserProfiles SET BusinessAddress = @BusinessAddress, City = @City, State = @State, Country = @Country, Pincode = @Pincode, UpdatedAt = GETDATE()
                                            WHERE UserId = @UserId
                                         END
                                         ELSE
                                         BEGIN
                                            INSERT INTO UserProfiles (UserId, BusinessAddress, City, State, Country, Pincode, UpdatedAt)
                                            VALUES (@UserId, @BusinessAddress, @City, @State, @Country, @Pincode, GETDATE())
                                         END";
                using (var cmdP = new SqlCommand(updateProfile, con, tx))
                {
                    cmdP.Parameters.AddWithValue("@UserId", request.UserId);
                    cmdP.Parameters.AddWithValue("@BusinessAddress", (object?)request.BusinessAddress ?? DBNull.Value);
                    cmdP.Parameters.AddWithValue("@City", (object?)request.City ?? DBNull.Value);
                    cmdP.Parameters.AddWithValue("@State", (object?)request.State ?? DBNull.Value);
                    cmdP.Parameters.AddWithValue("@Country", (object?)request.Country ?? "India");
                    cmdP.Parameters.AddWithValue("@Pincode", (object?)request.Pincode ?? DBNull.Value);
                    cmdP.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<UserAddressDto> GetUserAddresses(int userId)
        {
            var list = new List<UserAddressDto>();
            try
            {
                using var con = GetConnection();
                string sql = @"SELECT AddressId, UserId, FullName, Mobile, AddressLine1, AddressLine2, Landmark, City, State, Pincode, Country, IsDefault
                               FROM UserAddresses WHERE UserId = @UserId ORDER BY IsDefault DESC, AddressId DESC";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new UserAddressDto
                    {
                        AddressId = Convert.ToInt32(r["AddressId"]),
                        UserId = Convert.ToInt32(r["UserId"]),
                        FullName = r["FullName"]?.ToString() ?? "",
                        Mobile = r["Mobile"]?.ToString() ?? "",
                        AddressLine1 = r["AddressLine1"]?.ToString() ?? "",
                        AddressLine2 = r["AddressLine2"]?.ToString() ?? "",
                        Landmark = r["Landmark"]?.ToString() ?? "",
                        City = r["City"]?.ToString() ?? "",
                        State = r["State"]?.ToString() ?? "",
                        Pincode = r["Pincode"]?.ToString() ?? "",
                        Country = r["Country"]?.ToString() ?? "India",
                        IsDefault = Convert.ToBoolean(r["IsDefault"])
                    });
                }
            }
            catch { }
            return list;
        }

        public UserAddressDto? GetAddressById(int addressId, int userId)
        {
            return GetUserAddresses(userId).FirstOrDefault(a => a.AddressId == addressId);
        }

        public int AddUserAddress(UserAddressDto address)
        {
            try
            {
                using var con = GetConnection();
                using var tx = con.BeginTransaction();

                if (address.IsDefault)
                {
                    using var resetCmd = new SqlCommand("UPDATE UserAddresses SET IsDefault = 0 WHERE UserId = @UserId", con, tx);
                    resetCmd.Parameters.AddWithValue("@UserId", address.UserId);
                    resetCmd.ExecuteNonQuery();
                }

                string sql = @"INSERT INTO UserAddresses (UserId, FullName, Mobile, AddressLine1, AddressLine2, Landmark, City, State, Pincode, Country, IsDefault, CreatedAt, UpdatedAt)
                               OUTPUT INSERTED.AddressId
                               VALUES (@UserId, @FullName, @Mobile, @AddressLine1, @AddressLine2, @Landmark, @City, @State, @Pincode, @Country, @IsDefault, GETDATE(), GETDATE())";

                using var cmd = new SqlCommand(sql, con, tx);
                cmd.Parameters.AddWithValue("@UserId", address.UserId);
                cmd.Parameters.AddWithValue("@FullName", address.FullName);
                cmd.Parameters.AddWithValue("@Mobile", address.Mobile);
                cmd.Parameters.AddWithValue("@AddressLine1", address.AddressLine1);
                cmd.Parameters.AddWithValue("@AddressLine2", (object?)address.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Landmark", (object?)address.Landmark ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@City", address.City);
                cmd.Parameters.AddWithValue("@State", address.State);
                cmd.Parameters.AddWithValue("@Pincode", address.Pincode);
                cmd.Parameters.AddWithValue("@Country", string.IsNullOrEmpty(address.Country) ? "India" : address.Country);
                cmd.Parameters.AddWithValue("@IsDefault", address.IsDefault ? 1 : 0);

                int id = Convert.ToInt32(cmd.ExecuteScalar());
                tx.Commit();
                return id;
            }
            catch
            {
                return 0;
            }
        }

        public bool UpdateUserAddress(UserAddressDto address)
        {
            try
            {
                using var con = GetConnection();
                using var tx = con.BeginTransaction();

                if (address.IsDefault)
                {
                    using var resetCmd = new SqlCommand("UPDATE UserAddresses SET IsDefault = 0 WHERE UserId = @UserId", con, tx);
                    resetCmd.Parameters.AddWithValue("@UserId", address.UserId);
                    resetCmd.ExecuteNonQuery();
                }

                string sql = @"UPDATE UserAddresses SET FullName = @FullName, Mobile = @Mobile, AddressLine1 = @AddressLine1, AddressLine2 = @AddressLine2, 
                               Landmark = @Landmark, City = @City, State = @State, Pincode = @Pincode, Country = @Country, IsDefault = @IsDefault, UpdatedAt = GETDATE()
                               WHERE AddressId = @AddressId AND UserId = @UserId";

                using var cmd = new SqlCommand(sql, con, tx);
                cmd.Parameters.AddWithValue("@AddressId", address.AddressId);
                cmd.Parameters.AddWithValue("@UserId", address.UserId);
                cmd.Parameters.AddWithValue("@FullName", address.FullName);
                cmd.Parameters.AddWithValue("@Mobile", address.Mobile);
                cmd.Parameters.AddWithValue("@AddressLine1", address.AddressLine1);
                cmd.Parameters.AddWithValue("@AddressLine2", (object?)address.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Landmark", (object?)address.Landmark ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@City", address.City);
                cmd.Parameters.AddWithValue("@State", address.State);
                cmd.Parameters.AddWithValue("@Pincode", address.Pincode);
                cmd.Parameters.AddWithValue("@Country", string.IsNullOrEmpty(address.Country) ? "India" : address.Country);
                cmd.Parameters.AddWithValue("@IsDefault", address.IsDefault ? 1 : 0);

                cmd.ExecuteNonQuery();
                tx.Commit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteUserAddress(int addressId, int userId)
        {
            try
            {
                using var con = GetConnection();
                string sql = "DELETE FROM UserAddresses WHERE AddressId = @AddressId AND UserId = @UserId";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@AddressId", addressId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public bool SetDefaultAddress(int addressId, int userId)
        {
            try
            {
                using var con = GetConnection();
                using var tx = con.BeginTransaction();

                using var resetCmd = new SqlCommand("UPDATE UserAddresses SET IsDefault = 0 WHERE UserId = @UserId", con, tx);
                resetCmd.Parameters.AddWithValue("@UserId", userId);
                resetCmd.ExecuteNonQuery();

                using var setCmd = new SqlCommand("UPDATE UserAddresses SET IsDefault = 1 WHERE AddressId = @AddressId AND UserId = @UserId", con, tx);
                setCmd.Parameters.AddWithValue("@AddressId", addressId);
                setCmd.Parameters.AddWithValue("@UserId", userId);
                setCmd.ExecuteNonQuery();

                tx.Commit();
                return true;
            }
            catch { return false; }
        }

        public bool MarkMobileVerified(string mobileNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mobileNumber)) return false;
                string cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
                if (cleanMobile.Length > 10) cleanMobile = cleanMobile.Substring(cleanMobile.Length - 10);

                using var con = GetConnection();
                string sql = "UPDATE Users SET IsMobileVerified = 1 WHERE MobileNumber = @RawMobile OR MobileNumber LIKE '%' + @CleanMobile";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@RawMobile", mobileNumber);
                cmd.Parameters.AddWithValue("@CleanMobile", cleanMobile);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        private UserDto MapUserDto(SqlDataReader r)
        {
            return new UserDto
            {
                UserId = Convert.ToInt32(r["UserId"]),
                MobileNumber = r["MobileNumber"]?.ToString() ?? "",
                Email = r["Email"]?.ToString() ?? "",
                FullName = r["FullName"]?.ToString() ?? "",
                CompanyName = r["CompanyName"]?.ToString() ?? "",
                GSTIN = r["GSTIN"]?.ToString() ?? "",
                Role = r["Role"]?.ToString() ?? "Customer",
                IsApproved = Convert.ToBoolean(r["IsApproved"]),
                IsMobileVerified = r["IsMobileVerified"] != DBNull.Value && Convert.ToBoolean(r["IsMobileVerified"]),
                ProfilePhoto = r["ProfilePhoto"]?.ToString(),
                BusinessAddress = r["BusinessAddress"]?.ToString(),
                City = r["City"]?.ToString(),
                State = r["State"]?.ToString(),
                Country = r["Country"]?.ToString(),
                Pincode = r["Pincode"]?.ToString()
            };
        }
    }
}
