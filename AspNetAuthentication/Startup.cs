using AspNetAuthentication.GraphQL;
using HotChocolate;
using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace AspNetAuthentication
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            services.AddGraphQL(
                SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddAuthorizeDirectiveType());

            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                // NOTE: cookie authentication and OIDC is used to handle authentication in web browser
                .AddCookie(config =>
                {
                    // NOTE: This is used needed if you want to automatically trigger Microsoft OIDC below for authentication
                    config.ForwardChallenge = "Microsoft";
                    // Alternatively, you can have a sign in page to allow user to choose among different identity provider
                    //config.LoginPath = "/Login";
                })
                .AddOpenIdConnect("Microsoft", config =>
                {
                    config.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    Configuration.Bind("Authentication:Microsoft", config);

                    // NOTE: For multitenant, the issuer is of the form https://login.microsoftonline.com/{tenantid}/v2.0.
                    // Hence, we cannot preset ValidIssuers.
                    // In this case, a custom validation is needed.
                    config.TokenValidationParameters.IssuerValidator = new IssuerValidator((issuer, token, parameters) =>
                    {
                        // Accepts any issuer of the form "https://login.microsoftonline.com/{tenantid}/v2.0",
                        // where tenantid is the tid from the token.
                        if (token is JwtSecurityToken jwt)
                        {
                            if (jwt.Payload.TryGetValue("tid", out var value) &&
                                value is string tokenTenantId)
                            {
                                var issuers = (parameters.ValidIssuers ?? Enumerable.Empty<string>())
                                    .Append(parameters.ValidIssuer)
                                    .Where(i => !string.IsNullOrEmpty(i));

                                if (issuers.Any(i => i.Replace("{tenantid}", tokenTenantId) == issuer))
                                    return issuer;
                            }
                        }

                        // Recreate the exception that is thrown by default
                        // when issuer validation fails
                        var validIssuer = parameters.ValidIssuer ?? "null";
                        var validIssuers = parameters.ValidIssuers == null
                            ? "null"
                            : !parameters.ValidIssuers.Any()
                                ? "empty"
                                : string.Join(", ", parameters.ValidIssuers);
                        string errorMessage = FormattableString.Invariant(
                            $"IDX10205: Issuer validation failed. Issuer: '{issuer}'. Did not match: validationParameters.ValidIssuer: '{validIssuer}' or validationParameters.ValidIssuers: '{validIssuers}'.");

                        throw new SecurityTokenInvalidIssuerException(errorMessage)
                        {
                            InvalidIssuer = issuer
                        };
                    });
                });

            services.AddAuthorization();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseGraphQL("/GraphQL");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
