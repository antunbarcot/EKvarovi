using EKvarovi.Shared.DTOs;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace EKvarovi.App.Services;

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

    public bool IsInitialized => _initialized;

    public event Action? OnChange;

    public CurrentUserService(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

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
                    await _sessionStorage.DeleteAsync(StorageKey);
                }
                else
                {
                    ApplyStoredUser(result.Value);
                }
            }

            _initialized = true;

            OnChange?.Invoke();
        }
        catch (InvalidOperationException)
        {
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
