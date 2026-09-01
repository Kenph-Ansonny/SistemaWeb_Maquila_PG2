using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MaquilaBackend.Data;

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

// Generador de Swagger con versión y título explícitos
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "MaquilaBackend API",
        Description = "API de Gestión y Seguridad del Sistema de Maquila"
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
app.UseAuthorization();
app.MapControllers();

app.Run();