using NaverCafeMigrator.Models;

namespace NaverCafeMigrator.Services
{
    interface INaverAuthService
    {
        string GenerateAuthUrl(string state);
        Task<TokenResponse> GetAccessTokenAsync(string code, string state);
    }
}
