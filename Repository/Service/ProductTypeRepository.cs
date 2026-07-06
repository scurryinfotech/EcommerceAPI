using EcommerceAPI.Models;
using EcommerceService.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EcommerceService.Repository.Service
{
    public class ProductTypeRepository : IProductTypeRepository
    {
        private readonly IConfiguration _configuration;
        private SqlConnection con;

        public ProductTypeRepository(IConfiguration configuration)
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

        public List<ProductType> GetAllProductTypes(bool activeOnly = false)
        {
            List<ProductType> types = new List<ProductType>();
            try
            {
                connection();
                string query = "SELECT * FROM ProductTypes";
                if (activeOnly) query += " WHERE IsActive = 1";
                query += " ORDER BY DisplayOrder ASC, ProductTypeId ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                        types.Add(MapType(reader));
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return types;
        }

        public ProductType GetProductTypeById(int id)
        {
            ProductType type = null;
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM ProductTypes WHERE ProductTypeId = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read())
                        type = MapType(reader);
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return type;
        }

        public int AddProductType(ProductType type)
        {
            try
            {
                connection();
                string query = @"
                    INSERT INTO ProductTypes (ProductTypeName, ImagePath, DisplayOrder, IsActive)
                    OUTPUT INSERTED.ProductTypeId
                    VALUES (@Name, @ImagePath, @DisplayOrder, @IsActive)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Name", type.ProductTypeName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ImagePath", type.ImagePath ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DisplayOrder", type.DisplayOrder);
                    cmd.Parameters.AddWithValue("@IsActive", type.IsActive);

                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool UpdateProductType(ProductType type)
        {
            try
            {
                connection();
                string query = @"
                    UPDATE ProductTypes SET
                        ProductTypeName = @Name,
                        ImagePath = @ImagePath,
                        DisplayOrder = @DisplayOrder,
                        IsActive = @IsActive
                    WHERE ProductTypeId = @Id";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Name", type.ProductTypeName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ImagePath", type.ImagePath ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DisplayOrder", type.DisplayOrder);
                    cmd.Parameters.AddWithValue("@IsActive", type.IsActive);
                    cmd.Parameters.AddWithValue("@Id", type.ProductTypeId);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool DeleteProductType(int id)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("UPDATE ProductTypes SET IsActive = 0 WHERE ProductTypeId = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        private ProductType MapType(SqlDataReader reader)
        {
            return new ProductType
            {
                ProductTypeId = Convert.ToInt32(reader["ProductTypeId"]),
                ProductTypeName = reader["ProductTypeName"] as string,
                ImagePath = reader["ImagePath"] as string,
                DisplayOrder = reader["DisplayOrder"] != DBNull.Value ? Convert.ToInt32(reader["DisplayOrder"]) : 0,
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"])
            };
        }
    }
}