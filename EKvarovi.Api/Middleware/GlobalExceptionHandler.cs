using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EKvarovi.Api.Middleware;

// Hvata SVAKU iznimku koja procuri iz kontrolera (npr. zaboravljen try/catch, null-ref,
// DB greska) i vraca je kao ProblemDetails JSON umjesto gole ASP.NET HTML/tekst greske -
// klijent (Blazor App) uvijek dobiva predvidljiv JSON oblik, bez obzira sto je puklo.
// Puna poruka izuzetka NIKAD ne ide u odgovor (curenje internih detalja napadacu) - u
// Production okruzenju Detail je generican, stvarna poruka i stack trace idu samo u log.
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Neuhvacena iznimka na {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Došlo je do neočekivane greške na serveru.",
            Detail = _environment.IsDevelopment() ? exception.ToString() : null,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);

        return true;
    }
}
