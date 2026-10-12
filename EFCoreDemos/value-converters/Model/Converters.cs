using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace value_converters.Model;

/// <summary>
/// Encrypts a string with AES before it reaches the database and decrypts it on the way back.
/// DEMO ONLY: the key and IV are fixed constants in source code. The fixed IV makes the encryption
/// deterministic (same plaintext -> same ciphertext), which is what lets equality queries work - and
/// is also why it leaks which rows share a value. Use a real key store, or SQL Server Always Encrypted.
/// </summary>
public sealed class EncryptedStringConverter() : ValueConverter<string, string>(
    plain => Encrypt(plain),
    cipher => Decrypt(cipher))
{
    private static readonly byte[] Key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
    private static readonly byte[] IV = Convert.FromHexString("A0A1A2A3A4A5A6A7A8A9AAABACADAEAF");

    public static string Encrypt(string plain)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        return Convert.ToBase64String(aes.EncryptCbc(Encoding.UTF8.GetBytes(plain), IV));
    }

    public static string Decrypt(string cipher)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        return Encoding.UTF8.GetString(aes.DecryptCbc(Convert.FromBase64String(cipher), IV));
    }
}

/// <summary>Serializes a list of strings into one JSON text column.</summary>
public sealed class JsonListConverter() : ValueConverter<List<string>, string>(
    list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
    json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>());

/// <summary>
/// Without this, EF compares the list by reference: tags.Add("x") on a tracked entity would never be saved.
/// A mutable type behind a converter needs a comparer that looks at the contents.
/// </summary>
public sealed class StringListComparer() : ValueComparer<List<string>>(
    (a, b) => a!.SequenceEqual(b!),
    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
    list => list.ToList());
