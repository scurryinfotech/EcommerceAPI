namespace EcommerceAPI.Models
{

    public class ProductDetailPage
    {
        public int product_id { get; set; }
        public string name { get; set; }
        public decimal price { get; set; }
        public string description { get; set; }
        public string ShortDescription { get; set; }
        public string main_image { get; set; }
        public string SKU { get; set; }
        public decimal? Weight { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsTrending { get; set; }
        public bool IsNewArrival { get; set; }
        public int? BrandId { get; set; }
        public string BrandName { get; set; }
        public string BrandLogo { get; set; }

        public List<ProductImage> Images { get; set; } = new List<ProductImage>();
        public List<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public List<CategoryBasic> Categories { get; set; } = new List<CategoryBasic>();
    }


    public class CategoryBasic
    {
        public int category_id { get; set; }
        public string name { get; set; }
    }
}