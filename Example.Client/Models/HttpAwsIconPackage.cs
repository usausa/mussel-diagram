namespace Example.Client.Models;

using System.Text.Json;

public sealed class HttpAwsIconPackage(HttpClient http) : IAwsIconPackage
{
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("aws-icons", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return [.. document.RootElement.EnumerateArray().Select(static x => x.GetString()).OfType<string>()];
        }
    }

    public async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var uri = new Uri($"aws-icons/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}", UriKind.Relative);
        using var response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : null;
    }
}
