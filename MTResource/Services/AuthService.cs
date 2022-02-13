using Grpc.Core;
using OpenIddict.Abstractions;
using System.Net.Http.Headers;

namespace PMClient.Services
{
    public class AuthService : Auth.AuthBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuthService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            var auth = new HttpRequestMessage(HttpMethod.Post, $"https://localhost/token");

            auth.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "client",
                ["client_secret"] = "secret",
                ["username"] = "user",
                ["password"] = "1111",
                ["scope"] = "openid profile email custom roles",
            });

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(auth, HttpCompletionOption.ResponseContentRead);

            var payload = await response.Content.ReadFromJsonAsync<OpenIddictResponse>();

            if (string.IsNullOrEmpty(payload.Error))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);

                var res = await client.GetAsync($"https://localhost/api", HttpCompletionOption.ResponseContentRead);
                var id = await res.Content.ReadAsStringAsync();

                var grpc = new Greeter.GreeterClient(Grpc.Net.Client.GrpcChannel.ForAddress("https://localhost:5000", new Grpc.Net.Client.GrpcChannelOptions
                {
                    Credentials = ChannelCredentials.Create(ChannelCredentials.SecureSsl, CallCredentials.FromInterceptor((c, m) =>
                    {
                        m.Add("Authorization", $"Bearer {payload.AccessToken}");

                        return Task.CompletedTask;
                    }))
                }));

                await grpc.SayHelloAsync(new HelloRequest
                {
                    Name = id,
                });
            }

            return new LoginResponse();
        }
    }
}
