namespace EcommerceAPI.Models
{
    public class ProductVariant
    {

        public int variant_id { get; set; }
        public int product_id { get; set; }
        public string color_name { get; set; }
        public string color_hex { get; set; }
        public int stock { get; set; }
        public int size { get; set; }
        public bool IsActive { get; set; }
        public int heel_height { get; set; }

        public List<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }
}
