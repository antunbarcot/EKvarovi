using System.Text;
using System.Text.RegularExpressions;
using EKvarovi.Shared.DTOs;

namespace EKvarovi.Api.Services;

// Ne treba API kljuc niti vanjski poziv - radi uvijek deterministicki, na temelju
// labeliranih redaka u promptu (koje sastavlja pozivatelj, npr. AiController) i
// jednostavnih heuristika nad slobodnim tekstom opisa kvara. Zamjena "pravim" AI
// providerom (npr. OpenAI) kasnije ide iza istog IAiService sucelja, bez promjena
// u kontroleru koji ga poziva.
public class MockAiService : IAiService
{
    // FaultTypeId vrijednosti odgovaraju seed podacima iz EKvaroviDbContext
    // (SeedLookups) - MockAiService namjerno nema DbContext ovisnost (cista
    // heuristika nad tekstom), pa su ID-jevi ovdje nuzno hardkodirani.
    // Prepoznavanje je "scored" (broji se koliko kljucnih rijeci iz kategorije
    // se pojavljuje u opisu), ne "prvi pogodak pobjedjuje" - opis koji spomene
    // vise rijeci iz jedne kategorije treba jace prevagnuti nad slucajnim
    // spominjanjem jedne rijeci iz druge.
    private static readonly (string[] Keywords, int FaultTypeId, string FaultTypeName)[] FaultTypeRules =
    {
        (new[]
        {
            "struja", "struje", "struju", "kabel", "utičnic", "žarulj", "kratki spoj", "elektrik",
            "osigurač", "prekidač", "instalacij", "nema struje", "iskri", "trafostanic", "razvodn"
        }, 1, "Elektrika"),
        (new[]
        {
            "curi", "cure", "curi voda", "voda", "vode", "vodi", "slavin", "cijev", "cijevi",
            "vodokotlić", "odvod", "kanalizacij", "propušta", "prokišnjava", "kapanje", "sudoper",
            "wc školjk", "bojler"
        }, 2, "Voda"),
        (new[]
        {
            "grijanje", "grije", "radijator", "kotao", "termostat", "hladno", "hladnoć", "ne grije",
            "peć na", "toplinsk"
        }, 3, "Grijanje"),
        (new[]
        {
            "internet", "mreža", "mrežu", "mrežni", "wifi", "wi-fi", "router", "signal", "poveziv",
            "pristupna točka", "lan kabel", "switch"
        }, 4, "Mreža"),
        (new[]
        {
            "zid", "pukotin", "puknut", "strop", "krov", "vrata", "prozor", "fasad", "žbuka",
            "pod je", "pločic", "ograd", "brava", "kvaka", "staklo je", "razbijen"
        }, 5, "Građevinski radovi")
    };

    private const int OstaloFaultTypeId = 6;
    private const string OstaloFaultTypeName = "Ostalo";

    private const int PrioritySrednjiId = 2;
    private const int PriorityVisokId = 3;
    private const int PriorityKritičanId = 4;

    private static readonly string[] CriticalKeywords =
    {
        "curi plin", "plin", "požar", "gori", "eksploziv", "dim se", "iskri jako",
        "opasnost po život", "urušava", "ruši se", "struja udara"
    };

    private static readonly string[] HighKeywords =
    {
        "hitno", "opasno", "opasnost", "poplav", "puno vode", "curi jako", "ozlijeđ", "ozljed", "odmah"
    };

