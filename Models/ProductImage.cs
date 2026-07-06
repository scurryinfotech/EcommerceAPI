namespace EcommerceAPI.Models
{
    public class ProductImage
    {
        public int image_id { get; set; }
        public int product_id { get; set; }
        public string image_path { get; set; }
        public int display_order { get; set; }
        public bool is_main { get; set; }
        public bool IsActive { get; set; }
    }
}
