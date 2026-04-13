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

        public int GetOrderIdByOrderNumber(string orderNumber)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("SELECT OrderId FROM Orders WHERE OrderNumber = @OrderNumber", con))
                {
                    cmd.Parameters.AddWithValue("@OrderNumber", orderNumber ?? (object)DBNull.Value);
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        private void connection()
        {
            string constr = this._configuration.GetConnectionString("EcommerceDb");
            con = new SqlConnection(constr);
            if (con.State == ConnectionState.Closed)
                con.Open();
        }

        // ─────────────────────────────────────────────────────────────
        // EXISTING METHODS (unchanged)
        // ─────────────────────────────────────────────────────────────

        public List<Category> GetCategories()
        {
            List<Category> categories = new List<Category>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_GetCategories", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        categories.Add(new Category
                        {
                            category_id = Convert.ToInt32(reader["category_id"]),
                            Name = reader["Name"].ToString()
                        });
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return categories;
        }

        public List<Product> GetProducts()
        {
            List<Product> products = new List<Product>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM products WHERE IsActive = 1", con))
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
                if (con.State == ConnectionState.Open) con.Close();
            }
            return products;
        }

        public List<ProductVariant> GetProductsVariants(int id, List<ProductVariant>? productVariants)
        {
            Product foundProduct = null;
            try
            {
                connection();
                string query = @"
                    SELECT 
                        p.product_id, p.name, p.price, p.description,
                        p.main_image, p.created_at, p.IsActive,
                        pv.variant_id, pv.color_name, pv.color_hex,
                        pv.stock, pv.size, pv.heel_height,
                        pv.IsActive AS variant_IsActive
                    FROM [dbo].[products] p
                    LEFT JOIN [dbo].[product_variants] pv ON p.product_id = pv.product_id
                    WHERE p.product_id = @id
                      AND p.IsActive = 1
                      AND (pv.IsActive = 1 OR pv.IsActive IS NULL)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
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
                if (con.State == ConnectionState.Open) con.Close();
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
                            string insertOrderQuery = @"
                        INSERT INTO Orders 
                            (OrderNumber, CustomerName, Email, Phone, Address, City,
                             Pincode, TotalAmount, PaymentMode, PaymentStatus, OrderStatus,
                             RazorpayOrderId, RazorpayPaymentId, RazorpaySignature,
                             PaymentVerified, PaymentCompletedAt, CreatedDate)
                        OUTPUT INSERTED.OrderId
                        VALUES 
                            (@OrderNumber, @CustomerName, @Email, @Phone, @Address, @City,
                             @Pincode, @TotalAmount, @PaymentMode, @PaymentStatus, @OrderStatus,
                             @RazorpayOrderId, @RazorpayPaymentId, @RazorpaySignature,
                             @PaymentVerified, @PaymentCompletedAt, GETDATE())";

                            bool isRazorpay = order.PaymentMode?.ToLower() == "razorpay";

                            SqlCommand orderCmd = new SqlCommand(insertOrderQuery, conn, transaction);
                            orderCmd.Parameters.AddWithValue("@OrderNumber", order.OrderNumber ?? "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                            orderCmd.Parameters.AddWithValue("@CustomerName", order.Name ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Email", order.Email ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Phone", order.Phone ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Address", order.Address ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@City", order.City ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@Pincode", order.Pincode ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@TotalAmount", order.Total);
                            orderCmd.Parameters.AddWithValue("@PaymentMode", order.PaymentMode ?? "COD");
                            orderCmd.Parameters.AddWithValue("@PaymentStatus", isRazorpay ? "Paid" : "Pending");
                            orderCmd.Parameters.AddWithValue("@OrderStatus", isRazorpay ? "Confirmed" : "Pending");
                            orderCmd.Parameters.AddWithValue("@RazorpayOrderId", order.RazorpayOrderId ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@RazorpayPaymentId", order.RazorpayPaymentId ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@RazorpaySignature", order.RazorpaySignature ?? (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@PaymentVerified", isRazorpay ? 1 : 0);
                            orderCmd.Parameters.AddWithValue("@PaymentCompletedAt", isRazorpay ? DateTime.Now : (object)DBNull.Value);

                            var scalar = orderCmd.ExecuteScalar();
                            order.DbOrderId = (scalar != null && scalar != DBNull.Value)
                                              ? Convert.ToInt32(scalar) : 0;

                            if (order.DbOrderId == 0)
                                throw new Exception("Order insert failed — no OrderId returned.");

                            // Insert Order Items
                            foreach (var item in order.Items)
                            {
                                string insertItemQuery = @"
                            INSERT INTO OrderItems 
                                (OrderId, ProductId, ProductName, Color, Size, HeelHeight, Quantity, Price)
                            VALUES 
                                (@OrderId, @ProductId, @ProductName, @Color, @Size, @HeelHeight, @Quantity, @Price)";

                                SqlCommand itemCmd = new SqlCommand(insertItemQuery, conn, transaction);
                                itemCmd.Parameters.AddWithValue("@OrderId", order.DbOrderId);
                                itemCmd.Parameters.AddWithValue("@ProductId", item.Id);
                                itemCmd.Parameters.AddWithValue("@ProductName", item.Name ?? (object)DBNull.Value);
                                itemCmd.Parameters.AddWithValue("@Color", item.Color ?? (object)DBNull.Value);
                                itemCmd.Parameters.AddWithValue("@Size", item.Size ?? (object)DBNull.Value);
                                itemCmd.Parameters.AddWithValue("@HeelHeight", item.HeelHeight);
                                itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                itemCmd.Parameters.AddWithValue("@Price", item.Price);
                                itemCmd.ExecuteNonQuery();
                            }

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
                Console.WriteLine("PlaceOrder error: " + ex.Message);
                return false;
            }
        }



        public int InsertPaymentTransaction(int orderId, string orderNumber, string razorpayOrderId, decimal amount, string ipAddress, string userAgent)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_InsertPaymentTransaction", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                    cmd.Parameters.AddWithValue("@RazorpayOrderId", razorpayOrderId);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@PaymentMode", "Razorpay");
                    cmd.Parameters.AddWithValue("@IPAddress", ipAddress ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserAgent", userAgent ?? (object)DBNull.Value);
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool UpdatePaymentSuccess(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature, string paymentMethod, string rawResponse)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_UpdatePaymentSuccess", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@RazorpayOrderId", razorpayOrderId);
                    cmd.Parameters.AddWithValue("@RazorpayPaymentId", razorpayPaymentId);
                    cmd.Parameters.AddWithValue("@RazorpaySignature", razorpaySignature);
                    cmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RawResponse", rawResponse ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdatePaymentSuccess error: " + ex.Message);
                return false;
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool UpdatePaymentFailed(string razorpayOrderId, string razorpayPaymentId, string failureReason, string failureCode, string rawResponse)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_UpdatePaymentFailed", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@RazorpayOrderId", razorpayOrderId);
                    cmd.Parameters.AddWithValue("@RazorpayPaymentId", razorpayPaymentId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FailureReason", failureReason ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FailureCode", failureCode ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RawResponse", rawResponse ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdatePaymentFailed error: " + ex.Message);
                return false;
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool InsertWebhookLog(string eventId, string eventType, string razorpayOrderId, string razorpayPaymentId, string razorpayRefundId, decimal amount, string rawPayload)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_InsertWebhookLog", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@EventId", eventId);
                    cmd.Parameters.AddWithValue("@EventType", eventType);
                    cmd.Parameters.AddWithValue("@RazorpayOrderId", razorpayOrderId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RazorpayPaymentId", razorpayPaymentId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RazorpayRefundId", razorpayRefundId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@RawPayload", rawPayload ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("InsertWebhookLog error: " + ex.Message);
                return false;
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool UpdateWebhookProcessed(string eventId, string errorMessage)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_UpdateWebhookProcessed", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@EventId", eventId);
                    cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateWebhookProcessed error: " + ex.Message);
                return false;
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }
    }
}