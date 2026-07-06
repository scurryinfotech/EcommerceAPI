using EcommerceAPI.Models;
using EcommerceService.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EcommerceService.Repository.Service
{
    public class BrandRepository : IBrandRepository
    {
        private readonly IConfiguration _configuration;
        private SqlConnection con;

        public BrandRepository(IConfiguration configuration)
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

        public List<Brand> GetAllBrands(bool activeOnly = false)
        {
            List<Brand> brands = new List<Brand>();
            try
            {
                connection();
                string query = "SELECT * FROM Brands";
                if (activeOnly) query += " WHERE IsActive = 1";
                query += " ORDER BY DisplayOrder ASC, BrandId ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        brands.Add(MapBrand(reader));
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return brands;
        }

        public Brand GetBrandById(int brandId)
        {
            Brand brand = null;
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM Brands WHERE BrandId = @BrandId", con))
                {
                    cmd.Parameters.AddWithValue("@BrandId", brandId);
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read())
                        brand = MapBrand(reader);
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return brand;
        }

        public int AddBrand(Brand brand)
        {
            try
            {
                connection();
                string query = @"
                    INSERT INTO Brands (BrandName, Logo, Description, DisplayOrder, IsActive)
                    OUTPUT INSERTED.BrandId
                    VALUES (@BrandName, @Logo, @Description, @DisplayOrder, @IsActive)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@BrandName", brand.BrandName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Logo", brand.Logo ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", brand.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DisplayOrder", brand.DisplayOrder);
                    cmd.Parameters.AddWithValue("@IsActive", brand.IsActive);

                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool UpdateBrand(Brand brand)
        {
            try
            {
                connection();
                string query = @"
                    UPDATE Brands SET
                        BrandName = @BrandName,
                        Description = @Description,
                        DisplayOrder = @DisplayOrder,
                        IsActive = @IsActive
                    WHERE BrandId = @BrandId";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@BrandName", brand.BrandName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", brand.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DisplayOrder", brand.DisplayOrder);
                    cmd.Parameters.AddWithValue("@IsActive", brand.IsActive);
                    cmd.Parameters.AddWithValue("@BrandId", brand.BrandId);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool DeleteBrand(int brandId)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("UPDATE Brands SET IsActive = 0 WHERE BrandId = @BrandId", con))
                {
                    cmd.Parameters.AddWithValue("@BrandId", brandId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public string UpdateBrandLogo(int brandId, string logoPath)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("UPDATE Brands SET Logo = @Logo WHERE BrandId = @BrandId", con))
                {
                    cmd.Parameters.AddWithValue("@Logo", logoPath);
                    cmd.Parameters.AddWithValue("@BrandId", brandId);
                    cmd.ExecuteNonQuery();
                }
                return logoPath;
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        private Brand MapBrand(SqlDataReader reader)
        {
            return new Brand
            {
                BrandId = Convert.ToInt32(reader["BrandId"]),
                BrandName = reader["BrandName"] as string,
                Logo = reader["Logo"] as string,
                Description = reader["Description"] as string,
                DisplayOrder = reader["DisplayOrder"] != DBNull.Value ? Convert.ToInt32(reader["DisplayOrder"]) : 0,
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"])
            };
        }
    }
}