namespace EcommerceAPI.Models
{
    public class WebsiteSetting
    {
        public int WebsiteSettingId { get; set; }
        public string WebsiteName { get; set; }
        public string Logo { get; set; }
        public string Favicon { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Facebook { get; set; }
        public string Instagram { get; set; }
        public string Youtube { get; set; }
        public string WhatsApp { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
        public string FooterText { get; set; }
    }
}