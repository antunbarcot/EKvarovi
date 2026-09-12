using EKvarovi.App.Components;
using EKvarovi.App.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    // Skraceno trajanje success/error Snackbar obavijesti (MudBlazor default je ~5s) -
    // vrijedi globalno za SVAKI Snackbar.Add poziv u aplikaciji.
    config.SnackbarConfiguration.VisibleStateDuration = 2000;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB, isto kao API limit
});

// Blazor Server salje sadrzaj InputFile-a kroz SignalR krug (ne kao HTTP multipart POST),
// pa default MaximumReceiveMessageSize (32 KB) blokira upload vecih fotografija/PDF-ova -
// mora se povecati na isti limit kao API (10 MB), inace baca "message size limit" gresku.
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
});

builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AuthorizationDelegatingHandler>();

// HttpClient je Scoped (jedan po Blazor Server "circuitu"/korisnickoj sesiji) i RUCNO
// ozicen s AuthorizationDelegatingHandler - namjerno NE preko builder.Services.AddHttpClient(...)
// + AddHttpMessageHandler<T>(). Razlog: IHttpClientFactory ne izgradjuje svoj lanac handlera
// unutar scope-a koji je trazio HttpClient - koristi VLASTITI, odvojeni scope kreiran iz root
// service providera i taj se scope keš-ira/dijeli izmedu SVIH poziva do isteka HandlerLifetime-a
// (defaultno 2 minute). Kad bi AuthorizationDelegatingHandler bio registriran preko
// AddHttpMessageHandler<T>(), CurrentUserService koji on prima kroz konstruktor NE bi bio
// scoped na trenutni krug/korisnika, nego bi mogao zavrsiti dijeljen izmedu razlicitih
// korisnika koji su se spojili unutar istog "handler lifetime" prozora - token jednog
// korisnika bi mogao procuriti u pozive drugog. Rucnom izgradnjom HttpClient-a ovdje, unutar
// AddScoped(sp => ...) tvornicke funkcije, "sp" JEST scope trenutnog kruga, pa se
// AuthorizationDelegatingHandler i CurrentUserService razrjesavaju iz ISTOG scope-a i
// Authorization header je uvijek za pravog prijavljenog korisnika.
builder.Services.AddScoped(sp =>
{
    var authHandler = sp.GetRequiredService<AuthorizationDelegatingHandler>();
    authHandler.InnerHandler = new HttpClientHandler();

    // disposeHandler: false - DI kontejner vec upravlja zivotnim ciklusom
    // AuthorizationDelegatingHandler-a (Scoped), koji dispose-a i svoj InnerHandler.
    // Kad bi HttpClient TAKODJER dispose-ao handler, dobili bismo (bezopasno, ali suvisno)
    // dvostruko brisanje pri kraju kruga.
    return new HttpClient(authHandler, disposeHandler: false)
    {
        BaseAddress = new Uri("https://localhost:7094/")
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
