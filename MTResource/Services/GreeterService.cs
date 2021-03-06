using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;
using PMClient;
using System.Diagnostics;

namespace PMClient.Services
{
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            foreach(var item in context.RequestHeaders)
            {
                Debug.WriteLine($"{item.Key} {item.Value}");  
            }

            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }
    }
}