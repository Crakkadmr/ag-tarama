using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AgTarama.Services.Ai;

public static class AiKeyVault
{
    private static readonly string VaultFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgTarama",
        "ai.vault");

    public static void EnsureDefaultKey()
    {
        if (HasKey()) return;

        var key = AiDefaultKey.Get();
        if (string.IsNullOrWhiteSpace(key)) return;
        Save(key);
    }

    public static void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key bos olamaz.", nameof(apiKey));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VaultFile)!);

            var machineKey = SHA256.HashData(Encoding.UTF8.GetBytes(LicenseService.GetMachineId()));
            var encrypted = CryptoHelper.EncryptAesHmac(Encoding.UTF8.GetBytes(apiKey.Trim()), machineKey);
            var protectedBytes = ProtectedData.Protect(encrypted, null, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(VaultFile, protectedBytes);
        }
        catch (Exception ex)
        {
            LogService.Hata("AiKeyVault.Save", ex);
            throw;
        }
    }

    public static string? Load()
    {
        try
        {
            if (!File.Exists(VaultFile))
                return null;

            var protectedBytes = File.ReadAllBytes(VaultFile);
            if (protectedBytes.Length == 0)
                return null;

            var machineKey = SHA256.HashData(Encoding.UTF8.GetBytes(LicenseService.GetMachineId()));
            var encrypted = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var plain = CryptoHelper.DecryptAesHmac(encrypted, machineKey);
            if (plain is null)
                return null;

            var value = Encoding.UTF8.GetString(plain).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex)
        {
            LogService.Hata("AiKeyVault.Load", ex);
            return null;
        }
    }

    public static bool HasKey() => !string.IsNullOrWhiteSpace(Load());

    public static void Clear()
    {
        try
        {
            if (File.Exists(VaultFile))
                File.Delete(VaultFile);
        }
        catch (Exception ex)
        {
            LogService.Hata("AiKeyVault.Clear", ex);
        }
    }
}
