using System.Net.Http.Headers;

namespace EKvarovi.App.Services;

// Registriran kao Scoped i RUCNO ugradjen u Scoped HttpClient u Program.cs (namjerno NE
// preko builder.Services.AddHttpClient(...) + AddHttpMessageHandler<T>() - vidi opsirni
// komentar u Program.cs zasto bi to u Blazor Serveru moglo procuriti token izmedu korisnika).
public class AuthorizationDelegatingHandler : DelegatingHandler
{
    private readonly CurrentUserService _currentUserService;

    public AuthorizationDelegatingHandler(CurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Osigurava da je token ucitan iz sessionStorage PRIJE prvog poziva. Stranice
        // (Locations.razor i sl.) salju svoj HTTP poziv odmah u OnInitializedAsync,
        // neovisno o - i moguce PRIJE - MainLayout-ovog EnsureInitializedAsync (roditelj
        // i dijete u Blazoru NISU strogo sekvencijalni). Bez ovoga bi prvi poziv nakon F5
        // refresha otisao BEZ Authorization headera i dobio 401 iako je korisnik prijavljen.
        // EnsureInitializedAsync je interno cache-iran, pa ovo za sve sljedece pozive
        // odmah vraca (bez dodatnog JS interop poziva).
        await _currentUserService.EnsureInitializedAsync();

        if (!string.IsNullOrWhiteSpace(_currentUserService.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentUserService.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
