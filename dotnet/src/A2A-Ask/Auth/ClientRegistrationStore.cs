using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace A2AAsk.Auth;

/// <summary>
/// Persists and loads OAuth2 client registrations scoped by issuer and optional resource.
/// On Windows, registrations are encrypted using DPAPI (current user scope).
/// On other platforms, registrations are stored as plaintext JSON.
/// Store path: ~/.a2a-ask/clients.dat (encrypted) or clients.json (plaintext).
/// </summary>
public class ClientRegistrationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storePath;
    private readonly bool _useEncryption;

    public ClientRegistrationStore(string? storePath = null, bool? useEncryption = null)
    {
        var defaultEncrypt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _useEncryption = useEncryption ?? defaultEncrypt;
        _storePath = storePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".a2a-ask",
            _useEncryption ? "clients.dat" : "clients.json");
    }

    public async Task RegisterClientAsync(ClientRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var normalizedRegistration = new ClientRegistration
        {
            ClientId = string.IsNullOrWhiteSpace(registration.ClientId)
                ? throw new ArgumentException("ClientId is required.", nameof(registration))
                : registration.ClientId,
            Issuer = NormalizeIssuer(registration.Issuer),
            Resource = NormalizeResource(registration.Resource),
            CreatedAt = registration.CreatedAt == default ? DateTime.UtcNow : registration.CreatedAt
        };

        var registrations = await LoadAllClientsAsync();
        var key = BuildStorageKey(normalizedRegistration.Issuer, normalizedRegistration.Resource);
        registrations.RemoveAll(r => BuildStorageKey(r.Issuer, r.Resource) == key);
        registrations.Add(normalizedRegistration);
        await SaveAllClientsAsync(registrations);
    }

    public async Task<IReadOnlyList<ClientRegistration>> ListClientsAsync()
    {
        var registrations = await LoadAllClientsAsync();
        return registrations
            .OrderBy(r => r.Issuer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Resource ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ClientId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> RemoveClientAsync(string issuer, string? resource)
    {
        var registrations = await LoadAllClientsAsync();
        var key = BuildStorageKey(issuer, resource);
        var removed = registrations.RemoveAll(r => BuildStorageKey(r.Issuer, r.Resource) == key) > 0;
        if (removed)
            await SaveAllClientsAsync(registrations);

        return removed;
    }

    public async Task<ClientRegistration?> FindClientAsync(string issuer, string? resource = null)
    {
        var registrations = await LoadAllClientsAsync();
        var normalizedIssuer = NormalizeIssuer(issuer);
        var normalizedResource = NormalizeResource(resource);

        if (!string.IsNullOrEmpty(normalizedResource))
        {
            var exactMatch = registrations.FirstOrDefault(r =>
                string.Equals(r.Issuer, normalizedIssuer, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.Resource, normalizedResource, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;
        }

        return registrations.FirstOrDefault(r =>
            string.Equals(r.Issuer, normalizedIssuer, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(r.Resource));
    }

    internal static string NormalizeIssuer(string issuer) => NormalizeUrl(issuer);

    internal static string? NormalizeResource(string? resource) =>
        string.IsNullOrWhiteSpace(resource) ? null : NormalizeUrl(resource);

    internal static string BuildStorageKey(string issuer, string? resource)
    {
        var normalizedIssuer = NormalizeIssuer(issuer);
        var normalizedResource = NormalizeResource(resource);
        return string.IsNullOrEmpty(normalizedResource)
            ? normalizedIssuer
            : $"{normalizedIssuer}|resource={normalizedResource}";
    }

    private async Task SaveAllClientsAsync(List<ClientRegistration> registrations)
    {
        var dir = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(registrations, JsonOptions);

        if (_useEncryption)
        {
#pragma warning disable CA1416
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_storePath, encrypted);
#pragma warning restore CA1416
        }
        else
        {
            await File.WriteAllTextAsync(_storePath, json);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(_storePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private async Task<List<ClientRegistration>> LoadAllClientsAsync()
    {
        if (!File.Exists(_storePath))
            return [];

        try
        {
            string json;
            if (_useEncryption)
            {
#pragma warning disable CA1416
                var encrypted = await File.ReadAllBytesAsync(_storePath);
                var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                json = System.Text.Encoding.UTF8.GetString(plainBytes);
#pragma warning restore CA1416
            }
            else
            {
                json = await File.ReadAllTextAsync(_storePath);
            }

            return JsonSerializer.Deserialize<List<ClientRegistration>>(json, JsonOptions) ?? [];
        }
        catch (CryptographicException)
        {
            Console.Error.WriteLine("Warning: Client registration store is corrupted or inaccessible. Starting fresh.");
            return [];
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A valid URL is required.", nameof(url));

        return new Uri(url.TrimEnd('/')).ToString().TrimEnd('/').ToLowerInvariant();
    }
}

/// <summary>
/// Represents a persisted OAuth2 client registration.
/// </summary>
public class ClientRegistration
{
    public string ClientId { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string? Resource { get; set; }
    public DateTime CreatedAt { get; set; }
}
