using System.Security.Cryptography;

public static class CryptoUtil
{
    public static byte[] Encrypt(
        byte[] plain,
        byte[] key,
        byte[] nonce,
        byte[] aad,
        out byte[] tag)
    {
        byte[] cipher = new byte[plain.Length];
        tag = new byte[16];

        using var gcm = new AesGcm(key);
        gcm.Encrypt(nonce, plain, cipher, tag, aad);

        return cipher;
    }

    public static byte[] Decrypt(
        byte[] cipher,
        byte[] key,
        byte[] nonce,
        byte[] tag,
        byte[] aad)
    {
        byte[] plain = new byte[cipher.Length];

        using var gcm = new AesGcm(key);
        gcm.Decrypt(nonce, cipher, tag, plain, aad);

        return plain;
    }
}
