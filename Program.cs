using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MarketplaceAPI.Data;
using MarketplaceAPI.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ✅ Servicios base
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ✅ Configuración de Swagger con soporte JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Marketplace API",
        Version = "v1",
        Description = "API para Empresa y Cliente con autenticación JWT"
    });

    // 🔐 Botón "Authorize"
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Introduce tu token JWT de la siguiente manera: Bearer {tu_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ✅ Conexión a SQL Server
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Configuración JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "devkey_change_me";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://marketplaceapi.azurewebsites.net";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "https://marketplaceapi.azurewebsites.net";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // 🧩 Permitir tokens locales (sin issuer/audience)
        opt.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Error: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

// ✅ Configurar roles
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Company", p => p.RequireRole("Company"));
    options.AddPolicy("Customer", p => p.RequireRole("Customer"));
});

// ✅ Inyectar servicios
builder.Services.AddScoped<PasswordHasherService>();
builder.Services.AddScoped<TokenService>();

// ✅ CORS (para permitir conexión desde Flutter o Swagger)
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

// ✅ Middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Marketplace API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
