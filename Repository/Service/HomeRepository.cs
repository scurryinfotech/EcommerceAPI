using EcommerceAPI.Models;
using EcommerceAPI.Repository.Interface;
using EcommerceService.Repository.Interface;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EcommerceService.Repository.Service
{
    public class HomeRepository : IHomeRepository
    {
        private readonly IConfiguration _configuration;
        private SqlConnection con;

        public HomeRepository(IConfiguration configuration)
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

        private const string BaseCardQuery = @"
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
            WHERE p.IsActive = 1";

        public List<ProductCard> GetFeaturedProducts(int top = 20)
        {
            return RunCardQuery(BaseCardQuery + " AND p.IsFeatured = 1 ORDER BY p.created_at DESC", top);
        }

        public List<ProductCard> GetTrendingProducts(int top = 20)
        {
            return RunCardQuery(BaseCardQuery + " AND p.IsTrending = 1 ORDER BY p.created_at DESC", top);
        }

        public List<ProductCard> GetNewArrivals(int top = 20)
        {
            return RunCardQuery(BaseCardQuery + " AND p.IsNewArrival = 1 ORDER BY p.created_at DESC", top);
        }

        public List<ProductCard> GetProductsByCategory(int categoryId, int top = 50)
        {
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
                INNER JOIN product_relations pr ON p.product_id = pr.product_id
                WHERE p.IsActive = 1 AND pr.IsActive = 1 AND pr.category_id = @CategoryId
                ORDER BY p.created_at DESC";

            List<ProductCard> products = new List<ProductCard>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                        products.Add(MapCard(reader));
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return products;
        }

        public List<ProductCard> GetProductsByBrand(int brandId, int top = 50)
        {
            return RunCardQuery(BaseCardQuery + " AND p.BrandId = @BrandId ORDER BY p.created_at DESC", top, ("@BrandId", brandId));
        }

        private List<ProductCard> RunCardQuery(string query, int top, params (string Name, object Value)[] extraParams)
        {
            List<ProductCard> products = new List<ProductCard>();
            try
            {
                connection();
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    foreach (var p in extraParams)
                        cmd.Parameters.AddWithValue(p.Name, p.Value);

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                        products.Add(MapCard(reader));
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
            return products;
        }

        private ProductCard MapCard(SqlDataReader reader)
        {
            return new ProductCard
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
            };
        }
    }
}