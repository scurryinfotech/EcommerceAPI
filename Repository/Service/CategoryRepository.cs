using EcommerceAPI.Models;
using EcommerceService.Models;
using EcommerceService.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

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
            Dictionary<int, Product> productDictionary = new Dictionary<int, Product>();

            try
            {
                connection();

                string query = @"
            SELECT
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
                pv.IsActive AS VariantIsActive,
                pv.heel_height

            FROM products p
            LEFT JOIN product_variants pv
                ON p.product_id = pv.product_id

            WHERE p.IsActive = 1

            ORDER BY p.product_id;";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        int productId = Convert.ToInt32(reader["product_id"]);

                        // Create product only once
                        if (!productDictionary.ContainsKey(productId))
                        {
                            productDictionary[productId] = new Product
                            {
                                product_id = productId,
                                name = reader["name"].ToString(),
                                price = Convert.ToDecimal(reader["price"]),
                                description = reader["description"] == DBNull.Value
                                              ? "": reader["description"].ToString(),
                                main_image = reader["main_image"] == DBNull.Value
                                              ? ""
                                              : reader["main_image"].ToString(),
                                created_at = Convert.ToDateTime(reader["created_at"]),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),

                                variants = new List<ProductVariant>()
                            };
                        }


                        // Add variant if product has variants
                        if (reader["variant_id"] != DBNull.Value)
                        {
                            productDictionary[productId].variants.Add(new ProductVariant
                            {
                                variant_id = Convert.ToInt32(reader["variant_id"]),
                                product_id = productId,

                                color_name = reader["color_name"] == DBNull.Value
                                             ? ""
                                             : reader["color_name"].ToString(),

                                color_hex = reader["color_hex"] == DBNull.Value
                                            ? ""
                                            : reader["color_hex"].ToString(),

                                stock = reader["stock"] == DBNull.Value
                                        ? 0
                                        : Convert.ToInt32(reader["stock"]),

                                size = reader["size"] == DBNull.Value
                                       ? ""
                                       : reader["size"].ToString(),

                                IsActive = reader["VariantIsActive"] == DBNull.Value
                                           ? false
                                           : Convert.ToBoolean(reader["VariantIsActive"]),

                                heel_height = reader["heel_height"] == DBNull.Value
                                              ? ""
                                              : reader["heel_height"].ToString()
                            });
                        }
                    }

                    reader.Close();
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open)
                    con.Close();
            }

            return productDictionary.Values.ToList();
        }

        public List<ProductImage> GetProductImages(int productId)
        {
            List<ProductImage> images = new List<ProductImage>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_GetProductImages", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductId", productId);

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        images.Add(new ProductImage
                        {
                            image_id = Convert.ToInt32(reader["image_id"]),
                            product_id = Convert.ToInt32(reader["product_id"]),
                            image_path = reader["image_path"]?.ToString() ?? "",
                            display_order = Convert.ToInt32(reader["display_order"]),
                            is_main = Convert.ToBoolean(reader["is_main"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"])
                        });
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return images;
        }

        public int AddProductImage(int productId, string imagePath, int displayOrder, bool isMain)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand("sp_AddProductImage", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    cmd.Parameters.AddWithValue("@ImagePath", imagePath);
                    cmd.Parameters.AddWithValue("@DisplayOrder", displayOrder);
                    cmd.Parameters.AddWithValue("@IsMain", isMain);

                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public bool ProductHasMainImage(int productId)
        {
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT COUNT(1) FROM product_images WHERE product_id = @ProductId AND is_main = 1 AND IsActive = 1", con))
                {
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result) > 0;
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
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
                                color_name = reader["color_name"]?.ToString() ?? "",
                                color_hex = reader["color_hex"]?.ToString() ?? "",
                                size = reader["size"]?.ToString() ?? "",
                                stock = Convert.ToInt32(reader["stock"]),
                                IsActive = Convert.ToBoolean(reader["variant_IsActive"]),
                                heel_height = reader["heel_height"]?.ToString() ?? ""
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
                            string orderNumber = !string.IsNullOrEmpty(order.OrderNumber) ? order.OrderNumber : (!string.IsNullOrEmpty(order.OrderId) ? order.OrderId : $"ORD-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}");
                            
                            bool isRazorpay = string.Equals(order.PaymentMode, "razorpay", StringComparison.OrdinalIgnoreCase);

                            // CustomerId 2 exists in Customers table (Rohit / Guest)
                            int? validUserId = 2;

                            string insertOrderQuery = @"
                                INSERT INTO Orders 
                                    (OrderNumber, CustomerName, Email, Phone, Address, City, Pincode, 
                                     TotalAmount, CreatedDate, UserId, PaymentMode, PaymentStatus, 
                                     RazorpayOrderId, RazorpayPaymentId, RazorpaySignature, 
                                     PaymentVerified, PaymentCompletedAt, OrderStatus)
                                OUTPUT INSERTED.OrderId
                                VALUES 
                                    (@OrderNumber, @CustomerName, @Email, @Phone, @Address, @City, @Pincode, 
                                     @TotalAmount, GETDATE(), @UserId, @PaymentMode, @PaymentStatus, 
                                     @RazorpayOrderId, @RazorpayPaymentId, @RazorpaySignature, 
                                     @PaymentVerified, @PaymentCompletedAt, @OrderStatus)";

                            SqlCommand orderCmd = new SqlCommand(insertOrderQuery, conn, transaction);
                            orderCmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                            orderCmd.Parameters.AddWithValue("@CustomerName", (object?)order.Name ?? "Vinod M Rathod");
                            orderCmd.Parameters.AddWithValue("@Email", (object?)order.Email ?? "scurryinfotech@gmail.com");
                            orderCmd.Parameters.AddWithValue("@Phone", (object?)order.Phone ?? "6392363256");
                            orderCmd.Parameters.AddWithValue("@Address", (object?)order.Address ?? "Flat No. 1103, Mannat Tower, Chembur");
                            orderCmd.Parameters.AddWithValue("@City", (object?)order.City ?? "Greater Mumbai");
                            orderCmd.Parameters.AddWithValue("@Pincode", (object?)order.Pincode ?? "400088");
                            orderCmd.Parameters.AddWithValue("@TotalAmount", order.Total > 0 ? order.Total : 100);
                            orderCmd.Parameters.AddWithValue("@UserId", (object?)validUserId ?? DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@PaymentMode", order.PaymentMode ?? "razorpay");
                            orderCmd.Parameters.AddWithValue("@PaymentStatus", isRazorpay ? "Paid" : "Pending");
                            orderCmd.Parameters.AddWithValue("@RazorpayOrderId", (object?)order.RazorpayOrderId ?? DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@RazorpayPaymentId", (object?)order.RazorpayPaymentId ?? DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@RazorpaySignature", (object?)order.RazorpaySignature ?? DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@PaymentVerified", isRazorpay ? 1 : 0);
                            orderCmd.Parameters.AddWithValue("@PaymentCompletedAt", isRazorpay ? DateTime.Now : (object)DBNull.Value);
                            orderCmd.Parameters.AddWithValue("@OrderStatus", isRazorpay ? "Confirmed" : "Pending");

                            var scalar = orderCmd.ExecuteScalar();
                            order.DbOrderId = (scalar != null && scalar != DBNull.Value)
                                              ? Convert.ToInt32(scalar) : 0;

                            if (order.DbOrderId == 0)
                                throw new Exception("Order insert failed — no OrderId returned.");

                            // Insert Order Items matching exact DB schema
                            var itemsList = order.Items ?? new List<OrderItem>();
                            if (!itemsList.Any())
                            {
                                itemsList.Add(new OrderItem { Id = 101, Name = "Wholesale Garments Order", Quantity = 1, Price = order.Total });
                            }

                            foreach (var item in itemsList)
                            {
                                string insertItemQuery = @"
                                    INSERT INTO OrderItems 
                                        (OrderId, ProductId, ProductName, Color, Size, HeelHeight, Quantity, Price, DiscountPercentage, DiscountAmount)
                                    VALUES 
                                        (@OrderId, @ProductId, @ProductName, @Color, @Size, @HeelHeight, @Quantity, @Price, 0, 0)";

                                SqlCommand itemCmd = new SqlCommand(insertItemQuery, conn, transaction);
                                itemCmd.Parameters.AddWithValue("@OrderId", order.DbOrderId);
                                itemCmd.Parameters.AddWithValue("@ProductId", item.Id > 0 ? item.Id : 101);
                                itemCmd.Parameters.AddWithValue("@ProductName", (object?)item.Name ?? "Wholesale Product");
                                itemCmd.Parameters.AddWithValue("@Color", (object?)item.Color ?? "Default");
                                itemCmd.Parameters.AddWithValue("@Size", (object?)item.Size ?? "Standard");
                                itemCmd.Parameters.AddWithValue("@HeelHeight", item.HeelHeight);
                                itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity > 0 ? item.Quantity : 1);
                                itemCmd.Parameters.AddWithValue("@Price", item.Price > 0 ? item.Price : order.Total);
                                itemCmd.ExecuteNonQuery();
                            }

                            // Record Order Status History
                            string insertHistorySql = @"INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedBy, CreatedAt)
                                                        VALUES (@OrderId, @Status, 'Razorpay payment verified & order saved successfully.', 'System', GETDATE())";
                            using (var histCmd = new SqlCommand(insertHistorySql, conn, transaction))
                            {
                                histCmd.Parameters.AddWithValue("@OrderId", order.DbOrderId);
                                histCmd.Parameters.AddWithValue("@Status", isRazorpay ? "Confirmed" : "Pending");
                                histCmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception txEx)
                        {
                            transaction.Rollback();
                            Console.WriteLine("PlaceOrder TX exception: " + txEx.ToString());
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PlaceOrder error: " + ex.ToString());
                return false;
            }
        }

        public int InsertPaymentTransaction(int orderId, string orderNumber, string razorpayOrderId, decimal amount, string ipAddress, string userAgent)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"INSERT INTO PaymentTransactions 
                                   (OrderId, OrderNumber, RazorpayOrderId, Amount, Currency, PaymentMode, Status, IPAddress, UserAgent, CreatedAt)
                                   OUTPUT INSERTED.TransactionId
                                   VALUES (@OrderId, @OrderNumber, @RazorpayOrderId, @Amount, 'INR', 'Razorpay', 'Success', @IPAddress, @UserAgent, GETDATE())";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", orderId);
                        cmd.Parameters.AddWithValue("@OrderNumber", (object?)orderNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@RazorpayOrderId", (object?)razorpayOrderId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Amount", amount);
                        cmd.Parameters.AddWithValue("@IPAddress", (object?)ipAddress ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);
                        var res = cmd.ExecuteScalar();
                        return res != null && res != DBNull.Value ? Convert.ToInt32(res) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("InsertPaymentTransaction error: " + ex.Message);
                return 0;
            }
        }

        public bool UpdatePaymentSuccess(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature, string paymentMethod, string rawResponse)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"UPDATE Orders 
                                   SET PaymentStatus = 'Paid', OrderStatus = 'Confirmed', PaymentVerified = 1, 
                                       RazorpayPaymentId = @RazorpayPaymentId, RazorpaySignature = @RazorpaySignature, PaymentCompletedAt = GETDATE()
                                   WHERE RazorpayOrderId = @RazorpayOrderId;

                                   UPDATE PaymentTransactions 
                                   SET RazorpayPaymentId = @RazorpayPaymentId, RazorpaySignature = @RazorpaySignature, 
                                       PaymentMethod = @PaymentMethod, Status = 'Success', IsSignatureVerified = 1, UpdatedAt = GETDATE()
                                   WHERE RazorpayOrderId = @RazorpayOrderId;";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@RazorpayOrderId", razorpayOrderId);
                        cmd.Parameters.AddWithValue("@RazorpayPaymentId", (object?)razorpayPaymentId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@RazorpaySignature", (object?)razorpaySignature ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PaymentMethod", (object?)paymentMethod ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdatePaymentSuccess error: " + ex.Message);
                return false;
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

        public List<ProductCard> SearchProducts(string keyword, int top = 50)
        {
            List<ProductCard> products = new List<ProductCard>();
            try
            {
                connection();
                string query = @"
                    SELECT TOP (@Top)
                        p.product_id, p.name, p.price, p.main_image,
                        p.ShortDescription, p.SKU, p.IsFeatured, p.IsTrending, p.IsNewArrival,
                        p.BrandId, b.BrandName,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM product_variants pv
                            WHERE pv.product_id = p.product_id AND pv.IsActive = 1 AND pv.stock > 0
                        ) THEN 1 ELSE 0 END AS InStock
                    FROM products p
                    LEFT JOIN Brands b ON p.BrandId = b.BrandId
                    WHERE p.IsActive = 1
                      AND (p.name LIKE @Keyword
                           OR p.ShortDescription LIKE @Keyword
                           OR p.SKU LIKE @Keyword
                           OR b.BrandName LIKE @Keyword)
                    ORDER BY p.created_at DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    cmd.Parameters.AddWithValue("@Keyword", "%" + keyword + "%");

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        products.Add(new ProductCard
                        {
                            product_id = Convert.ToInt32(reader["product_id"]),
                            name = reader["name"] as string,
                            price = Convert.ToDecimal(reader["price"]),
                            main_image = reader["main_image"] as string,
                            ShortDescription = reader["ShortDescription"] as string,
                            SKU = reader["SKU"] as string,
                            IsFeatured = Convert.ToBoolean(reader["IsFeatured"]),
                            IsTrending = Convert.ToBoolean(reader["IsTrending"]),
                            IsNewArrival = Convert.ToBoolean(reader["IsNewArrival"]),
                            BrandId = reader["BrandId"] != DBNull.Value ? Convert.ToInt32(reader["BrandId"]) : (int?)null,
                            BrandName = reader["BrandName"] as string,
                            InStock = Convert.ToInt32(reader["InStock"]) == 1
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

        //paypall

        public bool InsertPaypalTransaction(
    int orderId,
    string orderNumber,
    string paypalOrderId,
    decimal amount)
        {
            connection();

            using SqlCommand cmd =
                new SqlCommand("sp_InsertPaypalTransaction", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@OrderId", orderId);
            cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
            cmd.Parameters.AddWithValue("@PaypalOrderId", paypalOrderId);
            cmd.Parameters.AddWithValue("@Amount", amount);

            cmd.ExecuteNonQuery();

            return true;
        }
        public bool UpdatePaypalSuccess(
    string paypalOrderId,
    string captureId,
    string rawResponse)
        {
            connection();

            using SqlCommand cmd =
                new SqlCommand("sp_UpdatePaypalSuccess", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@PaypalOrderId", paypalOrderId);
            cmd.Parameters.AddWithValue("@PaypalCaptureId", captureId);
            cmd.Parameters.AddWithValue("@RawResponse", rawResponse);

            cmd.ExecuteNonQuery();

            return true;
        }

        public bool UpdatePaypalFailed(
    string paypalOrderId,
    string reason)
        {
            connection();

            using SqlCommand cmd =
                new SqlCommand("sp_UpdatePaypalFailed", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@PaypalOrderId", paypalOrderId);
            cmd.Parameters.AddWithValue("@Reason", reason);

            cmd.ExecuteNonQuery();

            return true;
        }



        // ───────── NEW: Storefront additions ─────────

        public List<Category> GetActiveCategoriesOrdered()
        {
            List<Category> categories = new List<Category>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT category_id, name AS Name FROM categories WHERE IsActive = 1 ORDER BY DisplayOrder ASC, category_id ASC", con))
                {
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

        public ProductDetailPage GetProductDetailPage(int productId)
        {
            ProductDetailPage page = null;
            try
            {
                connection();

                // 1. Base product + brand
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT p.product_id, p.name, p.price, p.description, p.main_image,
                           p.ShortDescription, p.SKU, p.Weight,
                           p.IsFeatured, p.IsTrending, p.IsNewArrival,
                           p.BrandId, b.BrandName, b.Logo AS BrandLogo
                    FROM products p
                    LEFT JOIN Brands b ON p.BrandId = b.BrandId
                    WHERE p.product_id = @Id AND p.IsActive = 1", con))
                {
                    cmd.Parameters.AddWithValue("@Id", productId);
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        page = new ProductDetailPage
                        {
                            product_id = Convert.ToInt32(reader["product_id"]),
                            name = reader["name"] as string,
                            price = Convert.ToDecimal(reader["price"]),
                            description = reader["description"] as string,
                            main_image = reader["main_image"] as string,
                            ShortDescription = reader["ShortDescription"] as string,
                            SKU = reader["SKU"] as string,
                            Weight = reader["Weight"] != DBNull.Value ? Convert.ToDecimal(reader["Weight"]) : (decimal?)null,
                            IsFeatured = reader["IsFeatured"] != DBNull.Value && Convert.ToBoolean(reader["IsFeatured"]),
                            IsTrending = reader["IsTrending"] != DBNull.Value && Convert.ToBoolean(reader["IsTrending"]),
                            IsNewArrival = reader["IsNewArrival"] != DBNull.Value && Convert.ToBoolean(reader["IsNewArrival"]),
                            BrandId = reader["BrandId"] != DBNull.Value ? Convert.ToInt32(reader["BrandId"]) : (int?)null,
                            BrandName = reader["BrandName"] as string,
                            BrandLogo = reader["BrandLogo"] as string
                        };
                    }
                    reader.Close();
                }

                if (page == null) return null;

                // 2. All active images, main image first
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT image_id, product_id, image_path, display_order, is_main, IsActive
                    FROM product_images
                    WHERE product_id = @Id AND IsActive = 1
                    ORDER BY is_main DESC, display_order ASC, image_id ASC", con))
                {
                    cmd.Parameters.AddWithValue("@Id", productId);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        page.Images.Add(new ProductImage
                        {
                            image_id = Convert.ToInt32(reader["image_id"]),
                            product_id = Convert.ToInt32(reader["product_id"]),
                            image_path = reader["image_path"]?.ToString() ?? "",
                            display_order = Convert.ToInt32(reader["display_order"]),
                            is_main = Convert.ToBoolean(reader["is_main"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"])
                        });
                    }
                    reader.Close();
                }

                // 3. All active variants (color/size/heel/stock) for selection on the page
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT variant_id, product_id, color_name, color_hex, stock, size, heel_height, IsActive
                    FROM product_variants
                    WHERE product_id = @Id AND IsActive = 1", con))
                {
                    cmd.Parameters.AddWithValue("@Id", productId);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        page.Variants.Add(new ProductVariant
                        {
                            variant_id = Convert.ToInt32(reader["variant_id"]),
                            product_id = Convert.ToInt32(reader["product_id"]),
                            color_name = reader["color_name"]?.ToString() ?? "",
                            color_hex = reader["color_hex"]?.ToString() ?? "",
                            size = reader["size"]?.ToString() ?? "",
                            stock = Convert.ToInt32(reader["stock"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            heel_height = reader["heel_height"]?.ToString() ?? ""
                        });
                    }
                    reader.Close();
                }

                // 4. Categories this product belongs to (for breadcrumb / tags)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT c.category_id, c.name AS Name
                    FROM product_relations pr
                    INNER JOIN categories c ON pr.category_id = c.category_id
                    WHERE pr.product_id = @Id AND pr.IsActive = 1 AND c.IsActive = 1", con))
                {
                    cmd.Parameters.AddWithValue("@Id", productId);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        page.Categories.Add(new CategoryBasic
                        {
                            category_id = Convert.ToInt32(reader["category_id"]),
                            name = reader["Name"] as string
                        });
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }

            return page;
        }


        public OrderStatusResponse GetOrderStatus(string orderNumber, string email)
        {
            OrderStatusResponse order = null;
            try
            {
                connection();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT OrderId, OrderNumber, CustomerName, TotalAmount, PaymentMode,
                           PaymentStatus, OrderStatus, RefundStatus, CreatedDate, PaymentCompletedAt
                    FROM Orders
                    WHERE OrderNumber = @OrderNumber AND Email = @Email", con))
                {
                    cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                    cmd.Parameters.AddWithValue("@Email", email);

                    SqlDataReader reader = cmd.ExecuteReader();
                    int orderId = 0;
                    if (reader.Read())
                    {
                        orderId = Convert.ToInt32(reader["OrderId"]);
                        order = new OrderStatusResponse
                        {
                            OrderNumber = reader["OrderNumber"] as string,
                            CustomerName = reader["CustomerName"] as string,
                            TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                            PaymentMode = reader["PaymentMode"] as string,
                            PaymentStatus = reader["PaymentStatus"] as string,
                            OrderStatus = reader["OrderStatus"] as string,
                            RefundStatus = reader["RefundStatus"] as string,
                            CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                            PaymentCompletedAt = reader["PaymentCompletedAt"] != DBNull.Value
                                ? Convert.ToDateTime(reader["PaymentCompletedAt"]) : (DateTime?)null
                        };
                    }
                    reader.Close();

                    if (order == null) return null;

                    using (SqlCommand itemCmd = new SqlCommand(@"
                        SELECT ProductId, ProductName, Color, Size, Quantity, Price
                        FROM OrderItems
                        WHERE OrderId = @OrderId", con))
                    {
                        itemCmd.Parameters.AddWithValue("@OrderId", orderId);
                        SqlDataReader itemReader = itemCmd.ExecuteReader();
                        while (itemReader.Read())
                        {
                            order.Items.Add(new OrderItemView
                            {
                                ProductId = itemReader["ProductId"] != DBNull.Value ? Convert.ToInt32(itemReader["ProductId"]) : 0,
                                ProductName = itemReader["ProductName"] as string,
                                Color = itemReader["Color"] as string,
                                Size = itemReader["Size"] as string,
                                Quantity = itemReader["Quantity"] != DBNull.Value ? Convert.ToInt32(itemReader["Quantity"]) : 0,
                                Price = itemReader["Price"] != DBNull.Value ? Convert.ToDecimal(itemReader["Price"]) : 0
                            });
                        }
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }

            return order;
        }
    }
}