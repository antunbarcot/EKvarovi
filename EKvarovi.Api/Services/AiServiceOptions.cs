namespace EKvarovi.Api.Services;

// appsettings.json drzi samo Provider ("Mock"/"OpenAI") - ApiKey se NIKAD ne pise
// ovdje niti u Git; ako se ikad doda stvarni provider, kljuc ide iskljucivo kroz
// dotnet user-secrets (isto pravilo kao Jwt:Key).
public class AiServiceOptions
{
    public string Provider { get; set; } = "Mock";
    public string ApiKey { get; set; } = string.Empty;
}
