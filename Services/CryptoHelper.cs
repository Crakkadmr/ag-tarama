using System.Security.Cryptography;

namespace AgTarama.Services;

public static class CryptoHelper
{
    // Format: [16 IV | AES ciphertext | 32 HMAC]
    public static byte[] EncryptAesHmac(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(data, 0, data.Length);

        var payload = new byte[16 + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, 16);
        Buffer.BlockCopy(cipher, 0, payload, 16, cipher.Length);

        var hmac = HMACSHA256.HashData(key, payload);
        return payload.Concat(hmac).ToArray();
    }

    public static byte[]? DecryptAesHmac(byte[] data, byte[] key)
    {
        if (data.Length < 16 + 32) return null;

        var payloadLen = data.Length - 32;
        var storedHmac = data[payloadLen..];
        var payload = data[..payloadLen];

        var expectedHmac = HMACSHA256.HashData(key, payload);
        if (!CryptographicOperations.FixedTimeEquals(storedHmac, expectedHmac))
            return null;

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = payload[..16];
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(payload, 16, payload.Length - 16);
    }
}
