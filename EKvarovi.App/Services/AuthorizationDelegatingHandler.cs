using System.Net.Http.Headers;

namespace EKvarovi.App.Services;

public class AuthorizationDelegatingHandler : DelegatingHandler
{
    private readonly CurrentUserService _currentUserService;

    public AuthorizationDelegatingHandler(CurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _currentUserService.EnsureInitializedAsync();

        if (!string.IsNullOrWhiteSpace(_currentUserService.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentUserService.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
