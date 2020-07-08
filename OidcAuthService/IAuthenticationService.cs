
using System.Threading.Tasks;

namespace Hypertherm.OidcAuth
{
    public interface IAuthenticationService
    {
        Task<string> Login(string user = "default-user");
        void Logout(string user = "default-user");
    }
}