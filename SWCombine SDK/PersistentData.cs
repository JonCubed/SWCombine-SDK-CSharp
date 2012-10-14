using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;

namespace SWCombine.SDK
{
    /// <summary>
    /// Represents data that needs to persist after app is closed.
    /// </summary>
    [Serializable]
    class PersistentData
    {
        #region Members 

        private const string Filename = ".data";

        #endregion

        #region Properties 

        /// <summary>
        /// Name of character that app acts on behalf of.
        /// </summary>
        public string Character { get; set; }

        /// <summary>
        /// Refresh token that can be used to obtain a new one
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// Cookies to save user from logging in again
        /// </summary>
        public string Cookie { get; set; }

        #endregion

        #region Public

        /// <summary>
        /// Saves persistent data to storage.
        /// </summary>
        public void Save()
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, this);

                // Data to protect. Convert a string to a byte[] using Encoding.UTF8.GetBytes().
                var plainText = stream.ToArray();

                // Generate additional entropy (will be used as the Initialization vector)
                var entropy = new byte[20];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(entropy);
                }

                var cipherText = ProtectedData.Protect(plainText, entropy, DataProtectionScope.CurrentUser);

                var data = new byte[entropy.Length + cipherText.Length];
                entropy.CopyTo(data, 0);
                cipherText.CopyTo(data, entropy.Length);
                File.WriteAllBytes(Path.Combine(Application.UserAppDataPath, Filename), data);
            }
        }

        /// <summary>
        /// Loads persistent data from storage.
        /// </summary>
        /// <returns></returns>
        public static PersistentData Load()
        {
            try
            {
                PersistentData data = null;
                var pathData = Path.Combine(Application.UserAppDataPath, Filename);

                if (!File.Exists(pathData))
                {
                    return data;
                }

                var cipherData = File.ReadAllBytes(pathData);

                var entropy = cipherData.Take(20).ToArray();
                var cipherText = cipherData.Skip(20).ToArray();

                var plainText = ProtectedData.Unprotect(cipherText, entropy, DataProtectionScope.CurrentUser);

                using (var stream = new MemoryStream(plainText))
                {
                    var formatter = new BinaryFormatter();
                    data = formatter.Deserialize(stream) as PersistentData;
                }

                return data;
            }
            catch (CryptographicException)
            {
                // we should be able to continue
                return null;
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion
    }
}
