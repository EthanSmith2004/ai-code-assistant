using Microsoft.JSInterop;

namespace AiCodeAssistant.Client.Services;

public class UserPreferencesService
{
    private const string DefaultGraphLayoutKey = "codesight.defaultGraphLayout";
    private const string DefaultGraphLayout = "breadthfirst";
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
        return string.Equals(layout, "cose", StringComparison.OrdinalIgnoreCase)
            ? "cose"
            : DefaultGraphLayout;
    }
}
