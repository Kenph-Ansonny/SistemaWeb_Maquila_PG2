using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MaquilaBackend.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MaquilaBackend.Security;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configuración de conexión MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<MaquilaDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Soporte CORS para Vue 3
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- Autenticación JWT ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];

// Falla rápido y explícito si falta configurar la clave, en vez de arrancar
// "seguro en apariencia" con un token que nadie firmó correctamente.
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException(
        "La clave JWT (Jwt:Key) no está configurada. Defínela con: " +
        "dotnet user-secrets set \"Jwt:Key\" \"<valor-aleatorio-de-32+-caracteres>\"");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermisoHandler>();

// Generador de Swagger con versión y título explícitos
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "MaquilaBackend API",
        Description = "API de Gestión y Seguridad del Sistema de Maquila"
    });

    // Permite probar endpoints protegidos directamente desde Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT así: Bearer {tu token}"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Genera el swagger.json en la ruta estándar
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MaquilaBackend API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowVueApp");

// El orden importa: primero se autentica (¿quién eres?),
// luego se autoriza (¿qué puedes hacer?).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();