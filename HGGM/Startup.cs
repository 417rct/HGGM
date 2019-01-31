﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using AspNetCore.Identity.LiteDB;
using AspNetCore.Identity.LiteDB.Data;
using Hangfire;
using Hangfire.LiteDB;
using HGGM.Models.Configuration;
using HGGM.Models.Identity;
using HGGM.Services;
using HGGM.Services.Authorization;
using HGGM.Services.Authorization.Simple;
using HGGM.Services.Discourse;
using HGGM.Services.Authorization.Tag;
using Joonasw.AspNetCore.SecurityHeaders;
using LiteDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using LiteDbContext = HGGM.Services.LiteDbContext;

namespace HGGM
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            app.UseHealthChecks("/health");
            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All,
                RequireHeaderSymmetry = false,
                ForwardLimit = 10
            };
            foreach (var address in Configuration.GetSection("AllowedProxyIPs").Get<List<string>>()
                .Select(IPAddress.Parse)) forwardedHeadersOptions.KnownProxies.Add(address);
            foreach (var network in Configuration.GetSection("AllowedProxyNetworks").Get<List<string>>().Select(i =>
                new IPNetwork(IPAddress.Parse(i.Substring(0, i.LastIndexOf("/", StringComparison.Ordinal))),
                    int.Parse(i.Substring(i.LastIndexOf("/", StringComparison.Ordinal) + 1)))
            ))
                forwardedHeadersOptions.KnownNetworks.Add(network);
            app.UseForwardedHeaders(forwardedHeadersOptions);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseExceptionDemystifier();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                HstsBuilderExtensions.UseHsts(app);
            }

            app.UseHttpsRedirection();

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("en"),
                SupportedCultures = CultureInfo.GetCultures(CultureTypes.AllCultures),
                SupportedUICultures = new[] {new CultureInfo("en"), new CultureInfo("cs")}
            });

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseHangfireDashboard(options: new DashboardOptions
                {Authorization = new[] {new PermissionDashboardAuthorizationFilter()}});
            app.UseHangfireServer();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    "areaRoute",
                    "{area:exists}/{controller=Home}/{action=Index}/{id?}");

                routes.MapRoute(
                    "default",
                    "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseSwagger();
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"); });
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddSingleton(new LiteDatabase(Configuration.GetConnectionString("LiteDb")));
            services.AddSingleton<LiteRepository>();
            services.AddSingleton<ILiteDbContext, LiteDbContext>();

            services.AddAuthentication()
                .AddSteam();
            services.AddIdentity<User, Role>()
                .AddUserStore<LiteDbUserStore<User>>()
                .AddRoleStore<LiteDbRoleStore<Role>>()
                .AddDefaultTokenProviders();

            services.AddScoped<IAuthorizationHandler, SimplePermissionHandler>();
            services.AddSingleton<IAuthorizationPolicyProvider, SimplePermissionPolicyProvider>();

            services.Configure<MailConfig>(Configuration.GetSection("EmailSettings"));
            services.AddSingleton<IEmailSender, EmailSender>();

            services.AddTransient<INotificationService, NotificationService>();
            services.AddScoped<IAuthorizationHandler, TagHandler>();

            
            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services.AddHangfire(configuration => configuration.UseLiteDbStorage());

            services
                .AddMvc(options =>
                    options.AllowCombiningAuthorizeFilters = false) //https://github.com/aspnet/Mvc/pull/8068
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddMvcLocalization(LanguageViewLocationExpanderFormat.SubFolder)
                .AddViewLocalization(LanguageViewLocationExpanderFormat.SubFolder,
                    options => options.ResourcesPath = "Resources");

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Identity/Account/ExternalLogin";
                options.LogoutPath = "/Identity/Account/Logout";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            });

            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new Info {Title = "HGGM API", Version = "v1"}); });

            services.AddSingleton<MarkdownService>();
            services.AddSingleton<AuditService>();

            services.Configure<DiscourseService.Options>(Configuration.GetSection("Discourse"));
            services.AddSingleton<DiscourseService>();
            services.AddTransient<EventManager>();
            services.AddHealthChecks();
        }
    }
}
