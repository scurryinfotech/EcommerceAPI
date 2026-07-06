namespace EcommerceAPI.Models
{
    public class Brand
    {
        public int BrandId { get; set; }
        public string BrandName { get; set; }
        public string Logo { get; set; }
        public string Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }
}