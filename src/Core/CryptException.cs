namespace RcloneCrypt.Core;

/// <summary>
/// Raised for any recoverable error while encrypting or decrypting (bad password,
/// corrupt input, malformed encoding, ...).
/// </summary>
public sealed class CryptException : Exception
{
    public CryptException(string message) : base(message) { }
    public CryptException(string message, Exception inner) : base(message, inner) { }
}
