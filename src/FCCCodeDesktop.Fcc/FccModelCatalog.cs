using System.Text.Json;

namespace FCCCodeDesktop.Fcc;

public static class FccModelCatalog
{
    public static async Task<IReadOnlyList<string>> ListAsync(Uri healthEndpoint, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try { return await ListCoreAsync(healthEndpoint, deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("FCC model discovery timed out. Check the local server and retry.");
        }
    }

    private static async Task<IReadOnlyList<string>> ListCoreAsync(Uri healthEndpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(healthEndpoint);
        if (!healthEndpoint.IsAbsoluteUri || !healthEndpoint.IsLoopback || healthEndpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("FCC model discovery requires a loopback HTTP endpoint.", nameof(healthEndpoint));
        }
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var response = await client.GetAsync(new Uri(healthEndpoint, "/v1/models"), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + count > 256 * 1024) { throw new InvalidOperationException("FCC model list exceeds its size limit."); }
            buffer.Write(chunk, 0, count);
        }
        using var json = JsonDocument.Parse(buffer.ToArray());
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("FCC did not return a valid model catalog.");
        }
        var models = new List<string>();
        foreach (var item in data.EnumerateArray())
        {
            if (models.Count >= 512) { break; }
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) { continue; }
            var name = id.GetString();
            if (!string.IsNullOrWhiteSpace(name) && name.Length <= 256 && !name.Any(char.IsControl)
                && !models.Contains(name, StringComparer.Ordinal)) { models.Add(name); }
        }
        if (models.Count == 0) { throw new InvalidOperationException("FCC has no available models. Configure a provider and retry."); }
        return models;
    }
}
