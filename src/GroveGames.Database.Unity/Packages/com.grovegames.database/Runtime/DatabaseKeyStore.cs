using System;
using System.Security.Cryptography;
using GroveGames.SecureStorage;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    internal static class DatabaseKeyStore
    {
        private const string KeyName = "GroveGames.Database.Key";
        private const int KeyLength = 32;

        public static DatabaseProtection CreateProtection(ISecureStorage storage)
        {
            var result = Read(storage, out var stored);

            if (result == SecureStorageReadResult.Failed)
            {
                result = Read(storage, out stored);
            }

            if (result == SecureStorageReadResult.Found)
            {
                return DatabaseProtection.TamperCheck(stored);
            }

            if (result == SecureStorageReadResult.Failed)
            {
                Debug.LogWarning("The database key is stored but could not be read. A new key replaces it, and the existing save files are trusted and signed with it.");
            }

            var key = new byte[KeyLength];

            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(key);
            }

            if (!storage.Write(KeyName, Convert.ToBase64String(key)))
            {
                Debug.LogError("The database key could not be stored in SecureStorage. This session trusts the existing save files and signs them with a key that is not kept.");
            }

            return DatabaseProtection.TamperCheck(key, true);
        }

        private static SecureStorageReadResult Read(ISecureStorage storage, out byte[] key)
        {
            key = Array.Empty<byte>();
            var result = storage.Read(KeyName, out var stored);

            if (result != SecureStorageReadResult.Found)
            {
                return result;
            }

            try
            {
                key = Convert.FromBase64String(stored);
            }
            catch (FormatException)
            {
                return SecureStorageReadResult.Failed;
            }

            return key.Length >= KeyLength ? SecureStorageReadResult.Found : SecureStorageReadResult.Failed;
        }
    }
}
