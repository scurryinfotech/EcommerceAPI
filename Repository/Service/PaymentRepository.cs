using EcommerceAPI.Models;
using EcommerceService.Models;
using EcommerceService.Repository.Interface;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcommerceService.Repository.Service
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly IConfiguration _config;
        private readonly string _conn;
        private readonly string _merchantId;
        private readonly string _saltKey;
        private readonly string _saltIndex;
        private readonly string _baseUrl;

        public PaymentRepository(IConfiguration config)
        {
            _config = config;
            _conn = config.GetConnectionString("EcommerceDb");
            _merchantId = config["PhonePe:MerchantId"];
            _saltKey = config["PhonePe:SaltKey"];
            _saltIndex = config["PhonePe:SaltIndex"];
            _baseUrl = config["PhonePe:BaseUrl"];
        }

        public async Task<string> InitiatePayment(OrderRequest order, string baseCallbackUrl)
        {
            string transactionId = "TXN" + DateTime.Now.Ticks;

            var payload = new
            {
                merchantId = _merchantId,
                merchantTransactionId = transactionId,
                merchantUserId = order.Phone,
                amount = (long)(order.Total * 100),
                redirectUrl = $"{baseCallbackUrl}/api/payment/callback",
                redirectMode = "REDIRECT",
                callbackUrl = $"{baseCallbackUrl}/api/payment/callback",
                paymentInstrument = new { type = "PAY_PAGE" }
            };

            var payloadJson = JsonSerializer.Serialize(payload);
            var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));

            var checksumRaw = base64Payload + "/pg/v1/pay" + _saltKey;
            var checksumHash = ComputeSha256(checksumRaw) + "###" + _saltIndex;

            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/pg/v1/pay");
            request.Headers.Add("X-VERIFY", checksumHash);
            request.Headers.Add("X-MERCHANT-ID", _merchantId);
            request.Content = new StringContent(JsonSerializer.Serialize(new { request = base64Payload }), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return body;
        }

        public bool SaveOrderAfterPayment(OrderRequest order, string paymentStatus)
        {
            using SqlConnection conn = new SqlConnection(_conn);
            conn.Open();

            using SqlTransaction tx = conn.BeginTransaction();
            try
            {
                string orderQuery = @"
                    INSERT INTO Orders (OrderNumber, CustomerName, Email, Phone, Address, City, Pincode, TotalAmount, PaymentMode, PaymentStatus, CreatedDate)
                    OUTPUT INSERTED.OrderId
                    VALUES (@OrderNumber, @CustomerName, @Email, @Phone, @Address, @City, @Pincode, @TotalAmount, @PaymentMethod, @PaymentStatus, GETDATE())";

                SqlCommand orderCmd = new SqlCommand(orderQuery, conn, tx);
                orderCmd.Parameters.AddWithValue("@OrderNumber", order.OrderId ?? "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                orderCmd.Parameters.AddWithValue("@CustomerName", order.Name);
                orderCmd.Parameters.AddWithValue("@Email", order.Email);
                orderCmd.Parameters.AddWithValue("@Phone", order.Phone);
                orderCmd.Parameters.AddWithValue("@Address", order.Address);
                orderCmd.Parameters.AddWithValue("@City", order.City);
                orderCmd.Parameters.AddWithValue("@Pincode", order.Pincode);
                orderCmd.Parameters.AddWithValue("@TotalAmount", order.Total);
                orderCmd.Parameters.AddWithValue("@PaymentMethod", order.PaymentMode);
                orderCmd.Parameters.AddWithValue("@PaymentStatus", paymentStatus);

                int orderId = Convert.ToInt32(orderCmd.ExecuteScalar());

                foreach (var item in order.Items)
                {
                    SqlCommand itemCmd = new SqlCommand(@"
                        INSERT INTO OrderItems (OrderId, ProductId, ProductName, Color, Size, HeelHeight, Quantity, Price)
                        VALUES (@OrderId, @ProductId, @ProductName, @Color, @Size, @HeelHeight, @Quantity, @Price)", conn, tx);
                    itemCmd.Parameters.AddWithValue("@OrderId", orderId);
                    itemCmd.Parameters.AddWithValue("@ProductId", item.Id);
                    itemCmd.Parameters.AddWithValue("@ProductName", item.Name);
                    itemCmd.Parameters.AddWithValue("@Color", item.Color);
                    itemCmd.Parameters.AddWithValue("@Size", item.Size);
                    itemCmd.Parameters.AddWithValue("@HeelHeight", item.HeelHeight);
                    itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    itemCmd.Parameters.AddWithValue("@Price", item.Price);
                    itemCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private string ComputeSha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}
