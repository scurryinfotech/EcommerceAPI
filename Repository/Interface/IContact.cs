
using EcommerceAPI.Models;

namespace EcommerceAPI.Repositories
{
    public interface IContactRepository
    {
        Task<bool> SaveContactAsync(ContactModel contact);
        //Task<IEnumerable<ContactModel>> GetAllContactsAsync();
        //Task<ContactModel?> GetContactByIdAsync(int id);
    }
}