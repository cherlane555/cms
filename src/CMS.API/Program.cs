using CMS.API.Data;
using CMS.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger / OpenAPI (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "CMS API",
        Version = "v1",
        Description = "CMS backend API (Dapper, .NET 9)"
    });
});

// CORS for local Angular dev server
const string CorsPolicy = "AllowLocalhost";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return uri.Host is "localhost" or "127.0.0.1";
                }
                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Data access
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlConnectionFactory(builder.Configuration.GetConnectionString("CMS")!));

// Repositories
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();

var app = builder.Build();

// Swagger UI at /swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors(CorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so the test project can reference the entry-point assembly's types.
public partial class Program { }
