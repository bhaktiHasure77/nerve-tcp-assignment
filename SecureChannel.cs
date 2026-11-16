using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NerveTcp;

/// <summary>
/// Encrypts/decrypts messages with AES-256-GCM (key derived from a shared passphrase via PBKDF2)
/// and frames them on the stream as: [4-byte big-endian length][12-byte nonce][16-byte tag][ciphertext].
/// </summary>
public sealed class SecureChannel
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MaxFrame = 1_000_000;

    private readonly byte[] _key;

    public SecureChannel(string passphrase, string salt)
    {
        _key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            Encoding.UTF8.GetBytes(salt),
            200_000,
            HashAlgorithmName.SHA256,
            32);
    }

    public async Task SendAsync(Stream stream, string text, CancellationToken ct = default)
    {
        byte[] frame = Encrypt(text);
        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>Returns the decrypted message, or null if the peer closed the connection.
    /// Throws CryptographicException if the data can't be decrypted (wrong passphrase / tampering).</summary>
    public async Task<string?> ReceiveAsync(Stream stream, CancellationToken ct = default)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct)) return null;

        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < NonceSize + TagSize || length > MaxFrame)
            throw new InvalidDataException("Invalid frame length.");

        var body = new byte[length];
        if (!await ReadExactAsync(stream, body, ct)) return null;

        return Decrypt(body);
    }

    private byte[] Encrypt(string text)
    {
        byte[] plain = Encoding.UTF8.GetBytes(text);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);

        int bodyLength = NonceSize + TagSize + cipher.Length;
        byte[] frame = new byte[4 + bodyLength];
        BinaryPrimitives.WriteInt32BigEndian(frame, bodyLength);
        nonce.CopyTo(frame, 4);
        tag.CopyTo(frame, 4 + NonceSize);
        cipher.CopyTo(frame, 4 + NonceSize + TagSize);
        return frame;
    }

    private string Decrypt(byte[] body)
    {
        ReadOnlySpan<byte> nonce = body.AsSpan(0, NonceSize);
        ReadOnlySpan<byte> tag = body.AsSpan(NonceSize, TagSize);
        ReadOnlySpan<byte> cipher = body.AsSpan(NonceSize + TagSize);
        byte[] plain = new byte[cipher.Length];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (n == 0) return false;
            offset += n;
        }
        return true;
    }
}
