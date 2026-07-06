namespace EcommerceAPI.Models
{
    public class CategoryDetail
    {
        public int category_id { get; set; }
        public string name { get; set; }
        public bool IsActive { get; set; }
        public int? ParentCategoryId { get; set; }
        public string ImagePath { get; set; }
        public string BannerImage { get; set; }
        public string Description { get; set; }
        public int DisplayOrder { get; set; }
        public int? ProductTypeId { get; set; }

      
        public List<CategoryDetail> Children { get; set; } = new List<CategoryDetail>();
    }
}