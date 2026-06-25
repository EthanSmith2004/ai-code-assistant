using Microsoft.JSInterop;

namespace AiCodeAssistant.Client.Services;

public class UserPreferencesService
{
    private const string DefaultGraphLayoutKey = "codesight.defaultGraphLayout";
    private const string DefaultGraphLayout = "cose";
    private readonly IJSRuntime _jsRuntime;

    public UserPreferencesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> GetDefaultGraphLayoutAsync()
    {
        try
        {
            var storedLayout = await _jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                DefaultGraphLayoutKey);

            return NormalizeGraphLayout(storedLayout);
        }
        catch (JSException)
        {
            return DefaultGraphLayout;
        }
    }

    public async Task SaveDefaultGraphLayoutAsync(string layout)
    {
        var normalizedLayout = NormalizeGraphLayout(layout);

        await _jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            DefaultGraphLayoutKey,
            normalizedLayout);
    }

    public static string NormalizeGraphLayout(string? layout)
    {
        return string.Equals(layout, "breadthfirst", StringComparison.OrdinalIgnoreCase)
            ? "breadthfirst"
            : DefaultGraphLayout;
    }
}
