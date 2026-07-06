using EcommerceAPI.Models;
using EcommerceService.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EcommerceService.Repository.Service
{
    public class WebsiteSettingsRepository : IWebsiteSettingsRepository
    {
        private readonly IConfiguration _configuration;
        private SqlConnection con;
        private string _connectionString;

        public WebsiteSettingsRepository(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("EcommerceDb");
        }

        private void connection()
        {
            string constr = this._configuration.GetConnectionString("EcommerceDb");
            con = new SqlConnection(constr);
            if (con.State == ConnectionState.Closed)
                con.Open();
        }

        public WebsiteSetting GetSettings()
        {
            WebsiteSetting settings = null;
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT TOP 1 * FROM WebsiteSettings ORDER BY WebsiteSettingId ASC", con))
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        settings = new WebsiteSetting
                        {
                            WebsiteSettingId = Convert.ToInt32(reader["WebsiteSettingId"]),
                            WebsiteName = reader["WebsiteName"] as string,
                            Logo = reader["Logo"] as string,
                            Favicon = reader["Favicon"] as string,
                            Phone = reader["Phone"] as string,
                            Email = reader["Email"] as string,
                            Address = reader["Address"] as string,
                            Facebook = reader["Facebook"] as string,
                            Instagram = reader["Instagram"] as string,
                            Youtube = reader["Youtube"] as string,
                            WhatsApp = reader["WhatsApp"] as string,
                            PrimaryColor = reader["PrimaryColor"] as string,
                            SecondaryColor = reader["SecondaryColor"] as string,
                            FooterText = reader["FooterText"] as string
                        };
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return settings;
        }

        public bool UpdateSettings(WebsiteSetting settings)
        {
            try
            {
                connection();

                // Check if any row exists
                int existingId = 0;
                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT TOP 1 WebsiteSettingId FROM WebsiteSettings ORDER BY WebsiteSettingId ASC", con))
                {
                    var result = checkCmd.ExecuteScalar();
                    if (result != null) existingId = Convert.ToInt32(result);
                }

                if (existingId == 0)
                {
                    // No row yet — insert
                    string insertQuery = @"
                        INSERT INTO WebsiteSettings
                            (WebsiteName, Logo, Favicon, Phone, Email, Address,
                             Facebook, Instagram, Youtube, WhatsApp,
                             PrimaryColor, SecondaryColor, FooterText)
                        VALUES
                            (@WebsiteName, @Logo, @Favicon, @Phone, @Email, @Address,
                             @Facebook, @Instagram, @Youtube, @WhatsApp,
                             @PrimaryColor, @SecondaryColor, @FooterText)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, con))
                    {
                        AddSettingsParams(cmd, settings);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Row exists — update it (ignore whatever Id was sent, always update the single row)
                    string updateQuery = @"
                        UPDATE WebsiteSettings SET
                            WebsiteName = @WebsiteName,
                            Phone = @Phone,
                            Email = @Email,
                            Address = @Address,
                            Facebook = @Facebook,
                            Instagram = @Instagram,
                            Youtube = @Youtube,
                            WhatsApp = @WhatsApp,
                            PrimaryColor = @PrimaryColor,
                            SecondaryColor = @SecondaryColor,
                            FooterText = @FooterText
                        WHERE WebsiteSettingId = @WebsiteSettingId";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                    {
                        AddSettingsParams(cmd, settings, skipImages: true);
                        cmd.Parameters.AddWithValue("@WebsiteSettingId", existingId);
                        cmd.ExecuteNonQuery();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateSettings error: " + ex.Message);
                return false;
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        // Logo/Favicon updated separately via dedicated upload endpoints,
        // so the main UpdateSettings text-update never overwrites them accidentally.
        public string UpdateLogo(string logoPath)
        {
            return UpdateSingleImageColumn("Logo", logoPath);
        }

        public string UpdateFavicon(string faviconPath)
        {
            return UpdateSingleImageColumn("Favicon", faviconPath);
        }

        private string UpdateSingleImageColumn(string columnName, string path)
        {
            try
            {
                connection();

                int existingId = 0;
                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT TOP 1 WebsiteSettingId FROM WebsiteSettings ORDER BY WebsiteSettingId ASC", con))
                {
                    var result = checkCmd.ExecuteScalar();
                    if (result != null) existingId = Convert.ToInt32(result);
                }

                if (existingId == 0)
                {
                    // No settings row at all yet — create one with just this image column set
                    string insertQuery = $@"
                        INSERT INTO WebsiteSettings ({columnName})
                        OUTPUT INSERTED.WebsiteSettingId
                        VALUES (@Path)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@Path", path);
                        cmd.ExecuteScalar();
                    }
                }
                else
                {
                    string updateQuery = $@"
                        UPDATE WebsiteSettings SET {columnName} = @Path
                        WHERE WebsiteSettingId = @Id";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@Path", path);
                        cmd.Parameters.AddWithValue("@Id", existingId);
                        cmd.ExecuteNonQuery();
                    }
                }

                return path;
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        private void AddSettingsParams(SqlCommand cmd, WebsiteSetting s, bool skipImages = false)
        {
            cmd.Parameters.AddWithValue("@WebsiteName", s.WebsiteName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Phone", s.Phone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", s.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", s.Address ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Facebook", s.Facebook ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Instagram", s.Instagram ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Youtube", s.Youtube ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@WhatsApp", s.WhatsApp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PrimaryColor", s.PrimaryColor ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SecondaryColor", s.SecondaryColor ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FooterText", s.FooterText ?? (object)DBNull.Value);

            if (!skipImages)
            {
                cmd.Parameters.AddWithValue("@Logo", s.Logo ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Favicon", s.Favicon ?? (object)DBNull.Value);
            }
        }
    }
}