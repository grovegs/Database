using System;
using System.Security.Cryptography;
using GroveGames.SecureStorage.Unity;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    internal static class DatabaseKeyStore
    {
        private const string KeyName = "GroveGames.Database.Key";
        private const int KeyLength = 32;

        public static DatabaseProtection CreateProtection(ISecureStorage storage)
        {
            if (TryRead(storage, out var stored))
            {
                return DatabaseProtection.TamperCheck(stored);
            }

            var key = new byte[KeyLength];

            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(key);
            }

            if (!storage.Write(KeyName, Convert.ToBase64String(key)))
            {
                Debug.LogError("The database key could not be stored in SecureStorage. The database is opened without tamper protection.");
                return DatabaseProtection.None;
            }

            return DatabaseProtection.TamperCheck(key, true);
        }

        private static bool TryRead(ISecureStorage storage, out byte[] key)
        {
            key = Array.Empty<byte>();

            if (!storage.TryRead(KeyName, out var stored))
            {
                return false;
            }

            try
            {
                key = Convert.FromBase64String(stored);
            }
            catch (FormatException)
            {
                return false;
            }

            return key.Length >= KeyLength;
        }
    }
}
