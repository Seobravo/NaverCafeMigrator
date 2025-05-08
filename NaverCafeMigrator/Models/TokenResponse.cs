using System.Text.Json.Serialization;

namespace NaverCafeMigrator.Models
{
    public class TokenResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; } = string.Empty;
        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; } = string.Empty;
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")]
        public string? ExpiresIn { get; set; } = string.Empty;
    }
}
