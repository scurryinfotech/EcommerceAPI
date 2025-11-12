using EcommerceAPI.Models;
using EcommerceService.Models;
using System.Threading.Tasks;

namespace EcommerceService.Repository.Interface
{
    public interface IPaymentRepository
    {
        Task<string> InitiatePayment(OrderRequest order, string baseCallbackUrl);
        bool SaveOrderAfterPayment(OrderRequest order, string paymentStatus);
    }
}
