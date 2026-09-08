namespace EKvarovi.Api.Services;

// Apstrakcija oko AI providera - App/kontroleri ovise samo o ovom sucelju, nikad
// direktno o konkretnom provideru (Mock danas, eventualno OpenAI kasnije), tako da
// zamjena providera ne trazi promjene izvan Services sloja.
public interface IAiService
{
    Task<string> GenerateTextAsync(string prompt);
    Task<T?> GenerateStructuredAsync<T>(string prompt) where T : class;
}
