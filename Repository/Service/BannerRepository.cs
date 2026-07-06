using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EcommerceService.Repository.Service
{
    public class BannerRepository : IBannerRepository
    {
        private readonly IConfiguration _configuration;
        private SqlConnection con;

        public BannerRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private void connection()
        {
            string constr = _configuration.GetConnectionString("EcommerceDb");
            con = new SqlConnection(constr);
            if (con.State == ConnectionState.Closed)
                con.Open();
        }

        public List<Banner> GetActiveBanners()
        {
            List<Banner> banners = new List<Banner>();
            try
            {
                connection();
                string query = @"
                    SELECT * FROM Banners
                    WHERE IsActive = 1
                    ORDER BY DisplayOrder ASC, BannerId ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        banners.Add(new Banner
                        {
                            BannerId = Convert.ToInt32(reader["BannerId"]),
                            Title = reader["Title"] as string,
                            SubTitle = reader["SubTitle"] as string,
                            Description = reader["Description"] as string,
                            DesktopImage = reader["DesktopImage"] as string,
                            MobileImage = reader["MobileImage"] as string,
                            ButtonText = reader["ButtonText"] as string,
                            ButtonUrl = reader["ButtonUrl"] as string,
                            DisplayOrder = reader["DisplayOrder"] != DBNull.Value ? Convert.ToInt32(reader["DisplayOrder"]) : 0,
                            IsActive = Convert.ToBoolean(reader["IsActive"])
                        });
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return banners;
        }
    }
}