using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    if(args.Length > 0)
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
    Console.WriteLine("NaverCafeMigrator.exe token <state> <code>");
    Console.WriteLine("NaverCafeMigrator.exe post <CSV파일경로> <AccessToken>");
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

static async Task StartNaverLogin()
{
    Console.WriteLine("\n네이버 로그인을 시작합니다.");

    // 네이버 로그인 URL 생성
    _currentState = Guid.NewGuid().ToString("N");
    var authUrl = _authService.GenerateAuthUrl(_currentState);

    Console.WriteLine($"아래 URL을 브라우저에서 열어 네이버 로그인을 완료하세요:");
    Console.WriteLine(authUrl);
    Console.WriteLine("\n로그인 후, 리다이렉트된 URL에서 코드 파라미터의 값을 복사해두세요.");
    Console.WriteLine("그 다음 메뉴에서 '2. 토큰 발급받기'를 선택하세요.");
}

static async Task GetAccessToken()
{
    Console.WriteLine("\n토큰 발급 과정을 시작합니다.");

    if (string.IsNullOrEmpty(_currentState))
    {
        Console.WriteLine("먼저 네이버 로그인을 진행해주세요 (메뉴 1번).");
        return;
    }

    Console.Write("네이버 로그인 후 받은 코드를 입력하세요: ");
    var code = Console.ReadLine();

    if (string.IsNullOrEmpty(code))
    {
        Console.WriteLine("코드가 입력되지 않았습니다.");
        return;
    }

    Console.WriteLine("\n토큰을 발급 받는 중입니다...");
    var tokenResponse = await _authService.GetAccessTokenAsync(code, _currentState);

    if (!string.IsNullOrEmpty(tokenResponse.Error))
    {
        Console.WriteLine($"토큰 발급 실패: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
        return;
    }

    _currentAccessToken = tokenResponse.AccessToken;

    Console.WriteLine("토큰 발급 성공!");
    Console.WriteLine($"Access Token: {tokenResponse.AccessToken.Substring(0, 10)}...");
    Console.WriteLine($"Refresh Token: {tokenResponse.RefreshToken.Substring(0, 10)}...");
    Console.WriteLine($"만료 시간: {tokenResponse.ExpiresIn}초");
    Console.WriteLine("\n이제 '3. CSV에서 게시물 업로드' 메뉴를 선택하여 게시물을 업로드할 수 있습니다.");
}

static async Task UploadPostsFromCsv()
{
    Console.WriteLine("\nCSV 파일에서 게시물 업로드를 시작합니다.");

    if (string.IsNullOrEmpty(_currentAccessToken))
    {
        Console.WriteLine("먼저 토큰을 발급받아야 합니다 (메뉴 1번과 2번을 차례로 진행해주세요).");
        return;
    }

    Console.Write("업로드할 CSV 파일의 경로를 입력하세요: ");
    var csvPath = Console.ReadLine();

    if (string.IsNullOrEmpty(csvPath))
    {
        Console.WriteLine("CSV 파일 경로가 입력되지 않았습니다.");
        return;
    }

    if (!File.Exists(csvPath))
    {
        Console.WriteLine($"CSV 파일을 찾을 수 없습니다: {csvPath}");
        return;
    }

    Console.WriteLine($"\n게시물 업로드 중...");
    var result = await _cafeService.PostArticlesFromCsvAsync(csvPath, _currentAccessToken);

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