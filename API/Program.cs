using Application.Interfaces;
using Application.Options;
using API.Auth;
using Application.Services;
using API.Background;
using API.Configuration;
using API.Exceptions;
using API.Health;
using API.Hubs;
using API.Middleware;
using API.Validation;
using FluentValidation;
using Infrastructure;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

const string MigrateCommand = "--migrate";
const string BootstrapAdminCommand = "--bootstrap-admin";

var runMigrations = args.Contains(MigrateCommand, StringComparer.OrdinalIgnoreCase);
var bootstrapAdmin = args.Contains(BootstrapAdminCommand, StringComparer.OrdinalIgnoreCase);
if (runMigrations && bootstrapAdmin)
{
    throw new InvalidOperationException(
        $"Команды {MigrateCommand} и {BootstrapAdminCommand} необходимо запускать отдельно.");
}

var isOperationalCommand = runMigrations || bootstrapAdmin;
var hostArguments = args
    .Where(argument => !argument.Equals(MigrateCommand, StringComparison.OrdinalIgnoreCase)
        && !argument.Equals(BootstrapAdminCommand, StringComparison.OrdinalIgnoreCase))
    .ToArray();

var builder = WebApplication.CreateBuilder(hostArguments);
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

var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var hasConfiguredConnectionString = !string.IsNullOrWhiteSpace(configuredConnectionString);
if (!hasConfiguredConnectionString
    && (isOperationalCommand
        || (!builder.Environment.IsDevelopment()
            && !builder.Environment.IsEnvironment("Testing"))))
{
    throw new InvalidOperationException(
        "Строка подключения не настроена. Укажите ConnectionStrings__DefaultConnection.");
}

if (!hasConfiguredConnectionString)
{
    configuredConnectionString =
        "Host=127.0.0.1;Port=1;Database=algeda_unconfigured;Username=algeda;Password=not-used;Timeout=1;Command Timeout=1";
    builder.Configuration["ConnectionStrings:DefaultConnection"] = configuredConnectionString;
}

var configuredJwtSigningKey = builder.Configuration[$"{JwtOptions.SectionName}:SigningKey"];
if (string.IsNullOrWhiteSpace(configuredJwtSigningKey))
{
    if (!isOperationalCommand
        && !builder.Environment.IsDevelopment()
        && !builder.Environment.IsEnvironment("Testing"))
    {
        throw new InvalidOperationException(
            "Ключ подписи JWT не настроен. Укажите Jwt__SigningKey длиной не менее 32 символов.");
    }

    builder.Configuration[$"{JwtOptions.SectionName}:SigningKey"] =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}

ProductionConfigurationValidator.Validate(
    builder.Configuration,
    builder.Environment,
    isOperationalCommand);

var russianCulture = new CultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentCulture = russianCulture;
CultureInfo.DefaultThreadCurrentUICulture = russianCulture;
ValidatorOptions.Global.LanguageManager.Culture = russianCulture;

builder.Services.AddScoped<FluentValidationActionFilter>();

