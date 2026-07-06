using EcommerceAPI.Models;

namespace EcommerceService.Repository.Interface
{
    public interface IWebsiteSettingsRepository
    {
        WebsiteSetting GetSettings();
        bool UpdateSettings(WebsiteSetting settings);
        string UpdateLogo(string logoPath);
        string UpdateFavicon(string faviconPath);
    }
}