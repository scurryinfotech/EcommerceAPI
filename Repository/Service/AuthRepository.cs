using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceAPI.Repository.Service
{
    public class AuthRepository : IAuthRepository
    {
        private readonly IConfiguration _configuration;

        public AuthRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            string constr = _configuration.GetConnectionString("EcommerceDb");
            var con = new SqlConnection(constr);
            con.Open();
            return con;
        }

        private string HashOtp(string otp, string mobileNumber)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{mobileNumber}:{otp}:EU_SALT_2026"));
            return Convert.ToBase64String(bytes);
        }

        public (bool Success, string Message, string? OtpForDebug) SendOtp(string mobileNumber, string purpose)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
                return (false, "Mobile number is required.", null);

            mobileNumber = mobileNumber.Trim().Replace(" ", "").Replace("-", "");
            if (mobileNumber.Length > 15) mobileNumber = mobileNumber.Substring(mobileNumber.Length - 10);

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
                        if ((DateTime.Now - lastCreated).TotalSeconds < 30)
                        {
                            return (false, "Please wait 30 seconds before requesting another OTP.", null);
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

                // In production, integrate SMS Gateway API here (e.g. Fast2SMS/Twilio).
                // Return OTP for debug in response or log to server console.
                Console.WriteLine($"[SECURITY OTP NOTICE] Sent OTP {rawOtp} to {mobileNumber} for {purpose}");
                return (true, $"OTP sent successfully to +91 {mobileNumber}.", rawOtp);
            }
            catch (Exception ex)
            {
                return (false, "Failed to send OTP: " + ex.Message, null);
            }
        }

        public (bool Success, string Message, UserDto? User) VerifyOtp(string mobileNumber, string otp, string purpose)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber) || string.IsNullOrWhiteSpace(otp))
                return (false, "Mobile number and OTP are required.", null);

            mobileNumber = mobileNumber.Trim().Replace(" ", "").Replace("-", "");
            if (mobileNumber.Length > 15) mobileNumber = mobileNumber.Substring(mobileNumber.Length - 10);

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
                    // Increment attempts
                    using var updateAttemptsCmd = new SqlCommand("UPDATE OtpRequests SET AttemptsCount = AttemptsCount + 1 WHERE OtpId = @OtpId", con);
                    updateAttemptsCmd.Parameters.AddWithValue("@OtpId", otpId);
                    updateAttemptsCmd.ExecuteNonQuery();

                    int remaining = maxAttempts - (attempts + 1);
                    return (false, $"Invalid OTP. {remaining} attempt(s) remaining.", null);
                }

                // Mark OTP as verified/invalidated
                using var verifyCmd = new SqlCommand("UPDATE OtpRequests SET IsVerified = 1 WHERE OtpId = @OtpId", con);
                verifyCmd.Parameters.AddWithValue("@OtpId", otpId);
                verifyCmd.ExecuteNonQuery();

                // Check if user exists or if this is registration verification
                var user = GetUserByMobile(mobileNumber);
                if (user != null)
                {
                    return (true, "OTP verified successfully.", user);
                }

                return (true, "OTP verified successfully.", null);
            }
            catch (Exception ex)
            {
                return (false, "OTP verification failed: " + ex.Message, null);
            }
        }

        public (bool Success, string Message, UserDto? User) RegisterUser(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MobileNumber) || string.IsNullOrWhiteSpace(request.FullName))
                return (false, "Full Name and Mobile Number are required.", null);

            string mobile = request.MobileNumber.Trim().Replace(" ", "").Replace("-", "");

            var existingUser = GetUserByMobile(mobile);
            if (existingUser != null)
            {
                return (false, "An account with this mobile number already exists. Please login.", null);
            }

            // Verify OTP first
            var otpCheck = VerifyOtp(mobile, request.Otp, "Register");
            if (!otpCheck.Success)
            {
                return (false, otpCheck.Message, null);
            }

            try
            {
                using var con = GetConnection();
                using var transaction = con.BeginTransaction();

                string insertUser = @"INSERT INTO Users (MobileNumber, Email, FullName, CompanyName, GSTIN, Role, IsActive, IsApproved, CreatedAt, UpdatedAt)
                                      OUTPUT INSERTED.UserId
                                      VALUES (@MobileNumber, @Email, @FullName, @CompanyName, @GSTIN, 'Customer', 1, 1, GETDATE(), GETDATE())";

                int newUserId = 0;
                using (var cmd = new SqlCommand(insertUser, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@MobileNumber", mobile);
                    cmd.Parameters.AddWithValue("@Email", (object?)request.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FullName", request.FullName);
                    cmd.Parameters.AddWithValue("@CompanyName", (object?)request.CompanyName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@GSTIN", (object?)request.GSTIN ?? DBNull.Value);
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
                    cmdProf.Parameters.AddWithValue("@Country", (object?)request.Country ?? "India");
                    cmdProf.Parameters.AddWithValue("@Pincode", (object?)request.Pincode ?? DBNull.Value);
                    cmdProf.ExecuteNonQuery();
                }

                // Add default address
                string insertAddress = @"INSERT INTO UserAddresses (UserId, FullName, Mobile, AddressLine1, AddressLine2, City, State, Pincode, Country, IsDefault, CreatedAt, UpdatedAt)
                                         VALUES (@UserId, @FullName, @Mobile, @AddressLine1, @AddressLine2, @City, @State, @Pincode, @Country, 1, GETDATE(), GETDATE())";

                using (var cmdAddr = new SqlCommand(insertAddress, con, transaction))
                {
                    cmdAddr.Parameters.AddWithValue("@UserId", newUserId);
                    cmdAddr.Parameters.AddWithValue("@FullName", request.FullName);
                    cmdAddr.Parameters.AddWithValue("@Mobile", mobile);
                    cmdAddr.Parameters.AddWithValue("@AddressLine1", request.AddressLine1);
                    cmdAddr.Parameters.AddWithValue("@AddressLine2", (object?)request.AddressLine2 ?? DBNull.Value);
                    cmdAddr.Parameters.AddWithValue("@City", request.City);
                    cmdAddr.Parameters.AddWithValue("@State", request.State);
                    cmdAddr.Parameters.AddWithValue("@Pincode", request.Pincode);
                    cmdAddr.Parameters.AddWithValue("@Country", string.IsNullOrEmpty(request.Country) ? "India" : request.Country);
                    cmdAddr.ExecuteNonQuery();
                }

                transaction.Commit();

                var userDto = GetUserById(newUserId);
                return (true, "Account created successfully.", userDto);
            }
            catch (Exception ex)
            {
                return (false, "Failed to create account: " + ex.Message, null);
            }
        }

        public UserDto? GetUserByMobile(string mobileNumber)
        {
            mobileNumber = mobileNumber.Trim().Replace(" ", "").Replace("-", "");
            try
            {
                using var con = GetConnection();
                string sql = @"SELECT u.UserId, u.MobileNumber, u.Email, u.FullName, u.CompanyName, u.GSTIN, u.Role, u.IsApproved,
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
                string sql = @"SELECT u.UserId, u.MobileNumber, u.Email, u.FullName, u.CompanyName, u.GSTIN, u.Role, u.IsApproved,
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
                string sql = @"INSERT INTO Users (MobileNumber, FullName, CompanyName, Email, Role, IsActive, IsApproved, CreatedAt)
                               VALUES ('9925364108', 'Test Wholesale Customer', 'Test Enterprise LLC', 'testbuyer@euphoriacreation.com', 'Customer', 1, 1, GETDATE());
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
                string sql = "SELECT * FROM UserAddresses WHERE UserId = @UserId ORDER BY IsDefault DESC, AddressId DESC";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(MapAddress(r));
                }
            }
            catch { }
            return list;
        }

        public UserAddressDto? GetAddressById(int addressId, int userId)
        {
            try
            {
                using var con = GetConnection();
                string sql = "SELECT * FROM UserAddresses WHERE AddressId = @AddressId AND UserId = @UserId";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@AddressId", addressId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var r = cmd.ExecuteReader();
                if (r.Read()) return MapAddress(r);
            }
            catch { }
            return null;
        }

        public int AddUserAddress(UserAddressDto address)
        {
            try
            {
                using var con = GetConnection();
                if (address.IsDefault)
                {
                    using var resetCmd = new SqlCommand("UPDATE UserAddresses SET IsDefault = 0 WHERE UserId = @UserId", con);
                    resetCmd.Parameters.AddWithValue("@UserId", address.UserId);
                    resetCmd.ExecuteNonQuery();
                }

                string sql = @"INSERT INTO UserAddresses (UserId, FullName, Mobile, AddressLine1, AddressLine2, Landmark, City, State, Pincode, Country, IsDefault, CreatedAt, UpdatedAt)
                               OUTPUT INSERTED.AddressId
                               VALUES (@UserId, @FullName, @Mobile, @AddressLine1, @AddressLine2, @Landmark, @City, @State, @Pincode, @Country, @IsDefault, GETDATE(), GETDATE())";

                using var cmd = new SqlCommand(sql, con);
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
                cmd.Parameters.AddWithValue("@IsDefault", address.IsDefault);
                return Convert.ToInt32(cmd.ExecuteScalar());
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
                if (address.IsDefault)
                {
                    using var resetCmd = new SqlCommand("UPDATE UserAddresses SET IsDefault = 0 WHERE UserId = @UserId", con);
                    resetCmd.Parameters.AddWithValue("@UserId", address.UserId);
                    resetCmd.ExecuteNonQuery();
                }

                string sql = @"UPDATE UserAddresses SET FullName = @FullName, Mobile = @Mobile, AddressLine1 = @AddressLine1, AddressLine2 = @AddressLine2, Landmark = @Landmark, City = @City, State = @State, Pincode = @Pincode, Country = @Country, IsDefault = @IsDefault, UpdatedAt = GETDATE()
                               WHERE AddressId = @AddressId AND UserId = @UserId";

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@FullName", address.FullName);
                cmd.Parameters.AddWithValue("@Mobile", address.Mobile);
                cmd.Parameters.AddWithValue("@AddressLine1", address.AddressLine1);
                cmd.Parameters.AddWithValue("@AddressLine2", (object?)address.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Landmark", (object?)address.Landmark ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@City", address.City);
                cmd.Parameters.AddWithValue("@State", address.State);
                cmd.Parameters.AddWithValue("@Pincode", address.Pincode);
                cmd.Parameters.AddWithValue("@Country", string.IsNullOrEmpty(address.Country) ? "India" : address.Country);
                cmd.Parameters.AddWithValue("@IsDefault", address.IsDefault);
                cmd.Parameters.AddWithValue("@AddressId", address.AddressId);
                cmd.Parameters.AddWithValue("@UserId", address.UserId);
                return cmd.ExecuteNonQuery() > 0;
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
                using var cmd = new SqlCommand("DELETE FROM UserAddresses WHERE AddressId = @AddressId AND UserId = @UserId", con);
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

                using (var cmdReset = new SqlCommand("UPDATE UserAddresses SET IsDefault = 0 WHERE UserId = @UserId", con, tx))
                {
                    cmdReset.Parameters.AddWithValue("@UserId", userId);
                    cmdReset.ExecuteNonQuery();
                }

                using (var cmdSet = new SqlCommand("UPDATE UserAddresses SET IsDefault = 1 WHERE AddressId = @AddressId AND UserId = @UserId", con, tx))
                {
                    cmdSet.Parameters.AddWithValue("@AddressId", addressId);
                    cmdSet.Parameters.AddWithValue("@UserId", userId);
                    cmdSet.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch { return false; }
        }

        private UserDto MapUserDto(SqlDataReader r)
        {
            return new UserDto
            {
                UserId = Convert.ToInt32(r["UserId"]),
                MobileNumber = r["MobileNumber"].ToString() ?? "",
                Email = r["Email"] as string ?? "",
                FullName = r["FullName"].ToString() ?? "",
                CompanyName = r["CompanyName"] as string ?? "",
                GSTIN = r["GSTIN"] as string,
                Role = r["Role"].ToString() ?? "Customer",
                IsApproved = r["IsApproved"] != DBNull.Value && Convert.ToBoolean(r["IsApproved"]),
                ProfilePhoto = r["ProfilePhoto"] as string,
                BusinessAddress = r["BusinessAddress"] as string,
                City = r["City"] as string,
                State = r["State"] as string,
                Country = r["Country"] as string ?? "India",
                Pincode = r["Pincode"] as string
            };
        }

        private UserAddressDto MapAddress(SqlDataReader r)
        {
            return new UserAddressDto
            {
                AddressId = Convert.ToInt32(r["AddressId"]),
                UserId = Convert.ToInt32(r["UserId"]),
                FullName = r["FullName"].ToString() ?? "",
                Mobile = r["Mobile"].ToString() ?? "",
                AddressLine1 = r["AddressLine1"].ToString() ?? "",
                AddressLine2 = r["AddressLine2"] as string,
                Landmark = r["Landmark"] as string,
                City = r["City"].ToString() ?? "",
                State = r["State"].ToString() ?? "",
                Pincode = r["Pincode"].ToString() ?? "",
                Country = r["Country"].ToString() ?? "India",
                IsDefault = Convert.ToBoolean(r["IsDefault"])
            };
        }
    }
}
