using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Quartz;
using Serilog;
using Wash2Door.Logging;
using static OpenIddict.Abstractions.OpenIddictConstants;


ServiceLogging.InitializeLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSerilog();
builder.WebHost.ConfigureKestrel(options =>
{
    //options.Listen(IPAddress.Any, 443, listenOptions =>
    //{
    //    listenOptions.Protocols = HttpProtocols.Http1;
    //    listenOptions.UseHttps();
    //});
});

// OpenIddict offers native integration with Quartz.NET to perform scheduled tasks
// (like pruning orphaned authorizations/tokens from the database) at regular intervals.
builder.Services.AddQuartz(options =>
{
    options.UseMicrosoftDependencyInjectionJobFactory();
    options.UseSimpleTypeLoader();
    options.UseInMemoryStore();
});

// Register the Quartz.NET service and configure it to block shutdown until jobs are complete.
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

builder.Services.AddDbContext<DbContext>(options =>
{
    // Configure the context to use Microsoft SQL Server.
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

    // Register the entity sets needed by OpenIddict.
    // Note: use the generic overload if you need
    // to replace the default OpenIddict entities.
    options.UseOpenIddict();
});

builder.Services.AddOpenIddict()

    // Register the OpenIddict core components.
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<DbContext>();
    })

    // Register the OpenIddict server components.
    .AddServer(options =>
    {
        // Enable the authorization, introspection and token endpoints.
        options.SetAuthorizationEndpointUris("/authorize")
               .SetIntrospectionEndpointUris("/introspect")
               .SetTokenEndpointUris("/token");

        // Note: this sample only uses the authorization code flow but you can enable
        // the other flows if you need to support implicit, password or client credentials.
        options.AllowPasswordFlow();
        options.RegisterScopes(
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.OfflineAccess,
            OpenIddictConstants.Scopes.Roles,
            OpenIddictConstants.Scopes.Address,
            OpenIddictConstants.Scopes.Phone);
        // Register the encryption credentials. This sample uses a symmetric
        // encryption key that is shared between the server and the Api2 sample
        // (that performs local token validation instead of using introspection).
        //
        // Note: in a real world application, this encryption key should be
        // stored in a safe place (e.g in Azure KeyVault, stored as a secret).
        options.AddEncryptionKey(new SymmetricSecurityKey(
            Convert.FromBase64String("DRjd/GnduI3Efzen9V9BvbNUfc/VKgXltV7Kbk9sMkY=")));

        // Register the signing credentials.
        options.AddDevelopmentSigningCertificate();

        // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
        //
        // Note: unlike other samples, this sample doesn't use token endpoint pass-through
        // to handle token requests in a custom MVC action. As such, the token requests
        // will be automatically handled by OpenIddict, that will reuse the identity
        // resolved from the authorization code to produce access and identity tokens.
        //
        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough();
    })

    // Register the OpenIddict validation components.
    .AddValidation(options =>
    {
        // Import the configuration from the local OpenIddict server instance.
        options.UseLocalServer();

        // Register the ASP.NET Core host.
        options.UseAspNetCore();
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(pol =>
    {
        pol.SetIsOriginAllowed(origin => true);
        pol.AllowAnyHeader();
        pol.AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

// Create new application registrations matching the values configured in Zirku.Client and Zirku.Api1.
// Note: in a real world application, this step should be part of a setup script.
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DbContext>();
    await context.Database.EnsureCreatedAsync();

    await CreateApplicationsAsync();
    await CreateScopesAsync();

    async Task CreateApplicationsAsync()
    {
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync("client") is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "client",
                ClientSecret = "secret",
                ConsentType = ConsentTypes.Implicit,
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Introspection,
                    Permissions.GrantTypes.Password,
                    Permissions.ResponseTypes.IdTokenToken,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    Permissions.Prefixes.Scope + "custom"
                }
            });
        }
    }

    async Task CreateScopesAsync()
    {
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        if (await manager.FindByNameAsync("custom") is null)
        {
            await manager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "custom",
                Resources =
                {
                    "client"
                }
            });
        }
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api", [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
(ClaimsPrincipal user) => user.Identity!.Name);

app.MapPost("/token", async (HttpContext context, IOpenIddictScopeManager manager) =>
{
    // Retrieve the OpenIddict server request from the HTTP context.
    var request = context.GetOpenIddictServerRequest();

    var identifier = (string?)request.Password;
    if (identifier is not "1111")
    {
        return Results.Challenge(
            authenticationSchemes: new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            properties: new AuthenticationProperties(new Dictionary<string, string>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidRequest,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The specified hardcoded identity is invalid."
            }));
    }

    // Create a new identity and populate it based on the specified hardcoded identity identifier.
    var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);
    identity.AddClaim(new Claim(Claims.Subject, identifier!.ToString(CultureInfo.InvariantCulture)));
    identity.AddClaim(new Claim(Claims.Name, "BOB").SetDestinations(Destinations.AccessToken));

    // Note: in this sample, the client is granted all the requested scopes for the first identity (Alice)
    // but for the second one (Bob), only the "api1" scope can be granted, which will cause requests sent
    // to Zirku.Api2 on behalf of Bob to be automatically rejected by the OpenIddict validation handler,
    // as the access token representing Bob won't contain the "resource_server_2" audience required by Api2.
    var principal = new ClaimsPrincipal(identity);

    principal.SetScopes(request.GetScopes());
    var resources = await manager.ListResourcesAsync(principal.GetScopes()).ToListAsync();
    principal.SetResources(resources);

    return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.Run();