    public Task<string> GenerateTextAsync(string prompt)
    {
        if (prompt.Contains("sažetak", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("intervencij", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(BuildWorkOrderNarrative(prompt));
        }

        return Task.FromResult("Nema dovoljno podataka u upitu za sažetak.");
    }

    public Task<T?> GenerateStructuredAsync<T>(string prompt) where T : class
    {
        if (typeof(T) != typeof(FaultReportSuggestionDto))
        {
            return Task.FromResult<T?>(null);
        }

        var suggestion = BuildFaultReportSuggestion(prompt ?? string.Empty);
        return Task.FromResult(suggestion as T);
    }

    private static string BuildWorkOrderNarrative(string prompt)
    {
        var description = ExtractLabel(prompt, "Opis prijave") ?? "nepoznata prijava";
        var technician = ExtractLabel(prompt, "Izvršitelj") ?? "nepoznat izvršitelj";
        var total = ExtractNumber(prompt, "Broj intervencija");

        var sb = new StringBuilder();
        sb.Append($"Na prijavi \"{description}\" izvršitelj {technician} ");

        if (total is null or 0)
        {
            sb.Append("još nije evidentirao nijednu intervenciju.");
            return sb.ToString();
        }

        var successful = ExtractNumber(prompt, "Uspješne") ?? 0;
        var failed = ExtractNumber(prompt, "Neuspješne") ?? 0;
        var inProgress = ExtractNumber(prompt, "U tijeku") ?? 0;
        var totalDuration = ExtractNumber(prompt, "Ukupno trajanje (min)");

        sb.Append($"evidentirao je ukupno {total} {PluralizeCroatian(total.Value, "intervenciju", "intervencije", "intervencija")}");

        var parts = new List<string>();
        if (successful > 0)
        {
            parts.Add($"{successful} {AdjectiveFeminineForm(successful, "uspješn")}");
        }

        if (failed > 0)
        {
            parts.Add($"{failed} {AdjectiveFeminineForm(failed, "neuspješn")}");
        }

        if (inProgress > 0)
        {
            parts.Add($"{inProgress} u tijeku");
        }

        if (parts.Count > 0)
        {
            sb.Append($" ({string.Join(", ", parts)})");
        }

        sb.Append('.');

        if (totalDuration is > 0)
        {
            sb.Append($" Ukupno utrošeno vrijeme rada iznosi {totalDuration} {PluralizeCroatian(totalDuration.Value, "minutu", "minute", "minuta")}.");
        }

        var notes = ExtractAllLabels(prompt, "Bilješka");
        if (notes.Count > 0)
        {
            sb.Append($" Bilješke s intervencija: {string.Join("; ", notes)}.");
        }

        var materials = ExtractAllLabels(prompt, "Materijal");
        if (materials.Count > 0)
        {
            sb.Append($" Utrošeni materijal: {string.Join(", ", materials)}.");
        }

        return sb.ToString();
    }

    // Hrvatska mnozina prati "1 / 2-4 / 5+" obrazac (uz iznimku 11-14, koji uvijek
    // ide u "mnogo" oblik bez obzira na zadnju znamenku - npr. "14 intervencija", ne
    // "14 intervencije").
    private static string PluralizeCroatian(int count, string formOne, string formFew, string formMany)
    {
        var mod100 = count % 100;
        if (mod100 is >= 11 and <= 14)
        {
            return formMany;
        }

        return (count % 10) switch
        {
            1 => formOne,
            >= 2 and <= 4 => formFew,
            _ => formMany
        };
    }

    private static string AdjectiveFeminineForm(int count, string stem)
    {
        var mod100 = count % 100;
        if (mod100 is >= 11 and <= 14)
        {
            return stem + "ih";
        }

        return (count % 10) switch
        {
            1 => stem + "a",
            >= 2 and <= 4 => stem + "e",
            _ => stem + "ih"
        };
    }

    private static FaultReportSuggestionDto BuildFaultReportSuggestion(string description)
    {
        var normalized = RemoveDiacritics(description.ToLowerInvariant());

        var (faultTypeId, faultTypeName, matchedTypeKeywords) = MatchFaultType(normalized);
        var (priorityId, priorityName, matchedPriorityKeywords) = MatchPriority(normalized);

        var reasoning = new List<string>
        {
            matchedTypeKeywords.Count > 0
                ? $"prepoznate ključne riječi ({string.Join(", ", matchedTypeKeywords.Select(k => $"\"{k}\""))}) upućuju na tip \"{faultTypeName}\""
                : $"nije prepoznata nijedna ključna riječ za tip kvara, pa je predložen zadani tip \"{faultTypeName}\"",
            matchedPriorityKeywords.Count > 0
                ? $"prepoznate ključne riječi ({string.Join(", ", matchedPriorityKeywords.Select(k => $"\"{k}\""))}) upućuju na prioritet \"{priorityName}\""
                : $"nije prepoznata nijedna hitna ključna riječ, pa je predložen zadani prioritet \"{priorityName}\""
        };

        return new FaultReportSuggestionDto
        {
            SuggestedTitle = BuildTitle(description),
            SuggestedFaultTypeId = faultTypeId,
            SuggestedFaultTypeName = faultTypeName,
            SuggestedFaultPriorityId = priorityId,
            SuggestedFaultPriorityName = priorityName,
            Reasoning = string.Join("; ", reasoning) + "."
        };
    }

    // Bira kategoriju s najvise pogodenih kljucnih rijeci (ne prvu koja se pojavi u
    // popisu pravila) - opis koji spominje vise razlicitih elektricnih pojmova treba
    // prevagnuti nad slucajnim spominjanjem jedne rijeci iz druge kategorije.
    private static (int Id, string Name, List<string> MatchedKeywords) MatchFaultType(string normalizedDescription)
    {
        (int Id, string Name, List<string> Keywords)? best = null;

        foreach (var rule in FaultTypeRules)
        {
            var matched = rule.Keywords
                .Where(keyword => normalizedDescription.Contains(RemoveDiacritics(keyword)))
                .ToList();

            if (matched.Count > 0 && (best is null || matched.Count > best.Value.Keywords.Count))
            {
                best = (rule.FaultTypeId, rule.FaultTypeName, matched);
            }
        }

        return best is null
            ? (OstaloFaultTypeId, OstaloFaultTypeName, new List<string>())
            : (best.Value.Id, best.Value.Name, best.Value.Keywords);
    }

    private static (int Id, string Name, List<string> MatchedKeywords) MatchPriority(string normalizedDescription)
    {
        var criticalMatches = CriticalKeywords
            .Where(keyword => normalizedDescription.Contains(RemoveDiacritics(keyword)))
            .ToList();
        if (criticalMatches.Count > 0)
        {
            return (PriorityKritičanId, "Kritičan", criticalMatches);
        }

        var highMatches = HighKeywords
            .Where(keyword => normalizedDescription.Contains(RemoveDiacritics(keyword)))
            .ToList();
        if (highMatches.Count > 0)
        {
            return (PriorityVisokId, "Visok", highMatches);
        }

        return (PrioritySrednjiId, "Srednji", new List<string>());
    }

    private static string BuildTitle(string description)
    {
        var trimmed = description.Trim();
        if (trimmed.Length == 0)
        {
            return "Nova prijava";
        }

        return trimmed.Length > 60 ? trimmed[..60].TrimEnd() + "…" : trimmed;
    }

    // Korisnici cesto pisu bez hrvatskih dijakritika (npr. "uticnica" umjesto
    // "utičnica") - da prepoznavanje kljucnih rijeci ne ovisi o tome, i opis i
    // kljucne rijeci se prije usporedbe svode na verziju bez dijakritika.
    private static string RemoveDiacritics(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            sb.Append(ch switch
            {
                'č' or 'ć' => 'c',
                'š' => 's',
                'ž' => 'z',
                'đ' => 'd',
                'Č' or 'Ć' => 'C',
                'Š' => 'S',
                'Ž' => 'Z',
                'Đ' => 'D',
                _ => ch
            });
        }

        return sb.ToString();
    }

    private static string? ExtractLabel(string prompt, string label)
    {
        var match = Regex.Match(prompt, $@"^\s*{Regex.Escape(label)}:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static int? ExtractNumber(string prompt, string label)
    {
        var value = ExtractLabel(prompt, label);
        if (value is null)
        {
            return null;
        }

        var numberMatch = Regex.Match(value, @"\d+");
        return numberMatch.Success ? int.Parse(numberMatch.Value) : null;
    }

    private static List<string> ExtractAllLabels(string prompt, string label)
    {
        return Regex.Matches(prompt, $@"^\s*{Regex.Escape(label)}:\s*(.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(v => v.Length > 0)
            .ToList();
    }
}
