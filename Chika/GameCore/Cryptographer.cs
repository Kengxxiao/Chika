using System;
using System.Security.Cryptography;
using System.Text;

namespace Chika.GameCore
{
    public class Cryptographer
    {
        static Random rdm = new Random();
        private static readonly string sidKey = "c!SID!n";
        private static int Random()
        {
            return rdm.Next(1, 9);
        }
        public static string CalcSessionId(string sid)
        {
            var sidBytes = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(sid + sidKey));
            return BitConverter.ToString(sidBytes).Replace("-", "").ToLower();
        }
        public static string Decode(string dat)
        {
            string rst = "";
            if (dat.Length < 4)
            {
                return rst;
            }
            else
            {
                var v4 = dat.Substring(0, 4);
                var v5 = int.Parse(v4, System.Globalization.NumberStyles.AllowHexSpecifier);
                var v6 = dat.Length;
                var v7 = dat[4..v6];
                var v8 = v7.ToCharArray();
                for (int i = 0; rst.Length != v5; i++)
                {
                    if (((i + 2) & 3) == 0)
                    {
                        rst += (char)(Convert.ToInt32(v8[i]) - 10);
                    }
                }
                return rst;
            }
        }
        public static string Encode(string dat)
        {
            var v26 = dat.Length;
            var v4 = string.Format("{0:x4}", v26);
            string rst = v4;

            var charArray = dat.ToCharArray();
            for (int i = 0; i < v26; i++)
            {
                var v6 = charArray[i];
                rst += string.Format("{0,1:x}", Random());
                rst += string.Format("{0,1:x}", Random());
                var v27 = Convert.ToInt32(v6) + 10;
                rst += (char)v27;
                rst += string.Format("{0,1:x}", Random());
            }
            return rst + GenerateIvString();
        }
        public static string GenerateIvString()
        {
            var rst = "";
            for (int i = 0; i < 32; i++)
            {
                rst += string.Format("{0}", Random());
            }
            return rst;
        }
        public static string GenerateKeyString()
        {
            var rst = "";
            for (int i = 0; i < 32; i++)
            {
                rst += string.Format("{0:x}", rdm.Next(0, 0xffff));
            }
            var v8 = Encoding.ASCII.GetBytes(rst);
            return Convert.ToBase64String(v8).Substring(0, 32);
        }
    }
}
