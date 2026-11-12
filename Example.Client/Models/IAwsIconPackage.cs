namespace Example.Client.Models;

public interface IAwsIconPackage
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);

    Task<string?> ReadAsync(string path, CancellationToken cancellationToken);
}
