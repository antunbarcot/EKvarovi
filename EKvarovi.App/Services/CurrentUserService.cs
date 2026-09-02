using EKvarovi.Shared.DTOs;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace EKvarovi.App.Services;

// Scoped - jedna instanca po Blazor Server "circuitu" (korisnickoj sesiji/tabu), nikad
// se ne dijeli izmedu razlicitih korisnika. Cuva prijavljenog korisnika u memoriji tijekom
// kruga, a u ProtectedSessionStorage da prijava prezivi F5 refresh unutar istog taba.
public class CurrentUserService
{
    private const string StorageKey = "ekvarovi.auth";

    private readonly ProtectedSessionStorage _sessionStorage;
    private bool _initialized;

    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public string? DisplayName { get; private set; }
    public List<string> Roles { get; private set; } = new();
    public int? EmployeeId { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);

    // True tek kad je EnsureInitializedAsync stvarno uspio procitati sessionStorage (ili
    // je SetUserAsync/LogoutAsync eksplicitno pozvan). Dok je false, jos NE znamo je li
    // korisnik prijavljen ili ne - vidi komentar u EnsureInitializedAsync i MainLayout.
    public bool IsInitialized => _initialized;

    // MainLayout/NavMenu se pretplacuju na ovo da se odmah osvjeze nakon SetUserAsync/LogoutAsync -
    // Blazor ne re-renderira automatski komponente samo zato sto je servis promijenio stanje.
    public event Action? OnChange;

    public CurrentUserService(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    // Ucitava token iz sessionStorage - poziva se na pocetku svakog zasticenog dijela UI-ja
    // (vidi MainLayout). Interno cache-irano (_initialized), pa je sigurno pozvati vise puta.
    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            var result = await _sessionStorage.GetAsync<StoredUser>(StorageKey);
            if (result.Success && result.Value is not null)
            {
                if (result.Value.ExpiresAt <= DateTime.UtcNow)
                {
                    // Istekao token - ne vracamo ga kao "prijavljen", samo ga pocistimo.
                    await _sessionStorage.DeleteAsync(StorageKey);
                }
                else
                {
                    ApplyStoredUser(result.Value);
                }
            }

            _initialized = true;

            // Prijelaz iz "jos ne znamo" u "sad znamo" - komponente koje su se vec
            // renderirale (npr. NavMenu) prije nego je storage procitan moraju se
            // eksplicitno obavijestiti, jer se ne rendera nista ispocetka samo zato
            // sto je MainLayout-ov OnInitializedAsync zavrsio.
            OnChange?.Invoke();
        }
        catch (InvalidOperationException)
        {
            // JS interop jos nije dostupan (staticni prerender prije spajanja SignalR kruga) -
            // NE postavljamo _initialized na true. Prerender koristi drugi DI scope od
            // interaktivnog kruga, pa ce sljedeci (stvarni) prolaz probati s NOVOM instancom
            // ovog servisa i JS interop ce tad raditi.
        }
    }

    public async Task SetUserAsync(LoginResponseDto response)
    {
        Token = response.Token;
        Email = response.Email;
        DisplayName = string.IsNullOrWhiteSpace(response.DisplayName) ? response.Email : response.DisplayName;
        Roles = response.Roles ?? new List<string>();
        EmployeeId = response.EmployeeId;
        _initialized = true;

        await _sessionStorage.SetAsync(StorageKey, new StoredUser(Token, Email, DisplayName, Roles, EmployeeId, response.ExpiresAt));
        OnChange?.Invoke();
    }

    public async Task LogoutAsync()
    {
        Token = null;
        Email = null;
        DisplayName = null;
        Roles = new List<string>();
        EmployeeId = null;
        _initialized = true;

        await _sessionStorage.DeleteAsync(StorageKey);
        OnChange?.Invoke();
    }

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private void ApplyStoredUser(StoredUser stored)
    {
        Token = stored.Token;
        Email = stored.Email;
        DisplayName = stored.DisplayName;
        Roles = stored.Roles;
        EmployeeId = stored.EmployeeId;
    }

    private record StoredUser(string? Token, string? Email, string? DisplayName, List<string> Roles, int? EmployeeId, DateTime ExpiresAt);
}
