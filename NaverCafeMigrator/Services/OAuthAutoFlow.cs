using NaverCafeMigrator.Models;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace NaverCafeMigrator.Services
{
    public class OAuthAutoFlow
    {
        private readonly INaverAuthService _authService;
        private readonly string _redirectUri;
        private readonly int _port;
        private string _authorizationCode;
        private readonly ManualResetEvent _authCodeReceivedEvent = new(false); 
        private HttpListener _listener;

        public OAuthAutoFlow(INaverAuthService authService, string redirectUri)
        {
            _authService = authService;
            _redirectUri = redirectUri;

            // 리디렉션 URI에서 포트 추출
            var uri = new Uri(_redirectUri);
            _port = uri.Port > 0 ? uri.Port : 80;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            try
            {
                // 1. 임시 HTTP 서버 시작
                StartHttpServer();
                Console.WriteLine($"로컬 콜백 서버가 시작되었습니다...");
                
                // 2. 네이버 로그인 URL 생성
                var state = Guid.NewGuid().ToString("N");
                var authUrl = _authService.GenerateAuthUrl(state);

                // 3. 브라우저를 열어 사용자가 로그인하도록 함
                Console.WriteLine($"기본 브라우저에서 네이버 로그인 페이지를 엽니다...");
                OpenBrowser(authUrl);

                // 4. 콜백 URL로 리디렉션되어 인증 코드를 받을 때까지 대기
                Console.WriteLine($"네이버 로그인을 완료하고 승인해 주세요...");
                var receivedCode = _authCodeReceivedEvent.WaitOne(TimeSpan.FromMinutes(2));
                if (!receivedCode)
                {
                    throw new TimeoutException("인증 코드를 받는 시간이 초과되었습니다. 다시 시도해 주세요.");
                }

                // 5. 인증 코드로 액세스 토큰 요청
                Console.WriteLine("인증 코드 수신 완료. 액세스 토큰을 요청합니다...");
                TokenResponse tokenResponse = await _authService.GetAccessTokenAsync(_authorizationCode, state);
                if(!string.IsNullOrEmpty(tokenResponse.Error))
                {
                    throw new Exception($"토큰 발급 실패: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
                }

                Console.WriteLine("토큰 발급 성공!");

                return tokenResponse.AccessToken ?? "";
            }
            finally
            {
                // 임시 HTTP 서버 종료
                StopHttpServer();
            }
        }

        private void StopHttpServer()
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
        }

        private void OpenBrowser(string url)
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch(Exception)
            {
                Console.WriteLine($"브라우저를 자동으로 열 수 없습니다. URL을 복사하여 브라우저에 붙여넣어 주세요");
                Console.WriteLine($"{url}");
            }
        }

        private void StartHttpServer()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();

            // 비동기적으로 요청 수신 대기
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
        }

        private void ListenerCallback(IAsyncResult result)
        {
            if (result.AsyncState is not HttpListener listener)
            {
                Console.WriteLine("리스너가 null입니다.");
                return;
            }

            try
            {
                // 요청 컨텍스트 가져오기
                var context = listener.EndGetContext(result);
                var request = context.Request;
                var response = context.Response;

                // URL에서 인증 코드 추출
                var code = request.QueryString["code"];

                if(!string.IsNullOrEmpty(code))
                {
                    _authorizationCode = code;
                    _authCodeReceivedEvent.Set(); // 이벤트 신호 설정하여 대기 중인 스레드 해제

                    // 성공 응답 보내기
                    var responseString = "<html><body><h1>인증 완료</h1><p>브라우저를 닫아도 됩니다.</p></body></html>";
                    var buffer = Encoding.UTF8.GetBytes(responseString);

                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html; charset=UTF-8";
                    var output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }
                else
                {
                    // 오류 응답 보내기
                    var responseString = "<html><body><h1>인증 실패</h1><p>인증 코드가 없습니다.</p></body></html>";
                    var buffer = Encoding.UTF8.GetBytes(responseString);

                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html; charset=UTF-8";
                    var output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }

                // 계속해서 요청 수신 대기 (다른 요청이 있을 경우)
                listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"콜백 처리 중 오류 발생: {ex.Message}");
            }
        }
    }
}
