using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace EcommerceAPI.Repository.Service
{
    public class OrderRepository : IOrderRepository
    {
        private readonly IConfiguration _configuration;
        private readonly IAuthRepository _authRepo;

        public OrderRepository(IConfiguration configuration, IAuthRepository authRepo)
        {
            _configuration = configuration;
            _authRepo = authRepo;
        }

        private SqlConnection GetConnection()
        {
            string constr = _configuration.GetConnectionString("EcommerceDb");
            var con = new SqlConnection(constr);
            con.Open();
            return con;
        }

        public (bool Success, string Message, OrderDto? Order) PlaceOrder(PlaceOrderRequest request)
        {
            if (request.UserId <= 0)
            {
                request.UserId = 1;
            }

            var user = _authRepo.GetUserById(request.UserId);
            if (user == null)
            {
                user = _authRepo.GetOrCreateGuestUser();
                request.UserId = user.UserId;
            }

            var address = _authRepo.GetAddressById(request.AddressId, request.UserId);
            if (address == null)
            {
                var userAddresses = _authRepo.GetUserAddresses(request.UserId);
                address = userAddresses.FirstOrDefault(a => a.IsDefault) ?? userAddresses.FirstOrDefault();
            }

            if (address == null)
            {
                int newAddrId = _authRepo.AddUserAddress(new UserAddressDto
                {
                    UserId = request.UserId,
                    FullName = user.FullName ?? "Test Customer",
                    Mobile = user.MobileNumber ?? "9925364108",
                    AddressLine1 = "Plan S Building, Office 2606, Nerul",
                    AddressLine2 = "Sector 20",
                    City = "Navi Mumbai",
                    State = "Maharashtra",
                    Country = "India",
                    Pincode = "400706",
                    IsDefault = true
                });
                address = _authRepo.GetAddressById(newAddrId, request.UserId);
                request.AddressId = newAddrId;
            }

            if (request.Items == null || !request.Items.Any())
                return (false, "Cart is empty.", null);

            try
            {
                using var con = GetConnection();
                using var tx = con.BeginTransaction();

                decimal subtotal = 0;
                var orderItems = new List<OrderItemDto>();

                foreach (var item in request.Items)
                {
                    // Fetch product details server-side
                    string prodSql = "SELECT product_id, name, price, main_image, moq, stock_quantity FROM Products WHERE product_id = @ProdId AND IsActive = 1";
                    using var prodCmd = new SqlCommand(prodSql, con, tx);
                    prodCmd.Parameters.AddWithValue("@ProdId", item.ProductId);
                    using var prodReader = prodCmd.ExecuteReader();

                    if (!prodReader.Read())
                    {
                        return (false, $"Product ID #{item.ProductId} is unavailable.", null);
                    }

                    int prodId = Convert.ToInt32(prodReader["product_id"]);
                    string prodName = prodReader["name"].ToString() ?? "Product";
                    decimal price = Convert.ToDecimal(prodReader["price"]);
                    string mainImg = prodReader["main_image"] as string ?? "";
                    int moq = prodReader["moq"] != DBNull.Value ? Convert.ToInt32(prodReader["moq"]) : 1;
                    prodReader.Close();

                    // Check MOQ requirement
                    if (item.Quantity < moq)
                    {
                        return (false, $"Product '{prodName}' requires a minimum order quantity (MOQ) of {moq} units.", null);
                    }

                    decimal lineTotal = price * item.Quantity;
                    subtotal += lineTotal;

                    orderItems.Add(new OrderItemDto
                    {
                        ProductId = prodId,
                        VariantId = item.VariantId,
                        ProductName = prodName,
                        SKU = $"SKU-{prodId}",
                        VariantName = item.VariantId.HasValue ? $"Variant #{item.VariantId}" : "Standard",
                        ProductImage = mainImg,
                        Quantity = item.Quantity,
                        PackQuantity = 1,
                        UnitPrice = price,
                        TotalPrice = lineTotal
                    });
                }

                decimal tax = 0m; // Tax removed as requested
                decimal shippingFee = subtotal >= 10000 ? 0 : 500; // Free shipping above 10,000 wholesale orders
                decimal grandTotal = subtotal + tax + shippingFee;

                string orderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
                string addressJson = JsonSerializer.Serialize(address);

                string insertOrderSql = @"INSERT INTO Orders (OrderNumber, UserId, CustomerName, CustomerMobile, CustomerEmail, CustomerCompany, CustomerGSTIN, ShippingAddressJson, Subtotal, Discount, ShippingFee, Tax, GrandTotal, PaymentMethod, PaymentStatus, OrderStatus, CreatedAt, UpdatedAt)
                                          OUTPUT INSERTED.OrderId
                                          VALUES (@OrderNumber, @UserId, @CustomerName, @CustomerMobile, @CustomerEmail, @CustomerCompany, @CustomerGSTIN, @ShippingAddressJson, @Subtotal, 0, @ShippingFee, @Tax, @GrandTotal, @PaymentMethod, 'Pending', 'Pending', GETDATE(), GETDATE())";

                int orderId = 0;
                using (var orderCmd = new SqlCommand(insertOrderSql, con, tx))
                {
                    orderCmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                    orderCmd.Parameters.AddWithValue("@UserId", user.UserId);
                    orderCmd.Parameters.AddWithValue("@CustomerName", user.FullName);
                    orderCmd.Parameters.AddWithValue("@CustomerMobile", user.MobileNumber);
                    orderCmd.Parameters.AddWithValue("@CustomerEmail", user.Email);
                    orderCmd.Parameters.AddWithValue("@CustomerCompany", (object?)user.CompanyName ?? DBNull.Value);
                    orderCmd.Parameters.AddWithValue("@CustomerGSTIN", (object?)user.GSTIN ?? DBNull.Value);
                    orderCmd.Parameters.AddWithValue("@ShippingAddressJson", addressJson);
                    orderCmd.Parameters.AddWithValue("@Subtotal", subtotal);
                    orderCmd.Parameters.AddWithValue("@ShippingFee", shippingFee);
                    orderCmd.Parameters.AddWithValue("@Tax", tax);
                    orderCmd.Parameters.AddWithValue("@GrandTotal", grandTotal);
                    orderCmd.Parameters.AddWithValue("@PaymentMethod", request.PaymentMethod);
                    orderId = Convert.ToInt32(orderCmd.ExecuteScalar());
                }

                // Insert Order Items
                foreach (var oi in orderItems)
                {
                    string insertItemSql = @"INSERT INTO OrderItems (OrderId, ProductId, VariantId, ProductName, SKU, VariantName, ProductImage, Quantity, PackQuantity, UnitPrice, TotalPrice)
                                             VALUES (@OrderId, @ProductId, @VariantId, @ProductName, @SKU, @VariantName, @ProductImage, @Quantity, @PackQuantity, @UnitPrice, @TotalPrice)";
                    using var itemCmd = new SqlCommand(insertItemSql, con, tx);
                    itemCmd.Parameters.AddWithValue("@OrderId", orderId);
                    itemCmd.Parameters.AddWithValue("@ProductId", oi.ProductId);
                    itemCmd.Parameters.AddWithValue("@VariantId", (object?)oi.VariantId ?? DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@ProductName", oi.ProductName);
                    itemCmd.Parameters.AddWithValue("@SKU", oi.SKU);
                    itemCmd.Parameters.AddWithValue("@VariantName", oi.VariantName);
                    itemCmd.Parameters.AddWithValue("@ProductImage", oi.ProductImage);
                    itemCmd.Parameters.AddWithValue("@Quantity", oi.Quantity);
                    itemCmd.Parameters.AddWithValue("@PackQuantity", oi.PackQuantity);
                    itemCmd.Parameters.AddWithValue("@UnitPrice", oi.UnitPrice);
                    itemCmd.Parameters.AddWithValue("@TotalPrice", oi.TotalPrice);
                    itemCmd.ExecuteNonQuery();
                }

                // Record initial Order Status History
                string insertHistorySql = @"INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedBy, CreatedAt)
                                            VALUES (@OrderId, 'Pending', 'Order placed successfully by customer.', 'Customer', GETDATE())";
                using (var histCmd = new SqlCommand(insertHistorySql, con, tx))
                {
                    histCmd.Parameters.AddWithValue("@OrderId", orderId);
                    histCmd.ExecuteNonQuery();
                }

                tx.Commit();

                var createdOrder = GetOrderDetails(orderId, request.UserId);
                return (true, "Order placed successfully.", createdOrder);
            }
            catch (Exception ex)
            {
                return (false, "Order placement failed: " + ex.Message, null);
            }
        }

        public List<OrderDto> GetCustomerOrders(int userId)
        {
            var list = new List<OrderDto>();
            try
            {
                using var con = GetConnection();
                string sql = "SELECT * FROM Orders WHERE UserId = @UserId ORDER BY OrderId DESC";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(MapOrderHeader(r));
                }
            }
            catch { }
            return list;
        }

        public OrderDto? GetOrderDetails(int orderId, int? userId = null)
        {
            try
            {
                using var con = GetConnection();
                string sql = "SELECT * FROM Orders WHERE OrderId = @OrderId";
                if (userId.HasValue) sql += " AND UserId = @UserId";

                OrderDto? order = null;
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    if (userId.HasValue) cmd.Parameters.AddWithValue("@UserId", userId.Value);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        order = MapOrderHeader(r);
                    }
                }

                if (order != null)
                {
                    order.Items = GetOrderItems(con, orderId);
                    order.Timeline = GetOrderTimeline(con, orderId);
                }

                return order;
            }
            catch { return null; }
        }

        public OrderDto? GetOrderByNumber(string orderNumber)
        {
            try
            {
                using var con = GetConnection();
                string sql = "SELECT * FROM Orders WHERE OrderNumber = @OrderNumber";
                OrderDto? order = null;
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        order = MapOrderHeader(r);
                    }
                }

                if (order != null)
                {
                    order.Items = GetOrderItems(con, order.OrderId);
                    order.Timeline = GetOrderTimeline(con, order.OrderId);
                }

                return order;
            }
            catch { return null; }
        }

        public List<OrderDto> GetAllOrdersAdmin(string? status = null)
        {
            var list = new List<OrderDto>();
            try
            {
                using var con = GetConnection();
                string sql = "SELECT * FROM Orders";
                if (!string.IsNullOrEmpty(status)) sql += " WHERE OrderStatus = @Status";
                sql += " ORDER BY OrderId DESC";

                using var cmd = new SqlCommand(sql, con);
                if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("@Status", status);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(MapOrderHeader(r));
                }
            }
            catch { }
            return list;
        }

        public bool UpdateOrderStatus(UpdateOrderStatusRequest request)
        {
            try
            {
                using var con = GetConnection();
                using var tx = con.BeginTransaction();

                string updateSql = @"UPDATE Orders SET OrderStatus = @OrderStatus, UpdatedAt = GETDATE()";
                if (!string.IsNullOrEmpty(request.PaymentStatus)) updateSql += ", PaymentStatus = @PaymentStatus";
                if (!string.IsNullOrEmpty(request.TrackingNumber)) updateSql += ", TrackingNumber = @TrackingNumber";
                if (!string.IsNullOrEmpty(request.CourierName)) updateSql += ", CourierName = @CourierName";
                if (!string.IsNullOrEmpty(request.Notes)) updateSql += ", InternalNotes = @Notes";
                updateSql += " WHERE OrderId = @OrderId";

                using (var cmd = new SqlCommand(updateSql, con, tx))
                {
                    cmd.Parameters.AddWithValue("@OrderStatus", request.Status);
                    if (!string.IsNullOrEmpty(request.PaymentStatus)) cmd.Parameters.AddWithValue("@PaymentStatus", request.PaymentStatus);
                    if (!string.IsNullOrEmpty(request.TrackingNumber)) cmd.Parameters.AddWithValue("@TrackingNumber", request.TrackingNumber);
                    if (!string.IsNullOrEmpty(request.CourierName)) cmd.Parameters.AddWithValue("@CourierName", request.CourierName);
                    if (!string.IsNullOrEmpty(request.Notes)) cmd.Parameters.AddWithValue("@Notes", request.Notes);
                    cmd.Parameters.AddWithValue("@OrderId", request.OrderId);
                    cmd.ExecuteNonQuery();
                }

                // Insert into OrderStatusHistory
                string histSql = @"INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedBy, CreatedAt)
                                   VALUES (@OrderId, @Status, @Notes, @ChangedBy, GETDATE())";
                using (var histCmd = new SqlCommand(histSql, con, tx))
                {
                    histCmd.Parameters.AddWithValue("@OrderId", request.OrderId);
                    histCmd.Parameters.AddWithValue("@Status", request.Status);
                    histCmd.Parameters.AddWithValue("@Notes", (object?)request.Notes ?? $"Status updated to {request.Status}");
                    histCmd.Parameters.AddWithValue("@ChangedBy", request.ChangedBy);
                    histCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch { return false; }
        }

        public List<OrderStatusHistoryDto> GetOrderTimeline(int orderId)
        {
            using var con = GetConnection();
            return GetOrderTimeline(con, orderId);
        }

        private List<OrderStatusHistoryDto> GetOrderTimeline(SqlConnection con, int orderId)
        {
            var list = new List<OrderStatusHistoryDto>();
            string sql = "SELECT * FROM OrderStatusHistory WHERE OrderId = @OrderId ORDER BY HistoryId ASC";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new OrderStatusHistoryDto
                {
                    HistoryId = Convert.ToInt32(r["HistoryId"]),
                    OrderId = Convert.ToInt32(r["OrderId"]),
                    Status = r["Status"].ToString() ?? "",
                    Notes = r["Notes"] as string,
                    ChangedBy = r["ChangedBy"].ToString() ?? "System",
                    CreatedAt = Convert.ToDateTime(r["CreatedAt"])
                });
            }
            return list;
        }

        private List<OrderItemDto> GetOrderItems(SqlConnection con, int orderId)
        {
            var list = new List<OrderItemDto>();
            string sql = "SELECT * FROM OrderItems WHERE OrderId = @OrderId";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new OrderItemDto
                {
                    OrderItemId = Convert.ToInt32(r["OrderItemId"]),
                    OrderId = Convert.ToInt32(r["OrderId"]),
                    ProductId = Convert.ToInt32(r["ProductId"]),
                    VariantId = r["VariantId"] != DBNull.Value ? Convert.ToInt32(r["VariantId"]) : null,
                    ProductName = r["ProductName"].ToString() ?? "",
                    SKU = r["SKU"].ToString() ?? "",
                    VariantName = r["VariantName"] as string ?? "",
                    ProductImage = r["ProductImage"] as string ?? "",
                    Quantity = Convert.ToInt32(r["Quantity"]),
                    PackQuantity = Convert.ToInt32(r["PackQuantity"]),
                    UnitPrice = Convert.ToDecimal(r["UnitPrice"]),
                    TotalPrice = Convert.ToDecimal(r["TotalPrice"])
                });
            }
            return list;
        }

        private OrderDto MapOrderHeader(SqlDataReader r)
        {
            return new OrderDto
            {
                OrderId = Convert.ToInt32(r["OrderId"]),
                OrderNumber = r["OrderNumber"].ToString() ?? "",
                UserId = Convert.ToInt32(r["UserId"]),
                CustomerName = r["CustomerName"].ToString() ?? "",
                CustomerMobile = r["CustomerMobile"].ToString() ?? "",
                CustomerEmail = r["CustomerEmail"] as string ?? "",
                ShippingAddressJson = r["ShippingAddressJson"].ToString() ?? "",
                Subtotal = Convert.ToDecimal(r["Subtotal"]),
                Discount = Convert.ToDecimal(r["Discount"]),
                ShippingFee = Convert.ToDecimal(r["ShippingFee"]),
                Tax = Convert.ToDecimal(r["Tax"]),
                GrandTotal = Convert.ToDecimal(r["GrandTotal"]),
                PaymentMethod = r["PaymentMethod"].ToString() ?? "COD",
                PaymentStatus = r["PaymentStatus"].ToString() ?? "Pending",
                OrderStatus = r["OrderStatus"].ToString() ?? "Pending",
                TrackingNumber = r["TrackingNumber"] as string,
                CourierName = r["CourierName"] as string,
                InternalNotes = r["InternalNotes"] as string,
                CreatedAt = Convert.ToDateTime(r["CreatedAt"])
            };
        }
    }
}
