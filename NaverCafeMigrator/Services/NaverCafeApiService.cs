using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NaverCafeMigrator.Config;
using NaverCafeMigrator.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Web;

namespace NaverCafeMigrator.Services
{
    public class NaverCafeApiService(
        IOptions<NaverCafeApiSettings> settings,
        IHttpClientFactory httpClientFactory
        ) : INaverCafeApiService
    {
        private readonly NaverCafeApiSettings _settings = settings.Value;
        private HttpClient _httpClient = httpClientFactory.CreateClient();
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<PostResult> PostArticlesFromCsvAsync(string csvPath, string accessToken)
        {
            var result = new PostResult();

            try
            {
                // 인코딩 설정
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // 요청 헤더 설정
                _httpClient.DefaultRequestHeaders.Add("X-Naver-Client-Id", _settings.ClientId);
                _httpClient.DefaultRequestHeaders.Add("X-Naver-Client-Secret", _settings.ClientSecret);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                // CSV 설정
                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    TrimOptions = TrimOptions.Trim,
                    MissingFieldFound = null,
                    Encoding = Encoding.GetEncoding("EUC-KR"),
                };

                // CSV 파일 읽기
                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, csvConfig);
                var posts = csv.GetRecords<CafePost>().ToList();
                result.TotalPosts = posts.Count;
                posts.Reverse();

                // 게시물 업로드
                var retryCount = 1;
                for (int i = 0; i < posts.Count; i++)
                {
                    var success = await PostArticleAsync(posts[i].Title, posts[i].Content, posts[i].CreatedAt, posts[i].Author, accessToken);

                    if(!success)
                    {
                        Console.WriteLine($"{posts[i].Title} 올리기 실패 재시도 ({retryCount++}회)");
                        if(retryCount > 10)
                        {
                            result.Items.Add(new PostItem
                            {
                                Title = posts[i].Title,
                                IsSuccess = false,
                                Message = "재시도 횟수 초과"
                            });
                            result.FailedPosts++;

                            // API 호출 간 딜레이
                            await Task.Delay(3000);
                            continue;
                        }
                        i--;

                        // API 호출 간 딜레이
                        await Task.Delay(3000);
                        continue;
                    }

                    result.Items.Add(new PostItem
                    {
                        Title = posts[i].Title,
                        IsSuccess = true,
                        Message = $"게시물 업로드 성공 ({posts[i].Title})"
                    });
                    result.SuccessfulPosts++;
                    retryCount = 1; // 성공 시 재시도 횟수 초기화
                    Console.WriteLine($"게시물 업로드 성공 ({posts[i].Title})");

                    // API 호출 간 딜레이
                    await Task.Delay(3000);
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"오류 발생: {ex.Message}";
                return result;
            }
        }

        private async Task<bool> PostArticleAsync(string subject, string content, string createAt, string author, string accessToken)
        {
            try
            {
                // API 요청 URL 구성
                var apiUrl = $"{_settings.BaseUrl}/{_settings.CafeClubId}/menu/{_settings.CafeMenuId}/articles";

                // 요청 본문 생성 (네이버 예시 코드와 유사하게 처리)
                var encodedPostBody = EncodeData(subject, content, createAt, author);

                // POST 요청 설정
                using var bytesContent = new ByteArrayContent(encodedPostBody);
                bytesContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                // POST 요청 보내기
                using var response = await _httpClient.PostAsync(apiUrl, bytesContent);

                // 응답 확인
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private byte[] EncodeData(string subject, string content, string createAt, string author)
        {
            subject += $"[{createAt}][{author}]";
            var byteSubject = HttpUtility.UrlEncode(subject);
            byteSubject = HttpUtility.UrlEncode(byteSubject, Encoding.GetEncoding("EUC-KR"));

            var byteContent = HttpUtility.UrlEncode(content);
            byteContent = HttpUtility.UrlEncode(byteContent, Encoding.GetEncoding("EUC-KR"));

            return Encoding.UTF8.GetBytes($"subject={byteSubject}&content={byteContent}&openyn=true");
        }
    }
}
