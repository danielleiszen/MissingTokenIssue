using System;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation.AspNetCore;
using PMClient.Services;
using Serilog;
using Wash2Door.Logging;

ServiceLogging.InitializeLogger();
var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSerilog();
builder.WebHost.ConfigureKestrel(options =>
{
    //options.Listen(IPAddress.Any, 5000, listenOptions =>
    //{
    //    listenOptions.Protocols = HttpProtocols.Http2;
    //    listenOptions.UseHttps();
    //});
});

builder.Services.AddHttpClient();
// Register the OpenIddict validation components.
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        // Note: the validation handler uses OpenID Connect discovery
        // to retrieve the issuer signing keys used to validate tokens.
        options.SetIssuer("https://localhost/");
        options.AddAudiences("client");

        //options.UseIntrospection()
        //    .SetClientId(builder.Configuration["CLIENT_ID"])
        //    .SetClientSecret(builder.Configuration["CLIENT_SECRET"]);
        // Register the encryption credentials. This sample uses a symmetric
        // encryption key that is shared between the server and the Api2 sample
        // (that performs local token validation instead of using introspection).
        //
        // Note: in a real world application, this encryption key should be
        // stored in a safe place (e.g in Azure KeyVault, stored as a secret).
        options.AddEncryptionKey(new SymmetricSecurityKey(
            Convert.FromBase64String("DRjd/GnduI3Efzen9V9BvbNUfc/VKgXltV7Kbk9sMkY=")));

        // Register the System.Net.Http integration.
        options.UseSystemNetHttp();

        // Register the ASP.NET Core host.
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(pol =>
    {
        pol.SetIsOriginAllowed(origin => true);
        pol.AllowAnyHeader();
        pol.AllowAnyMethod();
        pol.WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
    });
});
builder.Services.AddGrpc();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseGrpcWeb();
app.UseCors();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>()
    .RequireAuthorization()
    .EnableGrpcWeb();

app.MapGrpcService<AuthService>()
    .EnableGrpcWeb();

app.Run();