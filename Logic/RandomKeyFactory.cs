using System;
using System.Security.Cryptography;
using System.Text;

namespace ShoppingListServer.Logic
{
    public class RandomKeyFactory
    {
        internal static readonly char[] chars =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

        /// <summary>
        /// Generate a cryptographically random string of the given size.
        /// https://stackoverflow.com/a/1344255
        /// </summary>
        /// <param name="size">Length of the generated string</param>
        /// <returns></returns>
        public static string GetUniqueKey(int size)
        {
            byte[] data = new byte[4 * size];
            using (var crypto = RandomNumberGenerator.Create())
            {
                crypto.GetBytes(data);
            }
            StringBuilder result = new StringBuilder(size);
            for (int i = 0; i < size; i++)
            {
                var rnd = BitConverter.ToUInt32(data, i * 4);
                var idx = rnd % chars.Length;

                result.Append(chars[idx]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Generate a cryptographically random string of the given size that consists of only numbers and characters.
        /// https://stackoverflow.com/a/1344255
        /// </summary>
        /// <param name="size">Length of the generated string</param>
        /// <returns></returns>
        public static string GetUniqueKeyOriginal_BIASED(int size)
        {
            char[] chars =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            byte[] data = new byte[size];
            RandomNumberGenerator.Fill(data);
            
            StringBuilder result = new StringBuilder(size);
            foreach (byte b in data)
            {
                result.Append(chars[b % (chars.Length)]);
            }
            return result.ToString();
        }
    }
}
