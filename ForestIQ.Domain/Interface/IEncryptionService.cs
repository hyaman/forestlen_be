namespace ForestIQ.Domain.Interface
{
    public interface IEncryptionService
    {
        string Protect(string plainText);
        string Unprotect(string cipherText);
    }
}
