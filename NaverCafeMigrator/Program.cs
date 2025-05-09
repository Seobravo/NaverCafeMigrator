using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NaverCafeMigrator.Config;
using NaverCafeMigrator.Services;

Console.WriteLine("네이버 카페 마이그레이터");
Console.WriteLine("======================");

try
{
    // 호스트 빌드
    var host = CreateHostBuilder(args).Build();

    // 서비스 가져오기
    var cafeService = host.Services.GetRequiredService<INaverCafeApiService>();
    var authService = host.Services.GetRequiredService<INaverAuthService>();
    var settings = host.Services.GetRequiredService<IOptions<NaverCafeApiSettings>>().Value;

    if (args.Length > 0)
    {
        var command = args[0].ToLower();

        switch(command)
        {
            case "auth":
                // 네이버 로그인 URL 생성
                var state = Guid.NewGuid().ToString("N");
                var authUrl = authService.GenerateAuthUrl(state);
                Console.WriteLine($"아래 URL을 브라우저에서 열어 네이버 로그인을 완료하세요:");
                Console.WriteLine(authUrl);
                Console.WriteLine("로그인 후, 리다이렉트된 URL에서 코드 파라미터를 복사하여 다음 명령을 실행하세요:");
                Console.WriteLine($"NaverCafeMigrator.exe token <코드> {state}");
                break;
            case "token":
                if(args.Length < 3)
                {
                    Console.WriteLine("사용법: NaverCafeMigrator.exe token <state> <code>");
                    break;
                }

                // 토큰 발급
                var code = args[1];
                var stateValue = args[2];
                var tokenResponse = await authService.GetAccessTokenAsync(code, stateValue);

                if (!string.IsNullOrEmpty(tokenResponse.Error))
                {
                    Console.WriteLine($"토큰 발급 실패: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
                    break;
                }

                Console.WriteLine("토큰 발급 성공!");
                Console.WriteLine($"Access Token: {tokenResponse.AccessToken.Substring(0, 10)}...");
                Console.WriteLine($"Refresh Token: {tokenResponse.RefreshToken.Substring(0, 10)}...");
                Console.WriteLine($"만료 시간: {tokenResponse.ExpiresIn}초");
                Console.WriteLine($"\n이 토큰으로 게시물을 업로드하려면 다음 명령을 실행하세요:");
                Console.WriteLine($"NaverCafeMigrator.exe post <CSV파일경로> {tokenResponse.AccessToken}");
                break;
            case "post":
                if(args.Length < 3)
                {
                    Console.WriteLine("사용법: NaverCafeMigrator.exe post <CSV파일경로> <AccessToken>");
                    break;
                }

                var csvPath = args[1];
                var accessToken = args[2];

                if (!File.Exists(csvPath))
                {
                    Console.WriteLine($"CSV 파일을 찾을 수 없습니다: {csvPath}");
                    break;
                }

                Console.WriteLine($"게시물 업로드 중...");
                var result = await cafeService.PostArticlesFromCsvAsync(csvPath, accessToken);

                // 결과 출력
                Console.WriteLine($"업로드 완료!");
                Console.WriteLine($"총 게시물: {result.TotalPosts}");
                Console.WriteLine($"성공: {result.SuccessfulPosts}");
                Console.WriteLine($"실패: {result.FailedPosts}");

                if(!string.IsNullOrEmpty(result.Message))
                {
                    Console.WriteLine($"메시지: {result.Message}");
                }
                break;
            case "autopost":
                // 자동 인증 및 게시물 업로드
                if(args.Length < 2)
                {
                    Console.WriteLine("사용법: NaverCafeMigrator.exe autopost <CSV파일경로>");
                    break;
                }

                csvPath = args[1];

                if (!File.Exists(csvPath))
                {
                    Console.WriteLine($"CSV 파일을 찾을 수 없습니다: {csvPath}");
                    break;
                }

                Console.WriteLine($"자동 인증 및 게시물 업로드를 시작합니다...");

                // 자동 인증 흐름 실행
                var oauthFlow = new OAuthAutoFlow(authService, settings.RedirectUri);

                try
                {
                    // 인증 처리 및 토큰 발급
                    accessToken = await oauthFlow.GetAccessTokenAsync();

                    // 발급된 토큰으로 게시물 업로드
                    Console.WriteLine("인증 완료! 게시물 업로드를 시작합니다...");
                    result = await cafeService.PostArticlesFromCsvAsync(csvPath, accessToken);

                    // 결과 출력
                    Console.WriteLine($"업로드 완료!");
                    Console.WriteLine($"총 게시물: {result.TotalPosts}");
                    Console.WriteLine($"성공: {result.SuccessfulPosts}");
                    Console.WriteLine($"실패: {result.FailedPosts}");

                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        Console.WriteLine($"메시지: {result.Message}");
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"자동 인증 중 오류 발생: {ex.Message}");
                }
                break;
            default:
                ShowHelp();
                break;
        }
    }
    else
    {
        ShowHelp();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"프로그램 실행 중 오류 발생: {ex.Message}");
}

// 프로그램 종료 대기
Console.WriteLine("프로그램 종료를 원하시면 아무 키나 누르세요.");
Console.ReadKey(true);

static void ShowHelp()
{
    Console.WriteLine("사용법:");
    Console.WriteLine("NaverCafeMigrator.exe auth");
    Console.WriteLine("NaverCafeMigrator.exe token <code> <state>");
    Console.WriteLine("NaverCafeMigrator.exe post <CSV파일경로> <AccessToken>");
    Console.WriteLine("NaverCafeMigrator.exe autopost <CSV파일경로>");
    Console.WriteLine("  - autopost: 인증과 게시물 업로드를 한 번에 자동으로 처리합니다.");
    Console.WriteLine("명령어를 입력하지 않으면 도움말이 표시됩니다.");
}

static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            config.AddEnvironmentVariables();
            config.AddCommandLine(args);
        })
        .ConfigureServices((context, services) =>
        {
            // 설정 등록
            services.Configure<NaverCafeApiSettings>(
                context.Configuration.GetSection("NaverCafeApiSettings"));
            // 서비스 등록
            services.AddHttpClient();
            services.AddSingleton<INaverAuthService, NaverAuthService>();
            services.AddSingleton<INaverCafeApiService, NaverCafeApiService>();
        });
}