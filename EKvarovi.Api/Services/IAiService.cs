namespace EKvarovi.Api.Services;

public interface IAiService
{
    Task<string> GenerateTextAsync(string prompt);
    Task<T?> GenerateStructuredAsync<T>(string prompt) where T : class;
}
