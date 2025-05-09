using NaverCafeMigrator.Models;

namespace NaverCafeMigrator.Services
{
    public interface INaverAuthService
    {
        string GenerateAuthUrl(string state);
        Task<TokenResponse> GetAccessTokenAsync(string code, string state);
    }
}
