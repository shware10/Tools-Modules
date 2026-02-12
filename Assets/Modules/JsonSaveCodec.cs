using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/*
    1.MAGIC(4)[포맷 판별] + VER(1)[버전 판별]
    2.SALT(16)[SHA256으로 PBKDF2 비밀번호 섞기] + NONCE(12)[매번 랜덤할 암호화값] + TAG(16)[변조 방지 서명]
    3.CIPHERTEXT(N)[실제 데이터]
 */
public static class JsonSaveCodec
{
    private const uint MAGIC = 0x9A89A817;
    private const byte VERSION = 1;

    private const int SALT_LEN = 16;
    private const int NONCE_LEN = 12;
    private const int TAG_LEN = 16;
    private const int KEY_LEN = 32;
    private const int PBKDF2_ITER = 100_000;

    public static byte[] Encode(string json, string password)
    {
        // json => UTF8
        byte[] plain = Encoding.UTF8.GetBytes(json);

        // LZ4 압축
        byte[] compressedPlain = CompressionUtil.Compress(plain);

        byte[] salt = RandomBytes(SALT_LEN);
        byte[] nonce = RandomBytes(NONCE_LEN);
        byte[] key = DeriveKey(password, salt);
        byte[] aad = BuildAad();

        // AES-GCM 암호화 => 키 + nonce(+ aad)를 입력으로 받아 데이터를 암호화하고 인증 태그(tag)를 생성
        byte[] cipher = 
            CryptoUtil.Encrypt(
            compressedPlain ,
            key             ,
            nonce           ,
            aad             ,
            out byte[] tag );
        

        // 포맷 저장
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(MAGIC);
        bw.Write(VERSION);
        bw.Write(salt);
        bw.Write(nonce);
        bw.Write(tag);
        bw.Write(cipher);

        return ms.ToArray();
    }

    public static string Decode(byte[] blob, string password)
    {
        using var ms = new MemoryStream(blob);
        using var br = new BinaryReader(ms);

        uint magic = br.ReadUInt32();
        byte ver = br.ReadByte();

        if (magic != MAGIC) throw new Exception("Invalid file magic.");
        if (ver != VERSION) throw new Exception($"Unsupported version: {ver}");

        byte[] salt = br.ReadBytes(SALT_LEN);
        byte[] nonce = br.ReadBytes(NONCE_LEN);
        byte[] tag = br.ReadBytes(TAG_LEN);
        byte[] cipher = br.ReadBytes((int)(ms.Length - ms.Position));

        byte[] key = DeriveKey(password, salt);
        byte[] aad = BuildAad();

        // AES-GCM 복호화
        byte[] compressedPlain = 
            CryptoUtil.Decrypt(
            cipher      ,
            key         ,
            nonce       ,
            tag         ,
            aad        );

        // LZ4 압축 해제
        byte[] plain = CompressionUtil.Decompress(compressedPlain);

        // UTF-8 => json
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>
    /// Additional Authenticated Data == Associated Data
    /// 태그 
    /// </summary>
    private static byte[] BuildAad()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(MAGIC);
        bw.Write(VERSION);
        return ms.ToArray();
    }

    /// <summary>
    /// 키 유도 함수
    /// </summary>
    /// <param name="password"></param>
    /// <param name="salt"></param>
    /// <returns></returns>
    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var kdf = new Rfc2898DeriveBytes(password, salt, PBKDF2_ITER, HashAlgorithmName.SHA256);
        return kdf.GetBytes(KEY_LEN);
    }

    /// <summary>
    /// 랜덤한숫자로 바이트 배열을 채우는 함수
    /// </summary>
    /// <param name="len"></param>
    /// <returns></returns>
    private static byte[] RandomBytes(int len)
    {
        byte[] bytes = new byte[len];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}
