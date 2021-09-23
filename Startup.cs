// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using WebApp_OpenIDConnect_DotNet.Services.Arm;
using WebApp_OpenIDConnect_DotNet.Services.GraphOperations;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using WebApp_OpenIDConnect_DotNet.Infrastructure;
using System.Threading.Tasks;

namespace WebApp_OpenIDConnect_DotNet
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(Configuration)
                .EnableTokenAcquisitionToCallDownstreamApi()
                .AddInMemoryTokenCaches();

            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
            options =>
            {
                var redirectToIdpHandler = options.Events.OnRedirectToIdentityProvider;
                options.Events.OnRedirectToIdentityProvider = async context =>
                {
                    // Call what Microsoft.Identity.Web is doing
                    await redirectToIdpHandler(context);

                    // Override the redirect URI to be what you want https://localhost:44321/signin-oidc
                    if (Configuration["AzureAd:WebAppURI"] != null)
                    {
                        context.ProtocolMessage.RedirectUri = Configuration["AzureAd:WebAppURI"] + Configuration["AzureAd:CallbackPath"];
                        System.Console.WriteLine("RedirectURL: " + Configuration["AzureAd:WebAppURI"] + Configuration["AzureAd:CallbackPath"]);
                    }
                };

                var redirectToIdpForSignOutHandler = options.Events.OnRedirectToIdentityProviderForSignOut;
                options.Events.OnRedirectToIdentityProviderForSignOut = async context =>
                {
                    // Call what Microsoft.Identity.Web is doing
                    await redirectToIdpForSignOutHandler(context);

                    // Override the redirect URI to be what you want
                    if (Configuration["AzureAd:WebAppURI"] != null)
                    {
                        context.ProtocolMessage.PostLogoutRedirectUri = Configuration["AzureAd:WebAppURI"] + Configuration["AzureAd:SignedOutCallbackPath"];
                        System.Console.WriteLine("PostLogoutRedirectURL: " + Configuration["AzureAd:WebAppURI"] + Configuration["AzureAd:SignedOutCallbackPath"]);
                    }
                };
            });

            // Add APIs
            services.AddGraphService(Configuration);
            services.AddHttpClient<IArmOperations, ArmApiOperationService>();
            services.AddHttpClient<IArmOperationsWithImplicitAuth, ArmApiOperationServiceWithImplicitAuth>()
                .AddMicrosoftIdentityUserAuthenticationHandler(
                    "arm", 
                    options => options.Scopes = $"{ArmApiOperationService.ArmResource}user_impersonation");

            services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            }).AddMicrosoftIdentityUI();
            services.AddRazorPages();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
