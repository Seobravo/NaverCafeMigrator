using NaverCafeMigrator.Models;

namespace NaverCafeMigrator.Services
{
    interface INaverCafeApiService
    {
        Task<PostResult> PostArticlesFromCsvAsync(string csvPath, string accessToken);
    }
}
