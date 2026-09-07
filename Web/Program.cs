using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http;
using Web.Health;
using Web.Options;
using Web.Middleware;
using Web.Services;
using Web.ModelBinding;
using WebDataProtectionOptions = Web.Options.DataProtectionOptions;

var builder = WebApplication.CreateBuilder(args);
var secretsPath = builder.Configuration["SecretsPath"] ?? "/run/secrets";
builder.Configuration.AddKeyPerFile(secretsPath, optional: true, reloadOnChange: false);

if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

var reverseProxyOptions = builder.Configuration
    .GetSection(ReverseProxyOptions.SectionName)
    .Get<ReverseProxyOptions>()
    ?? new ReverseProxyOptions();
var dataProtectionOptions = builder.Configuration
    .GetSection(WebDataProtectionOptions.SectionName)
    .Get<WebDataProtectionOptions>()
    ?? new WebDataProtectionOptions();

if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    var allowedHosts = builder.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts)
        || allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(host => host == "*"))
    {
        throw new InvalidOperationException(
            "AllowedHosts должен содержать точные production-хосты, а не '*'.");
    }

    if (!reverseProxyOptions.Enabled)
    {
        throw new InvalidOperationException(
            "В production необходимо включить ReverseProxy:Enabled и настроить доверенные proxy/network.");
    }

    if (string.IsNullOrWhiteSpace(dataProtectionOptions.KeysPath))
    {
        throw new InvalidOperationException(
            "В production необходимо настроить общий DataProtection:KeysPath.");
    }
}

builder.Services
    .AddControllersWithViews(options =>
    {
        options.ModelBinderProviders.Insert(0, new FlexibleNumberModelBinderProvider());
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter, ClientContextHttpMessageHandlerBuilderFilter>();
builder.Services.AddTransient<TokenDelegatingHandler>();
builder.Services.AddTransient<ApiRetryDelegatingHandler>();
builder.Services
    .AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<ApiColdStartRecoveryOptions>()
    .Bind(builder.Configuration.GetSection(ApiColdStartRecoveryOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<PricingOptions>()
    .Bind(builder.Configuration.GetSection(PricingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ICurrencyFormatter, CurrencyFormatter>();
builder.Services.AddScoped<AnalyticsPdfExportService>();
builder.Services
    .AddOptions<ReverseProxyOptions>()
    .Bind(builder.Configuration.GetSection(ReverseProxyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<WebDataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(WebDataProtectionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ReverseProxyConfiguration.Configure(options, reverseProxyOptions));
builder.Services.AddHealthChecks()
    .AddCheck<ApiReachabilityHealthCheck>("api", tags: ["ready"]);

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionOptions.ApplicationName);
if (!string.IsNullOrWhiteSpace(dataProtectionOptions.KeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionOptions.KeysPath));
}

var apiOptions = builder.Configuration
    .GetSection(ApiOptions.SectionName)
    .Get<ApiOptions>()
    ?? new ApiOptions();
var coldStartRecoveryOptions = builder.Configuration
    .GetSection(ApiColdStartRecoveryOptions.SectionName)
    .Get<ApiColdStartRecoveryOptions>()
    ?? new ApiColdStartRecoveryOptions();

Action<HttpClient> configureApiClient = client =>
{
    client.BaseAddress = new Uri(apiOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
};

builder.Services.AddHostedService<ApiWakeupHostedService>();
builder.Services.AddHttpClient("ApiWakeup", client =>
{
    client.BaseAddress = new Uri(apiOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(coldStartRecoveryOptions.WakeupTimeoutSeconds);
});

builder.Services.AddHttpClient<AuthApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient("AccountStatusApi", client =>
{
    configureApiClient(client);
}).AddHttpMessageHandler<ApiRetryDelegatingHandler>();

builder.Services.AddHttpClient<PropertiesApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<RealEstateApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<MatchingApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<RealtorsApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<ClientDashboardApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<RealtorDashboardApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<AdminDashboardApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<DealRequestsApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<RealtorEfficiencyApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<ComplaintsApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<DealChatApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<PropertyPhotoApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddHttpClient<ProfileApiClient>(client =>
{
    configureApiClient(client);
})
.AddHttpMessageHandler<ApiRetryDelegatingHandler>()
.AddHttpMessageHandler<TokenDelegatingHandler>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.LogoutPath = "/Auth/Logout";
        options.Cookie.Name = "Algeda.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                var path = context.Request.Path;
                if (path.StartsWithSegments("/Matching", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/Notifications", StringComparison.OrdinalIgnoreCase))
                {
                    var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
                    var loginUrl =
                        $"/Auth/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&registrationHint=true";

                    context.Response.Redirect(loginUrl);
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin", "SuperAdmin"));
    options.AddPolicy("RequireRealtorOrAdmin", policy => policy.RequireRole("Realtor", "Admin", "SuperAdmin"));
    options.AddPolicy("RequireClientOrAdmin", policy => policy.RequireRole("Client", "Admin", "SuperAdmin"));
    options.AddPolicy("RequireAnyAuthorized", policy => policy.RequireAuthenticatedUser());
});

var app = builder.Build();

if (reverseProxyOptions.Enabled)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!reverseProxyOptions.Enabled)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseAuthentication();
app.UseMiddleware<FrozenAccountMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
})
    .AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
})
    .AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