builder.Services.AddControllers(options =>
    {
        options.Filters.AddService<FluentValidationActionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    const string bearerSchemeId = JwtBearerDefaults.AuthenticationScheme;

    options.AddSecurityDefinition(
        bearerSchemeId,
        new OpenApiSecurityScheme
        {
            Scheme = "bearer",
            BearerFormat = "JWT",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Description = "Введите только JWT-токен (без префикса Bearer)."
        });

    options.AddSecurityRequirement(document =>
    {
        var securitySchemeReference = new OpenApiSecuritySchemeReference(
            bearerSchemeId,
            document,
            null);

        return new OpenApiSecurityRequirement
        {
            [securitySchemeReference] = []
        };
    });
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var statusCode = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        var (title, detail) = statusCode switch
        {
            StatusCodes.Status400BadRequest => ("Проверьте заполнение формы.", "Исправьте ошибки в форме и повторите попытку."),
            StatusCodes.Status401Unauthorized => ("Требуется авторизация", "Выполните вход и повторите попытку."),
            StatusCodes.Status403Forbidden => ("Нет доступа", "У вас недостаточно прав для этой операции."),
            StatusCodes.Status404NotFound => ("Ресурс не найден", "Запрошенные данные не найдены."),
            StatusCodes.Status409Conflict => ("Конфликт данных", "Операцию нельзя выполнить из-за конфликта данных."),
            StatusCodes.Status423Locked => ("Аккаунт заморожен", "Ваш аккаунт заморожен. Обратитесь к администратору."),
            StatusCodes.Status503ServiceUnavailable => ("Сервер временно недоступен", "Попробуйте еще раз позже."),
            StatusCodes.Status504GatewayTimeout => ("Сервер отвечает слишком долго", "Попробуйте еще раз."),
            >= StatusCodes.Status500InternalServerError => ("Ошибка сервера", "На сервере произошла ошибка. Попробуйте позже."),
            _ => ("Ошибка запроса", "Не удалось выполнить запрос. Попробуйте еще раз.")
        };

        if (IsDefaultProblemTitle(context.ProblemDetails.Title))
        {
            context.ProblemDetails.Title = title;
        }

        if (string.IsNullOrWhiteSpace(context.ProblemDetails.Detail)
            || IsDefaultProblemDetail(context.ProblemDetails.Detail))
        {
            context.ProblemDetails.Detail = detail;
        }
    };
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Проверьте заполнение формы.",
            Detail = "Исправьте ошибки в форме и повторите попытку."
        });
});
builder.Services.AddSignalR();
builder.Services
    .AddOptions<DatabaseInitializationOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<ReverseProxyOptions>()
    .Bind(builder.Configuration.GetSection(ReverseProxyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<AutoMatchingWorkerOptions>()
    .Bind(builder.Configuration.GetSection(AutoMatchingWorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<RealtorEfficiencyOptions>()
    .Bind(builder.Configuration.GetSection(RealtorEfficiencyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var reverseProxyOptions = builder.Configuration
    .GetSection(ReverseProxyOptions.SectionName)
    .Get<ReverseProxyOptions>()
    ?? new ReverseProxyOptions();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ReverseProxyConfiguration.Configure(options, reverseProxyOptions));

var rateLimitingOptions = builder.Configuration
    .GetSection(RateLimitingOptions.SectionName)
    .Get<RateLimitingOptions>()
    ?? new RateLimitingOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitingOptions.GlobalPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rateLimitingOptions.GlobalWindowSeconds)
            }));
    options.AddPolicy(RateLimitingOptions.AuthPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{context.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitingOptions.AuthPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rateLimitingOptions.AuthWindowSeconds)
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Слишком много запросов",
                Detail = "Подождите и повторите запрос позже."
            },
            cancellationToken);
    };
});

builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>(
        "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: ["ready"]);
builder.Services
    .AddOptions<RealtorCommissionOptions>()
    .Bind(builder.Configuration.GetSection(RealtorCommissionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<PropertyPhotoStorageOptions>()
    .Bind(builder.Configuration.GetSection(PropertyPhotoStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.MaxTotalUploadBytes >= options.MaxUploadBytes,
        "PropertyPhotoStorage:MaxTotalUploadBytes must be greater than or equal to MaxUploadBytes.")
    .ValidateOnStart();

var autoMapperLicenseKey = builder.Configuration[
    $"{AutoMapperLicenseOptions.SectionName}:LicenseKey"];
builder.Services.AddAutoMapper(
    options => options.LicenseKey = autoMapperLicenseKey,
    typeof(ClientService).Assembly);
builder.Services.AddAssemblyValidators(typeof(ClientService).Assembly);

builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<IClientDealCenterService, ClientDealCenterService>();
builder.Services.AddScoped<IDealChatService, DealChatService>();
builder.Services.AddScoped<AuthRegistrationService>();
builder.Services.AddScoped<IApplicationEmailService, ApplicationEmailService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<ClientRequirementService>();
builder.Services.AddScoped<ComplaintService>();
builder.Services.AddScoped<CurrencyRateService>();
builder.Services.AddScoped<DealDocumentService>();
builder.Services.AddScoped<DealService>();
builder.Services.AddScoped<PropertyMatchingNotificationService>();
builder.Services.AddScoped<PropertyService>();
builder.Services.AddScoped<PropertyCriterionDefinitionService>();
builder.Services.AddScoped<PropertyMatchingService>();
builder.Services.AddScoped<RealtorService>();
builder.Services.AddScoped<RealtorActivityLogService>();
builder.Services.AddScoped<IRealtorEligibilitySettingsService, RealtorEligibilitySettingsService>();
builder.Services.AddScoped<IRealtorCommissionSettingsService, RealtorCommissionSettingsService>();
builder.Services.AddScoped<IRealtorLevelSettingsService, RealtorLevelSettingsService>();
builder.Services.AddScoped<IRealtorLevelCalculationService, RealtorLevelCalculationService>();
builder.Services.AddScoped<IRealtorFeedbackService, RealtorFeedbackService>();
builder.Services.AddScoped<RealtorRegistrationRequestService>();
builder.Services.AddScoped<RealtorKpiCalculationService>();
builder.Services.AddScoped<IRealtorEfficiencyCalculationService, RealtorEfficiencyCalculationService>();
builder.Services.AddScoped<RealtorEfficiencyCalculationService>();
builder.Services.AddScoped<RealtorDealEligibilityService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<UserAccessManagementService>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddScoped<UserSessionService>();
builder.Services.AddSingleton<AutoMatchingWorkerState>();
builder.Services.AddHostedService<AutoMatchingBackgroundService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddInfrastructure(builder.Configuration);

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException("Отсутствует конфигурация JWT.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment()
            && !builder.Environment.IsEnvironment("Testing");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken)
                    && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var sessionIdValue = principal?.FindFirstValue(AuthSessionClaimNames.SessionId);
                var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var securityStampValue = principal?.FindFirstValue(AuthSessionClaimNames.SecurityStamp);

                if (!Guid.TryParse(sessionIdValue, out var sessionId)
                    || !Guid.TryParse(userIdValue, out var userId)
                    || string.IsNullOrWhiteSpace(securityStampValue))
                {
                    context.Fail("Сессия авторизации не найдена.");
                    return;
                }

                var sessionService = context.HttpContext.RequestServices.GetRequiredService<UserSessionService>();
                bool isActive;
                try
                {
                    isActive = await sessionService.ValidateAndTouch(
                        sessionId,
                        userId,
                        context.HttpContext.RequestAborted);
                }
                catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
                {
                    context.NoResult();
                    return;
                }

                if (!isActive)
                {
                    context.Fail("Сессия завершена.");
                    return;
                }

                var userManager = context.HttpContext.RequestServices
                    .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId.ToString());
                if (user is null
                    || !string.Equals(
                        user.SecurityStamp,
                        securityStampValue,
                        StringComparison.Ordinal))
                {
                    context.Fail("Учетные данные сессии устарели.");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(AppRoles.Admin, AppRoles.SuperAdmin));

    options.AddPolicy(
        AuthorizationPolicies.RealtorOrAdmin,
        policy => policy.RequireRole(AppRoles.Realtor, AppRoles.Admin, AppRoles.SuperAdmin));

    options.AddPolicy(
        AuthorizationPolicies.ClientOrAdmin,
        policy => policy.RequireRole(AppRoles.Client, AppRoles.Admin, AppRoles.SuperAdmin));

    options.AddPolicy(
        AuthorizationPolicies.ClientOrRealtorOrAdmin,
        policy => policy.RequireRole(AppRoles.Client, AppRoles.Realtor, AppRoles.Admin, AppRoles.SuperAdmin));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApps", policy =>
    {
        if (allowedOrigins.Contains("*"))
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Wildcard CORS разрешён только в Development. Настройте Cors__AllowedOrigins.");
            }

            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

if (runMigrations)
{
    await RunMigrationCommandAsync(app.Services, builder.Configuration, CancellationToken.None);
    app.Logger.LogInformation("Миграции базы данных успешно применены.");
    return;
}

if (bootstrapAdmin)
{
    await RunBootstrapAdminCommandAsync(app.Services, builder.Configuration, CancellationToken.None);
    app.Logger.LogInformation("Главный администратор успешно подготовлен.");
    return;
}

await InitializeDatabaseForServerAsync(
    app.Services,
    builder.Configuration,
    app.Environment,
    hasConfiguredConnectionString,
    app.Logger,
    CancellationToken.None);

if (reverseProxyOptions.Enabled)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() && !reverseProxyOptions.Enabled)
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

app.UseRouting();
app.UseCors("ClientApps");
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<FrozenAccountMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.Write
})
    .AllowAnonymous()
    .DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.Write
})
    .AllowAnonymous()
    .DisableRateLimiting();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat").DisableRateLimiting();

await app.RunAsync();

