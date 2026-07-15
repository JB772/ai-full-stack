using System.Text;
using CMS.API.Data;
using CMS.API.Repositories;
using CMS.API.Security;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Dapper type handlers for SQL Server date / time(n) columns.
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

const string LocalhostCorsPolicy = "LocalhostCors";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CMS API",
        Version = "v1",
        Description = "CMS 內容管理系統 API"
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "CMS.API.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Let Swagger UI send "Authorization: Bearer <token>" via the Authorize button.
    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "貼上登入取得的 JWT (不含 'Bearer ' 前綴)。",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearerScheme] = [] });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalhostCorsPolicy, policy => policy
        .SetIsOriginAllowed(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<JwtSigningKeyProvider>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IFeaturedPromoItemRepository, FeaturedPromoItemRepository>();
builder.Services.AddScoped<ITrainingCenterRepository, TrainingCenterRepository>();
builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();

// JWT bearer authentication. The signing key is the same SysConfig['appConfig'].symmetricSecurityKey
// used to issue tokens; it is resolved at runtime (via JwtSigningKeyProvider), never hard-coded.
// Tokens are issued with HMAC-SHA256 and carry no issuer/audience, so those checks are off.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSigningKeyProvider>((options, signingKey) =>
    {
        options.MapInboundClaims = false; // keep the raw claim types (role / userId / userName)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtTokenService.UserIdClaimType,
            RoleClaimType = JwtTokenService.RoleClaimType,
            IssuerSigningKeyResolver = (_, _, _, _) =>
                [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey.Get()))]
        };
    });

// Global authorization: every endpoint requires an authenticated user unless it opts out with
// [AllowAnonymous] (only AuthController does).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors(LocalhostCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so the xUnit test project can boot the app with WebApplicationFactory.</summary>
public partial class Program;
