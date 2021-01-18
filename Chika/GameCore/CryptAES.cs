using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Chika.GameCore
{
    public class CryptAES
    {
        public const string IVString = "ha4nBYA2APUD6Uv1"; // 0x0
        public static byte[] EncryptRJ256Api(byte[] toEncryptData)
        {
            var v3 = Encoding.UTF8.GetBytes(Cryptographer.GenerateKeyString());
            RijndaelManaged ri = new RijndaelManaged()
            {
                KeySize = 256,
                BlockSize = 128,
                Key = v3,
                IV = Encoding.UTF8.GetBytes(IVString) //cn->IVString tw->udid[0..16]
            };
            var cryp = ri.CreateEncryptor();
            var enc = cryp.TransformFinalBlock(toEncryptData, 0, toEncryptData.Length).ToList();
            enc.AddRange(v3);
            return enc.ToArray();
        }
        public static byte[] DecryptRJ256ApiInternal(byte[] data)
        {
            RijndaelManaged ri = new RijndaelManaged()
            {
                KeySize = 256,
                BlockSize = 128,
                Key = data[^32..],
                IV = Encoding.UTF8.GetBytes(IVString)
            };
            var b = ri.CreateDecryptor();
            return b.TransformFinalBlock(data, 0, data.Length - 32);
        }
        public static byte[] DecryptRJ256Api(string sEncryptedString)
        {
            var data = Convert.FromBase64String(sEncryptedString);
            return DecryptRJ256ApiInternal(data);
        }
    }
}
