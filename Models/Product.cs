namespace EcommerceAPI.Models
{
    public class Product
    {
        public int product_id { get; set; }
        public string name { get; set; }
        public decimal price { get; set; }
        public string description { get; set; }
        public string main_image { get; set; }
        public DateTime created_at { get; set; }
        public bool IsActive { get; set; }
        public List<ProductVariant> variants { get; set; } = new List<ProductVariant>();


    }

}

