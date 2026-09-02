using Application.Interfaces;
using Application.Options;
using API.Auth;
using Application.Services;
using API.Background;
using API.Exceptions;
using API.Hubs;
using API.Middleware;
using API.Validation;
using FluentValidation;
using Infrastructure;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var hasConfiguredConnectionString = !string.IsNullOrWhiteSpace(configuredConnectionString);
if (!hasConfiguredConnectionString
    && !builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing"))
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
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    {
        throw new InvalidOperationException(
            "Ключ подписи JWT не настроен. Укажите Jwt__SigningKey длиной не менее 32 символов.");
    }

    builder.Configuration[$"{JwtOptions.SectionName}:SigningKey"] =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}

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
builder.Services
    .AddOptions<RealtorCommissionOptions>()
    .Bind(builder.Configuration.GetSection(RealtorCommissionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<PropertyPhotoStorageOptions>()
    .Bind(builder.Configuration.GetSection(PropertyPhotoStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAutoMapper(_ => { }, typeof(ClientService).Assembly);
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
builder.Services.AddSingleton<IRealtorCommissionSettingsService, RealtorCommissionSettingsService>();
builder.Services.AddSingleton<IRealtorLevelSettingsService, RealtorLevelSettingsService>();
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

                if (!Guid.TryParse(sessionIdValue, out var sessionId)
                    || !Guid.TryParse(userIdValue, out var userId))
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

if (hasConfiguredConnectionString)
{
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup.Seeding");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await TryCanConnectDatabaseAsync(dbContext, logger))
        {
            try
            {
                await dbContext.Database.MigrateAsync();

                var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
                await identitySeeder.SeedAsync();

                if (builder.Configuration.GetValue<bool>("Seeding:PropertyCriteriaDictionary:RunOnStartup"))
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<IPropertyCriteriaDictionarySeeder>();
                    await seeder.SeedAsync();
                }

                var photoOptions = scope.ServiceProvider.GetRequiredService<IOptions<PropertyPhotoStorageOptions>>().Value;
                if (photoOptions.LegacyMigration.Enabled)
                {
                    var photoStorageService = scope.ServiceProvider.GetRequiredService<IPropertyPhotoStorageService>();
                    var migration = await photoStorageService.MigrateLegacyPropertyPhotos(
                        photoOptions.LegacyMigration.WebRootPath,
                        photoOptions.LegacyMigration.DeleteSourceFilesAfterImport,
                        CancellationToken.None);

                    logger.LogInformation(
                        "Legacy photo migration: imported={Imported}, skipped={Skipped}, missing={Missing}, failed={Failed}",
                        migration.ImportedCount,
                        migration.SkippedExistingCount,
                        migration.MissingSourceCount,
                        migration.FailedCount);
                }
            }
            catch (Exception ex) when (IsDatabaseConnectionException(ex))
            {
                logger.LogWarning(
                    ex,
                    "База данных стала недоступна во время стартовой настройки. " +
                    "API продолжит запуск, но DB-зависимые эндпоинты будут временно недоступны.");
            }
        }
        else
        {
            logger.LogWarning(
                "База данных недоступна на старте. Сидирование пропущено. " +
                "API запущен, но DB-зависимые эндпоинты будут недоступны до восстановления соединения.");
        }
    }
}
else
{
    app.Logger.LogWarning(
        "Строка подключения не настроена. Инициализация базы пропущена; " +
        "DB-зависимые эндпоинты недоступны.");
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("ClientApps");
app.UseAuthentication();
app.UseMiddleware<FrozenAccountMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

static async Task<bool> TryCanConnectDatabaseAsync(AppDbContext dbContext, ILogger logger)
{
    try
    {
        return await dbContext.Database.CanConnectAsync();
    }
    catch (Exception ex) when (IsDatabaseConnectionException(ex))
    {
        logger.LogWarning(ex, "Не удалось подключиться к базе данных во время стартовой проверки.");
        return false;
    }
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
