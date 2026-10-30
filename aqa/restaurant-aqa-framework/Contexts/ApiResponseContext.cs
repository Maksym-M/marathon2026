using System.Text.Json;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Contexts;

/// <summary>
/// The most recent API response of the current scenario, shared (via Reqnroll context injection)
/// between every step-definition class — so generic "Then the response status should be ..." steps
/// work no matter which class made the call. Holds the body text too, since reading it is async.
/// </summary>
public sealed class ApiResponseContext
{
    public IAPIResponse? Response { get; private set; }
    public string Body { get; private set; } = "";
    public string ErrorMessage { get; private set; } = "";

    public async Task CaptureAsync(IAPIResponse response)
    {
        Response = response;
        Body = await response.TextAsync();
        ErrorMessage = response.Ok ? "" : await response.ReadErrorMessageAsync();
    }

    /// <summary>Captures a set-up call the scenario cannot do without, and fails right there — naming what was being
    /// set up, with the status and body — when it did not succeed.</summary>
    public async Task<T> CaptureRequiredAsync<T>(IAPIResponse response, string settingUp)
    {
        await CaptureRequiredAsync(response, settingUp);
        return Json<T>();
    }

    /// <inheritdoc cref="CaptureRequiredAsync{T}"/>
    public async Task CaptureRequiredAsync(IAPIResponse response, string settingUp)
    {
        await CaptureAsync(response);
        if (!Succeeded)
            throw new InvalidOperationException($"{settingUp} failed with {Status}: {Body}");
    }

    public bool Succeeded => (Response ?? throw new InvalidOperationException("No API call has been made in this scenario yet.")).Ok;

    public int Status => (Response ?? throw new InvalidOperationException("No API call has been made in this scenario yet.")).Status;

    /// <summary>Deserializes the body, failing with the status and raw body — not a bare JsonException —
    /// when the call did not succeed, since an error response is the usual reason the shape is wrong.</summary>
    public T Json<T>()
    {
        if (!Succeeded)
            throw new InvalidOperationException($"Expected a successful response to read as {typeof(T).Name}, but got {Status}: {Body}");

        return JsonSerializer.Deserialize<T>(Body, JsonDefaults.Options)
            ?? throw new InvalidOperationException($"Response body deserialized to null for {typeof(T).Name}: {Body}");
    }

    public JsonElement JsonElement()
    {
        if (!Succeeded)
            throw new InvalidOperationException($"Expected a successful response, but got {Status}: {Body}");

        return JsonSerializer.Deserialize<JsonElement>(Body);
    }
}
