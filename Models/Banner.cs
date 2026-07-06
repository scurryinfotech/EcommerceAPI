namespace EcommerceAPI.Models
{
    public class Banner
    {
        public int BannerId { get; set; }
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string Description { get; set; }
        public string DesktopImage { get; set; }
        public string MobileImage { get; set; }
        public string ButtonText { get; set; }
        public string ButtonUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }
}