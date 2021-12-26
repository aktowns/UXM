using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;
using System;
using System.IO;

namespace UXM
{
    /// <summary>
    /// These RSA functions are copy-pasted straight from BinderTool. Thank you Atvaark!
    /// </summary>
    internal static class CryptographyUtility
    {
        /// <summary>
        ///     Decrypts a file with a provided decryption key.
        /// </summary>
        /// <param name="filePath">An encrypted file</param>
        /// <param name="key">The RSA key in PEM format</param>
        /// <exception cref="ArgumentNullException">When the argument filePath is null</exception>
        /// <exception cref="ArgumentNullException">When the argument keyPath is null</exception>
        /// <returns>A memory stream with the decrypted file</returns>
        public static MemoryStream DecryptRsa(string filePath, string key)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var keyParameter = GetKeyOrDefault(key);
            var engine = new RsaEngine();
            engine.Init(false, keyParameter);

            var outputStream = new MemoryStream();
            using (var inputStream = File.OpenRead(filePath))
            {

                var inputBlockSize = engine.GetInputBlockSize();
                var outputBlockSize = engine.GetOutputBlockSize();
                var inputBlock = new byte[inputBlockSize];
                while (inputStream.Read(inputBlock, 0, inputBlock.Length) > 0)
                {
                    var outputBlock = engine.ProcessBlock(inputBlock, 0, inputBlockSize);

                    var requiredPadding = outputBlockSize - outputBlock.Length;
                    if (requiredPadding > 0)
                    {
                        var paddedOutputBlock = new byte[outputBlockSize];
                        outputBlock.CopyTo(paddedOutputBlock, requiredPadding);
                        outputBlock = paddedOutputBlock;
                    }

                    outputStream.Write(outputBlock, 0, outputBlock.Length);
                }
            }

            outputStream.Seek(0, SeekOrigin.Begin);
            return outputStream;
        }

        private static AsymmetricKeyParameter GetKeyOrDefault(string key)
        {
            try
            {
                var pemReader = new PemReader(new StringReader(key));
                return (AsymmetricKeyParameter)pemReader.ReadObject();
            }
            catch
            {
                return null;
            }
        }
    }
}
