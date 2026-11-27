using System;
using System.IO;
using System.Security.Cryptography;
using GroveGames.SecureStorage;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    internal static class DatabaseKeyStore
    {
        private const string KeyName = "GroveGames.Database.Key";
        private const string TrustMarkerName = ".trust";
        private const int KeyLength = 32;

        // While the marker exists every launch trusts the existing files and signs them all again, so a session that
        // re-signed with a key it could not keep, or was killed while re-signing, is repaired by the next one.
        public static DatabaseProtection CreateProtection(ISecureStorage storage, string directory, out bool isKeyStored)
        {
            var result = Read(storage, out var stored);

            if (result == SecureStorageReadResult.Failed)
            {
                result = Read(storage, out stored);
            }

            if (result == SecureStorageReadResult.Found)
            {
                isKeyStored = true;
                return DatabaseProtection.TamperCheck(stored, HasTrustMarker(directory));
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

            WriteTrustMarker(directory);
            isKeyStored = storage.Write(KeyName, Convert.ToBase64String(key));

            if (!isKeyStored)
            {
                Debug.LogError("The database key could not be stored in SecureStorage. This session signs the save files with a key that is not kept, and the next launch trusts and signs them again.");
            }

            return DatabaseProtection.TamperCheck(key, true);
        }

        public static void DeleteTrustMarker(string directory)
        {
            try
            {
                File.Delete(GetTrustMarkerPath(directory));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"Failed to delete the database trust marker: {exception.Message}");
            }
        }

        private static bool HasTrustMarker(string directory)
        {
            return File.Exists(GetTrustMarkerPath(directory));
        }

        private static void WriteTrustMarker(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);

                using (var stream = new FileStream(GetTrustMarkerPath(directory), FileMode.Create, FileAccess.Write))
                {
                    Storage.DiskSync.Flush(stream);
                }

                Storage.DiskSync.FlushDirectory(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"Failed to write the database trust marker: {exception.Message}");
            }
        }

        private static string GetTrustMarkerPath(string directory)
        {
            return Path.Combine(directory, TrustMarkerName);
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