static async Task InitializeDatabaseForServerAsync(
    IServiceProvider services,
    IConfiguration configuration,
    IHostEnvironment environment,
    bool hasConfiguredConnectionString,
    ILogger logger,
    CancellationToken cancellationToken)
{
    if (!hasConfiguredConnectionString)
    {
        logger.LogWarning(
            "Строка подключения не настроена. Инициализация базы пропущена; " +
            "DB-зависимые эндпоинты недоступны.");
        return;
    }

    await using var scope = services.CreateAsyncScope();
    var options = scope.ServiceProvider
        .GetRequiredService<IOptions<DatabaseInitializationOptions>>()
        .Value;
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(options.StartupTimeoutSeconds));

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (!await dbContext.Database.CanConnectAsync(timeout.Token))
        {
            if (!options.FailOnUnavailable)
            {
                logger.LogWarning(
                    "База данных недоступна во время запуска. Readiness останется в состоянии unhealthy.");
                return;
            }

            throw new InvalidOperationException("База данных недоступна во время запуска API.");
        }

        if (options.ApplyMigrationsOnStartup)
        {
            await dbContext.Database.MigrateAsync(timeout.Token);
        }
        else if (options.FailOnPendingMigrations
                 && (await dbContext.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
        {
            throw new InvalidOperationException(
                "Схема базы данных устарела. Сначала выполните API с параметром --migrate.");
        }

        var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
        await identitySeeder.SeedAsync(timeout.Token);

        if (configuration.GetValue<bool>("Seeding:PropertyCriteriaDictionary:RunOnStartup"))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IPropertyCriteriaDictionarySeeder>();
            await seeder.SeedAsync(timeout.Token);
        }

        var photoOptions = scope.ServiceProvider.GetRequiredService<IOptions<PropertyPhotoStorageOptions>>().Value;
        if (photoOptions.LegacyMigration.Enabled)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Автоматическая миграция legacy-фотографий разрешена только в Development.");
            }

            var photoStorageService = scope.ServiceProvider.GetRequiredService<IPropertyPhotoStorageService>();
            var migration = await photoStorageService.MigrateLegacyPropertyPhotos(
                photoOptions.LegacyMigration.WebRootPath,
                photoOptions.LegacyMigration.DeleteSourceFilesAfterImport,
                timeout.Token);

            logger.LogInformation(
                "Legacy photo migration: imported={Imported}, skipped={Skipped}, missing={Missing}, failed={Failed}",
                migration.ImportedCount,
                migration.SkippedExistingCount,
                migration.MissingSourceCount,
                migration.FailedCount);
        }
    }
    catch (Exception exception) when (
        !options.FailOnUnavailable
        && (IsDatabaseConnectionException(exception)
            || (exception is OperationCanceledException
                && timeout.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested)))
    {
        logger.LogWarning(
            exception,
            "База данных недоступна во время запуска. Readiness останется в состоянии unhealthy.");
    }
}

static async Task RunMigrationCommandAsync(
    IServiceProvider services,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync(cancellationToken);

    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
    await identitySeeder.EnsureRolesAsync(cancellationToken);

    if (configuration.GetValue<bool>("Seeding:PropertyCriteriaDictionary:RunOnStartup"))
    {
        var dictionarySeeder = scope.ServiceProvider.GetRequiredService<IPropertyCriteriaDictionarySeeder>();
        await dictionarySeeder.SeedAsync(cancellationToken);
    }
}

static async Task RunBootstrapAdminCommandAsync(
    IServiceProvider services,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    var email = configuration["IdentityBootstrap:Email"]?.Trim() ?? string.Empty;
    var password = configuration["IdentityBootstrap:Password"] ?? string.Empty;
    var displayName = configuration["IdentityBootstrap:DisplayName"]?.Trim();

    if (!new EmailAddressAttribute().IsValid(email))
    {
        throw new InvalidOperationException("IdentityBootstrap__Email должен содержать корректный email.");
    }

    if (password.Length is < 12 or > 128)
    {
        throw new InvalidOperationException(
            "IdentityBootstrap__Password должен содержать от 12 до 128 символов.");
    }

    if (displayName?.Length > 100)
    {
        throw new InvalidOperationException(
            "IdentityBootstrap__DisplayName не должен превышать 100 символов.");
    }

    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
    {
        throw new InvalidOperationException(
            "Перед созданием администратора выполните API с параметром --migrate.");
    }

    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
    await identitySeeder.BootstrapSuperAdminAsync(
        new SeedUserOptions
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        },
        cancellationToken);
}

static bool IsDatabaseConnectionException(Exception ex)
{
    for (var current = ex; current is not null; current = current.InnerException)
    {
        if (current is NpgsqlException or SocketException or TimeoutException)
        {
            return true;
        }
    }

    return false;
}

static string GetRateLimitPartitionKey(HttpContext context)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

static bool IsDefaultProblemTitle(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return true;
    }

    return value.Equals("Bad Request", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Unauthorized", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Forbidden", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Not Found", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Conflict", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Internal Server Error", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Service Unavailable", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Gateway Timeout", StringComparison.OrdinalIgnoreCase)
        || value.Contains("validation errors", StringComparison.OrdinalIgnoreCase);
}

static bool IsDefaultProblemDetail(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return true;
    }

    return value.Contains("RFC 9110", StringComparison.OrdinalIgnoreCase)
        || value.Contains("validation errors", StringComparison.OrdinalIgnoreCase);
}
