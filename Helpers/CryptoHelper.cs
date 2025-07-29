using LicenseServerApi.Models;
using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseServerApi.Helpers // تأكد من هذا الـ Namespace
{
    public static class CryptoHelper
    {
        private enum LicenseType { Legacy, Signed }

        public static string EncryptLicense(string clientName, string hwid, DateTime expiryDate, string password)
        {
            var license = new LicenseInfo
            {
                ClientName = clientName,
                HWID = hwid,
                ExpiryDate = expiryDate
            };

            string json = JsonConvert.SerializeObject(license);
            string encrypted = Encrypt(json, password);
            string signature = CreateHMAC(encrypted, password);

            var payload = new
            {
                Data = encrypted,
                Sign = signature
            };

            string payloadJson = JsonConvert.SerializeObject(payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
        }

        public static LicenseInfo DecryptLicense(string input, string password)
        {
            LicenseType type = DetectLicenseType(input);

            if (type == LicenseType.Signed)
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(input));
                var payload = JsonConvert.DeserializeObject<SignedLicensePayload>(json); // استخدام SignedLicensePayload

                if (payload == null || string.IsNullOrEmpty(payload.Data) || string.IsNullOrEmpty(payload.Sign))
                {
                    throw new Exception("🔒 License payload is malformed or incomplete.");
                }

                string encryptedData = payload.Data;
                string receivedSignature = payload.Sign;
                string expectedSignature = CreateHMAC(encryptedData, password);

                if (receivedSignature != expectedSignature)
                    throw new Exception("🔒 License signature is invalid or has been tampered.");

                string decryptedJson = Decrypt(encryptedData, password);
                return JsonConvert.DeserializeObject<LicenseInfo>(decryptedJson) ?? throw new InvalidOperationException("Decrypted license info cannot be null.");
            }
            else
            {
                string decryptedJson = Decrypt(input, password);
                return JsonConvert.DeserializeObject<LicenseInfo>(decryptedJson) ?? throw new InvalidOperationException("Decrypted legacy license info cannot be null.");
            }
        }

        public static string Encrypt(string plainText, string password)
        {
            byte[] salt = GenerateRandomBytes(16);
            byte[] iv = GenerateRandomBytes(16);

            using (var keyDeriver = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            {
                byte[] key = keyDeriver.GetBytes(32);

                using (var aes = System.Security.Cryptography.Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (var encryptor = aes.CreateEncryptor())
                    {
                        byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
                        byte[] cipherBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

                        byte[] combined = new byte[salt.Length + iv.Length + cipherBytes.Length];
                        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
                        Buffer.BlockCopy(iv, 0, combined, salt.Length, iv.Length);
                        Buffer.BlockCopy(cipherBytes, 0, combined, salt.Length + iv.Length, cipherBytes.Length);

                        return Convert.ToBase64String(combined);
                    }
                }
            }
        }

        public static string Decrypt(string encryptedText, string password)
        {
            byte[] combined = Convert.FromBase64String(encryptedText);

            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            Buffer.BlockCopy(combined, 0, salt, 0, 16);
            Buffer.BlockCopy(combined, 16, iv, 0, 16);

            byte[] cipherBytes = new byte[combined.Length - 32];
            Buffer.BlockCopy(combined, 32, cipherBytes, 0, cipherBytes.Length);

            using (var keyDeriver = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            {
                byte[] key = keyDeriver.GetBytes(32);

                using (var aes = System.Security.Cryptography.Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor())
                    {
                        byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                        return Encoding.UTF8.GetString(decryptedBytes);
                    }
                }
            }
        }

        private static string CreateHMAC(string data, string secret)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }

        private static LicenseType DetectLicenseType(string input)
        {
            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(input));
                if (json.Contains("\"Data\"") && json.Contains("\"Sign\""))
                    return LicenseType.Signed;
            }
            catch { }
            return LicenseType.Legacy;
        }
    }

    // كلاس مساعد لفك تشفير البيانات الموقعة
    public class SignedLicensePayload
    {
        public string? Data { get; set; }
        public string? Sign { get; set; }
    }
}