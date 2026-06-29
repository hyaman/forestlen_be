using System;
using System.Security.Cryptography;
using System.Text;
using ForestIQ.Domain;
using ForestIQ.Domain.Interface.Licensing;

namespace ForestIQ.Service.Licensing
{
    public class RsaHelper : IRsaHelper
    {
        public string SignData(string data)
        {
            var privateKey = Runtime.Licensing.RsaPrivateKey;
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                throw new InvalidOperationException("RSA Private Key is not configured.");
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signatureBytes);
        }

        public bool VerifyData(string data, string signature)
        {
            var publicKey = Runtime.Licensing.RsaPublicKey;
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                throw new InvalidOperationException("RSA Public Key is not configured.");
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(publicKey);

                var dataBytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = Convert.FromBase64String(signature);

                return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }
    }
}
