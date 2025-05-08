using Microsoft.Extensions.Options;
using NaverCafeMigrator.Config;
using NaverCafeMigrator.Models;
using System.Text.Json;
using System.Web;

namespace NaverCafeMigrator.Services
{
    public class NaverAuthService(
            IOptions<NaverCafeApiSettings> settings,
            IHttpClientFactory httpClientFactory
            ) : INaverAuthService
    {
        private readonly NaverCafeApiSettings _settings = settings.Value;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public string GenerateAuthUrl(string state)
        {
            var apiURL = $"https://nid.naver.com/oauth2.0/authorize?response_type=code";
            apiURL += $"&client_id={HttpUtility.UrlEncode(_settings.ClientId)}";
            apiURL += $"&redirect_uri={HttpUtility.UrlEncode(_settings.RedirectUri)}";
            apiURL += $"&state={HttpUtility.UrlEncode(state)}";

            return apiURL;
        }

        public async Task<TokenResponse> GetAccessTokenAsync(string code, string state)
        {
            using var client = _httpClientFactory.CreateClient();

            // API URL 구성
            var apiURL = $"https://nid.naver.com/oauth2.0/token?grant_type=authorization_code";
            apiURL += $"&client_id={HttpUtility.UrlEncode(_settings.ClientId)}";
            apiURL += $"&client_secret={HttpUtility.UrlEncode(_settings.ClientSecret)}";
            apiURL += $"&code={HttpUtility.UrlEncode(code)}";
            apiURL += $"&state={HttpUtility.UrlEncode(state)}";

            // 요청 헤더 설정
            client.DefaultRequestHeaders.Add("X-Naver-Client-Id", _settings.ClientId);
            client.DefaultRequestHeaders.Add("X-Naver-Client-Secret", _settings.ClientSecret);

            // 토큰 요청
            var response = await client.GetAsync(apiURL);
            var responseContent = await response.Content.ReadAsStringAsync();

            // 응답 처리
            TokenResponse? tokenResponse;
            try
            {
                tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
            }
            catch (JsonException)
            {
                tokenResponse = new TokenResponse
                {
                    Error = "Invalid JSON response",
                    ErrorDescription = responseContent
                };
            }

            return tokenResponse ?? new TokenResponse
            {
                Error = "Null response",
                ErrorDescription = "The deserialized response was null."
            };
        }
    }
}
