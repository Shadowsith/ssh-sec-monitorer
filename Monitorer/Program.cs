using System.Globalization;
using Dapper;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi.Models;
using Monitorer;
using Monitorer.BackgroundServices;
using Monitorer.Data;
using Monitorer.Extensions;
using Monitorer.Filters;
using Monitorer.Hubs;
using Monitorer.Middleware;
using Monitorer.Repository;
using Monitorer.Services;
using Monitorer.Services.Cve;
using Serilog;

// Bootstrap logger — catches errors before the configured logger is ready
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    DefaultTypeMap.MatchNamesWithUnderscores = true;

    // ---------------------------------------------------------------- settings
    var config = builder.Configuration;
    builder.Services.Configure<DbConfig>(config.GetSection("DbConfig"));
    builder.Services.Configure<JwtSettings>(config.GetSection("Jwt"));
    builder.Services.Configure<AuthSettings>(config.GetSection("Auth"));
    builder.Services.Configure<SetupSettings>(config.GetSection("Setup"));
    builder.Services.Configure<LdapSettings>(config.GetSection("Ldap"));
    builder.Services.Configure<MailSettings>(config.GetSection("Mail"));
    builder.Services.Configure<MonitoringSettings>(config.GetSection("Monitoring"));
    builder.Services.Configure<CveSettings>(config.GetSection("Cve"));
    builder.Services.Configure<SwaggerSettings>(config.GetSection("Swagger"));

    var dbConfig = config.GetSection("DbConfig").Get<DbConfig>() ?? new DbConfig();
    var jwtSettings = config.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
    var authSettings = config.GetSection("Auth").Get<AuthSettings>() ?? new AuthSettings();
    var dpSettings = config.GetSection("DataProtection").Get<DataProtectionSettings>() ?? new DataProtectionSettings();
    var swaggerSettings = config.GetSection("Swagger").Get<SwaggerSettings>() ?? new SwaggerSettings();
    var localizationSettings = config.GetSection("Localization").Get<LocalizationSettings>() ?? new LocalizationSettings();

    // ---------------------------------------------------------------- localization (i18n/<language>.json)
    var localizer = JsonStringLocalizer.Load(localizationSettings.DefaultLanguage);
    builder.Services.AddSingleton(localizer);
    builder.Services.AddSingleton<IStringLocalizer>(localizer);
    var defaultCulture = CultureInfo.GetCultureInfo(localizer.DefaultLanguage);
    // background work (polls, live checks, alert mails) has no request: it runs in the default language
    CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
    CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
    builder.Services.Configure<RequestLocalizationOptions>(o =>
    {
        var cultures = localizer.Languages.Select(CultureInfo.GetCultureInfo).ToList();
        o.DefaultRequestCulture = new RequestCulture(defaultCulture);
        o.SupportedCultures = cultures;
        o.SupportedUICultures = cultures;
        // only the language cookie (set on /Language) and Accept-Language, no ?culture= query
        o.RequestCultureProviders = o.RequestCultureProviders
            .Where(p => p is not QueryStringRequestCultureProvider).ToList();
    });

    // ---------------------------------------------------------------- database
    builder.Services.AddSingleton(new DbConnectionFactory(dbConfig.ConnectionString));
    builder.Services.AddSingleton<DatabaseInitializer>();
    builder.Services.AddFluentMigratorCore()
        .ConfigureRunner(rb => rb
            .AddPostgres()
            .WithGlobalConnectionString(dbConfig.ConnectionString)
            .ScanIn(typeof(Program).Assembly).For.Migrations());

    // ---------------------------------------------------------------- data protection (encrypts stored secrets)
    builder.Services.AddDataProtection()
        .SetApplicationName("SshSecMonitorer")
        .PersistKeysToFileSystem(new DirectoryInfo(Path.IsPathRooted(dpSettings.KeyPath)
            ? dpSettings.KeyPath
            : Path.Combine(builder.Environment.ContentRootPath, dpSettings.KeyPath)));

    // ---------------------------------------------------------------- authentication: JWT in header or session cookie
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = TokenService.CreateValidationParameters(jwtSettings);
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    if (string.IsNullOrEmpty(ctx.Token) && !ctx.Request.Headers.ContainsKey("Authorization")
                        && ctx.Request.Cookies.TryGetValue(authSettings.CookieName, out var cookieToken))
                        ctx.Token = cookieToken;
                    return Task.CompletedTask;
                },
                OnTokenValidated = async ctx =>
                {
                    // deactivated / deleted users lose access immediately, not only when the token expires
                    var users = ctx.HttpContext.RequestServices.GetRequiredService<UserService>();
                    if (!await users.IsActive(ctx.Principal!.GetUserId()))
                        ctx.Fail("User is inactive or deleted.");
                },
                OnChallenge = ctx =>
                {
                    // API and SignalR (fetch / WebSocket) get a plain 401 instead of the login redirect
                    if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/hubs"))
                        return Task.CompletedTask;
                    ctx.HandleResponse();
                    var returnUrl = Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString);
                    ctx.Response.Redirect($"/Login?returnUrl={returnUrl}");
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(o =>
    {
        // everything requires a login unless marked [AllowAnonymous]
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    });

    builder.Services.AddAntiforgery(o =>
    {
        o.HeaderName = "X-CSRF-TOKEN";
        o.Cookie.Name = "ssm_csrf";
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = authSettings.AllowHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });

    // ---------------------------------------------------------------- MVC: REST controllers + Razor Pages
    builder.Services.AddControllers(o => o.Filters.Add<ApiAntiforgeryFilter>());
    builder.Services.AddRazorPages();
    builder.Services.AddSignalR();

    if (swaggerSettings.Enabled)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "SshSecMonitorer API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Token from POST /api/auth/login"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    Array.Empty<string>()
                }
            });
            var xml = Path.Combine(AppContext.BaseDirectory, "Monitorer.xml");
            if (File.Exists(xml))
                c.IncludeXmlComments(xml);
        });
    }

    // ---------------------------------------------------------------- HTTP clients for live checks
    builder.Services.AddHttpClient(LiveCheckRunnerService.HttpClientName);
    builder.Services.AddHttpClient(LiveCheckRunnerService.InsecureHttpClientName)
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

    // ---------------------------------------------------------------- HTTP client for the CVE sources
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient(CveHttpService.ClientName, c =>
    {
        c.Timeout = TimeSpan.FromSeconds(30);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("SshSecMonitorer/1.0");
    });

    // ---------------------------------------------------------------- application services
    builder.Services.AddSingleton<SetupState>();
    builder.Services.AddSingleton<LoginThrottleService>();
    builder.Services.AddSingleton<PollScheduler>();
    builder.Services.AddSingleton<LiveUpdateService>();

    builder.Services.AddScoped<UserRepository>();
    builder.Services.AddScoped<SshCredentialRepository>();
    builder.Services.AddScoped<AgentRepository>();
    builder.Services.AddScoped<AgentSnapshotRepository>();
    builder.Services.AddScoped<LiveCheckRepository>();
    builder.Services.AddScoped<CertCheckRepository>();
    builder.Services.AddScoped<CveCheckRepository>();
    builder.Services.AddScoped<CveFindingRepository>();
    builder.Services.AddScoped<KnownContainerRepository>();
    builder.Services.AddScoped<AlertRuleRepository>();
    builder.Services.AddScoped<AlertStateRepository>();

    builder.Services.AddScoped<SecretService>();
    builder.Services.AddScoped<TokenService>();
    builder.Services.AddScoped<SessionCookieService>();
    builder.Services.AddScoped<LdapService>();
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<SetupService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<EmailService>();
    builder.Services.AddScoped<AlertService>();
    builder.Services.AddScoped<SshFetchService>();
    builder.Services.AddSingleton<VmSetupScriptService>();
    builder.Services.AddScoped<SshCredentialService>();
    builder.Services.AddScoped<LocalMonitoringSource>();
    builder.Services.AddScoped<MonitoringFetchService>();
    builder.Services.AddScoped<MonitoringEvaluationService>();
    builder.Services.AddScoped<KnownContainerService>();
    builder.Services.AddScoped<AgentService>();
    builder.Services.AddScoped<AgentPollService>();
    builder.Services.AddScoped<LiveCheckService>();
    builder.Services.AddScoped<LiveCheckRunnerService>();
    builder.Services.AddScoped<CertCheckService>();
    builder.Services.AddScoped<CertCheckRunnerService>();
    builder.Services.AddScoped<CveHttpService>();
    builder.Services.AddScoped<ICveProvider, BsiCveProvider>();
    builder.Services.AddScoped<ICveProvider, TldrCveProvider>();
    builder.Services.AddScoped<ICveProvider, OpenCveProvider>();
    builder.Services.AddScoped<ICveProvider, OpenCveKbProvider>();
    builder.Services.AddScoped<CveEnrichmentService>();
    builder.Services.AddScoped<CveCheckService>();
    builder.Services.AddScoped<CveCheckRunnerService>();
    builder.Services.AddScoped<CveFindingService>();
    builder.Services.AddScoped<OverviewService>();
    builder.Services.AddScoped<DockerOverviewService>();
    builder.Services.AddScoped<OverviewChartService>();
    builder.Services.AddScoped<AlertConditionService>();
    builder.Services.AddScoped<AlertEngineService>();
    builder.Services.AddScoped<AlertRuleService>();

    builder.Services.AddHostedService<AgentPollingBackgroundService>();
    builder.Services.AddHostedService<LiveCheckBackgroundService>();
    builder.Services.AddHostedService<CertCheckBackgroundService>();
    builder.Services.AddHostedService<CveCheckBackgroundService>();
    builder.Services.AddHostedService<AlertBackgroundService>();

    // ---------------------------------------------------------------- pipeline
    var app = builder.Build();

    foreach (var (language, key) in localizer.MissingKeys())
        Log.Warning("Translation {Key} missing in i18n/{Language}.json - the default language is used", key, language);

    app.MigrateDatabase();
    await app.SeedInitialAdmin();

    // Behind the HTTPS reverse proxy: take the original scheme from X-Forwarded-Proto so that
    // secure cookies / antiforgery work. Only the scheme is taken over, never X-Forwarded-For -
    // the client IP feeds the login throttle and must not be spoofable.
    var forwardedOptions = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
    forwardedOptions.KnownIPNetworks.Clear();   // the proxy reaches the container via a Docker network
    forwardedOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedOptions);

    app.UseSerilogRequestLogging();
    app.UseRequestLocalization();
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        if (!authSettings.AllowHttp)
            app.UseHsts();
    }
    app.UseMiddleware<SecurityHeadersHandler>();
    // HTML error pages only for the UI; the API keeps its plain status codes
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
        branch => branch.UseStatusCodePagesWithReExecute("/Error", "?code={0}"));

    app.UseStaticFiles();
    if (swaggerSettings.Enabled)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseMiddleware<SetupRedirectHandler>();
    app.UseAuthorization();

    app.MapControllers();
    app.MapRazorPages();
    // the session JWT expires -> the connection is closed; live.js then reloads into the login page
    app.MapHub<MonitorHub>(MonitorHub.Path, o => o.CloseOnAuthenticationExpiration = true);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
