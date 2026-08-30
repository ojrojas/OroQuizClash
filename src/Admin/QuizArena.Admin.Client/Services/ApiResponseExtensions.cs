using System.Net.Http.Json;
using System.Text.Json;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Shared HTTP response handling: maps RFC 7807 ProblemDetails from QuizArena.Api
/// into <see cref="ApiErrorException"/> with an actionable <see cref="ApiErrorView"/>.
/// Internal details never leak (FR-031): only code/title/detail/field errors surface.
/// </summary>
public static class ApiResponseExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opts.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return opts;
    }

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response, CancellationToken ct = default)
    {
        await response.ThrowIfApiErrorAsync(ct).ConfigureAwait(false);
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
        return value ?? throw new ApiErrorException(ApiErrorView.Unknown);
    }

    public static async Task ThrowIfApiErrorAsync(this HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiErrorView view;
        try
        {
            view = await ParseProblemDetailsAsync(response, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            view = DefaultView(response);
        }

        throw new ApiErrorException(view);
    }

    private static async Task<ApiErrorView> ParseProblemDetailsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            return DefaultView(response);
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return DefaultView(response);
        }

        var code = GetString(root, "code")
            ?? GetString(root, "type")
            ?? ((int)response.StatusCode).ToString();
        var title = GetString(root, "title") ?? DefaultTitle(response);
        var detail = GetString(root, "detail");

        Dictionary<string, string[]>? fieldErrors = null;
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
        {
            fieldErrors = [];
            foreach (var property in errors.EnumerateObject())
            {
                fieldErrors[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.Array => property.Value.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToArray(),
                    JsonValueKind.String => [property.Value.GetString()!],
                    _ => [property.Value.GetRawText()]
                };
            }
        }

        return new ApiErrorView(NormalizeCode(code), title, detail, fieldErrors);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string NormalizeCode(string code)
    {
        // ProblemDetails 'type' is often an absolute URI; keep the last segment as code.
        if (Uri.IsWellFormedUriString(code, UriKind.Absolute))
        {
            var uri = new Uri(code);
            var segment = uri.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrEmpty(segment))
            {
                return segment;
            }
        }
        return code;
    }

    private static ApiErrorView DefaultView(HttpResponseMessage response) =>
        new(((int)response.StatusCode).ToString(), DefaultTitle(response));

    private static string DefaultTitle(HttpResponseMessage response) => (int)response.StatusCode switch
    {
        400 => "Invalid request",
        401 => "Session expired",
        403 => "Insufficient permissions",
        404 => "Resource not found",
        409 => "Conflict with the current state",
        _ => "The operation could not be completed"
    };
}
