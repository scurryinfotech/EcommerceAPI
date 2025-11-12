using EcommerceAPI.Models;
using EcommerceService.Models;
using EcommerceService.Repository.Interface;
using System.Data;
using System.Data.SqlClient;

namespace EcommerceService.Repository.Service
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly IConfiguration _configuration;
        private SqlConnection con;
        private string _connectionString;

        public CategoryRepository(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("EcommerceDb");
        }

        private void connection()
        {
            string constr = this._configuration.GetConnectionString("EcommerceDb");
            con = new SqlConnection(constr);
            if (con.State == ConnectionState.Closed)
            {
                con.Open();
            }
        }

        public List<Category> GetCategories()
        {
            List<Category> categories = new List<Category>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_GetCategories", con))
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    // Ensure you have the following NuGet package installed in your project:
                    // System.Data.SqlClient

                    // This using directive is required for SqlConnection

                    // If you already have this using directive, make sure the NuGet package is referenced.
                    // In Visual Studio, right-click your project > Manage NuGet Packages > Browse > Search for "System.Data.SqlClient" and install it.
                    while (reader.Read())
                    {
                        categories.Add(new Category
                        {
                            category_id = Convert.ToInt32(reader["category_id"]),
                            Name = reader["Name"].ToString(),
                            //Description = reader["Description"].ToString()
                        });
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
            return categories;
        }

        public List<Product> GetProducts()
        {
            List<Product> products = new List<Product>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM products", con))
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        products.Add(new Product
                        {
                            product_id = Convert.ToInt32(reader["product_id"]),
                            name = reader["name"].ToString(),
                            price = Convert.ToDecimal(reader["price"]),
                            description = reader["description"].ToString(),
                            main_image = reader["main_image"].ToString(),
                            created_at = Convert.ToDateTime(reader["created_at"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"])
                        });
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
            return products;
        }

        //public List<Product> GetProductsVariants()
        //{
        //    List<Product> products = new List<Product>();
        //    try
        //    {
        //        connection();
        //        using (SqlCommand cmd = new SqlCommand("SELECT * FROM products", con))
        //        {
        //            SqlDataReader reader = cmd.ExecuteReader();
        //            while (reader.Read())
        //            {
        //                products.Add(new Product
        //                {
        //                    product_id = Convert.ToInt32(reader["product_id"]),
        //                    name = reader["name"].ToString(),
        //                    price = Convert.ToDecimal(reader["price"]),
        //                    description = reader["description"].ToString(),
        //                    main_image = reader["main_image"].ToString(),
        //                    created_at = Convert.ToDateTime(reader["created_at"]),
        //                    IsActive = Convert.ToBoolean(reader["IsActive"])
        //                });
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        if (con.State == ConnectionState.Open)
        //            con.Close();
        //    }
        //    return products;
        ////}

        public List<ProductVariant> GetProductsVariants(int id, List<ProductVariant>? productVariants)
        {
            Product foundProduct = null;
            try
            {
                connection();
                string query = @"SELECT 
                    p.product_id,
                    p.name,
                    p.price,
                    p.description,
                    p.main_image,
                    p.created_at,
                    p.IsActive,
                    pv.variant_id,
                    pv.color_name,
                    pv.color_hex,
                    pv.stock,
                    pv.size,
                    pv.heel_height,
                    pv.IsActive as variant_IsActive
                FROM [Ecommerce].[dbo].[products] p
                LEFT JOIN [Ecommerce].[dbo].[product_variants] pv 
                    ON p.product_id = pv.product_id
                WHERE p.product_id = @id
                    AND p.IsActive = 1
                    AND (pv.IsActive = 1 OR pv.IsActive IS NULL)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        // Create product object only once
                        if (foundProduct == null)
                        {
                            foundProduct = new Product
                            {
                                product_id = Convert.ToInt32(reader["product_id"]),
                                name = reader["name"].ToString(),
                                price = Convert.ToDecimal(reader["price"]),
                                description = reader["description"].ToString(),
                                main_image = reader["main_image"].ToString(),
                                created_at = Convert.ToDateTime(reader["created_at"]),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                variants = new List<ProductVariant>()
                            };
                        }

                        // Add variant if exists
                        if (!reader.IsDBNull(reader.GetOrdinal("variant_id")))
                        {
                            foundProduct.variants.Add(new ProductVariant
                            {
                                variant_id = Convert.ToInt32(reader["variant_id"]),
                                product_id = Convert.ToInt32(reader["product_id"]),
                                color_name = reader["color_name"].ToString(),
                                color_hex = reader["color_hex"].ToString(),
                                size = Convert.ToInt32(reader["size"]),
                                stock = Convert.ToInt32(reader["stock"]),
                                IsActive = Convert.ToBoolean(reader["variant_IsActive"]),
                                heel_height = Convert.ToInt32(reader["heel_height"])
                            });
                        }
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
            return foundProduct?.variants ?? new List<ProductVariant>();
        }
        public bool PlaceOrder(OrderRequest order)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1️⃣ Insert Order
                            string insertOrderQuery = @"
                            INSERT INTO Orders (OrderNumber, CustomerName, Email, Phone, Address, City, Pincode, TotalAmount)
                            OUTPUT INSERTED.OrderId
                            VALUES (@OrderNumber, @CustomerName, @Email, @Phone, @Address, @City, @Pincode, @TotalAmount)";

                            SqlCommand orderCmd = new SqlCommand(insertOrderQuery, conn, transaction);
                            orderCmd.Parameters.AddWithValue("@OrderNumber", "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                            orderCmd.Parameters.AddWithValue("@CustomerName", order.Name ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Email", order.Email ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Phone", order.Phone ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Address", order.Address ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@City", order.City ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Pincode", order.Pincode ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@TotalAmount", order.Total);

                            int orderId = Convert.ToInt32(orderCmd.ExecuteScalar());

                            // 2️⃣ Insert Order Items
                            foreach (var item in order.Items)
                            {
                                string insertItemQuery = @"
                                INSERT INTO OrderItems (OrderId, ProductId, ProductName, Color, Size, HeelHeight, Quantity, Price)
                                VALUES (@OrderId, @ProductId, @ProductName, @Color, @Size, @HeelHeight, @Quantity, @Price)";

                                SqlCommand itemCmd = new SqlCommand(insertItemQuery, conn, transaction);
                                itemCmd.Parameters.AddWithValue("@OrderId", orderId);
                                itemCmd.Parameters.AddWithValue("@ProductId", item.Id);
                                itemCmd.Parameters.AddWithValue("@ProductName", item.Name ?? (object)DBNull.Value);
                                itemCmd.Parameters.AddWithValue("@Color", item.Color ?? (object)DBNull.Value);
                                itemCmd.Parameters.AddWithValue("@Size", item.Size ?? (object)DBNull.Value);
                                itemCmd.Parameters.AddWithValue("@HeelHeight", item.HeelHeight);
                                itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                itemCmd.Parameters.AddWithValue("@Price", item.Price);
                                itemCmd.ExecuteNonQuery();
                            }

                            // 3️⃣ Commit
                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error placing order: " + ex.Message);
                return false;
            }
        }


    }
}
