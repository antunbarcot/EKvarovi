using System.Text;
using System.Threading.RateLimiting;
using EKvarovi.Api.Data;
using EKvarovi.Api.Middleware;
using EKvarovi.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// QuestPDF trazi eksplicitnu potvrdu licence prije generiranja bilo kojeg dokumenta -
// Community licenca je besplatna (limit prihoda/imovine koji ovaj projekt ne dotice).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Omogucuje "Authorize" gumb u Swagger UI-ju za slanje "Bearer {token}" zaglavlja
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Unesite samo JWT token (bez \"Bearer \" prefiksa - Swagger ga dodaje automatski)."
    });
    // VAZNO: drugi argument mora biti "document" (ne null) - referenca inace ne zna
    // serijalizirati svoj "Bearer" kljuc, pa "security" u swagger.json ispadne prazan
    // objekt "{}" i Swagger UI onda NE salje Authorization header iako pise "Authorized".
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

builder.Services.AddDbContext<EKvaroviDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key nije konfiguriran. Postavi ga preko: dotnet user-secrets set \"Jwt:Key\" \"<dugacak nasumican string>\" (u EKvarovi.Api folderu).");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Vraca neuhvacene iznimke kao ProblemDetails JSON (vidi GlobalExceptionHandler) umjesto
// gole ASP.NET greske - klijent uvijek dobiva predvidljiv oblik odgovora.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Brute-force zastita na prijavu - IP adresa smije pokusati prijavu najvise 5x u minuti,
// svaki visak odmah dobiva 429 bez da uopce dotakne bazu/hashira lozinku. QueueLimit=0 =
// visak se odbija odmah (bez cekanja u redu), sto je ono sto ovdje zelimo.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// Provider je "Mock" za sada (radi bez API kljuca) - stvarni provider (npr. OpenAI)
// dodaje se kasnije kao zamjena registracije ispod, iza istog IAiService sucelja.
// ApiKey (kad zatreba) ide iskljucivo kroz dotnet user-secrets, nikad u appsettings.json.
builder.Services.Configure<AiServiceOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.AddScoped<IAiService, MockAiService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EKvaroviDbContext>();
    dbContext.Database.Migrate();
    // DemoDataSeeder MORA ici prije DemoUserSeeder-a - potonji trazi konkretne Employee
    // zapise (tehnicar@/prijavitelj@ marker) koje prvi kreira, preko Email polja.
    await EKvarovi.Api.Data.DemoDataSeeder.SeedAsync(dbContext, app.Environment.ContentRootPath);
    await EKvarovi.Api.Data.DemoUserSeeder.SeedAsync(dbContext);
}

// Configure the HTTP request pipeline.

// Mora biti PRVI middleware - jedino tako hvata iznimke iz svega sto slijedi (ukljucujuci
// autentikaciju/autorizaciju i same kontrolere).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();