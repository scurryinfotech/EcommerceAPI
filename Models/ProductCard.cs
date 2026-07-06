namespace EcommerceAPI.Models
{

    public class ProductCard
    {
        public int product_id { get; set; }
        public string name { get; set; }
        public decimal price { get; set; }
        public string main_image { get; set; }
        public string ShortDescription { get; set; }
        public string SKU { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsTrending { get; set; }
        public bool IsNewArrival { get; set; }
        public string BrandName { get; set; }
        public int? BrandId { get; set; }
        public bool InStock { get; set; } 
    }
}