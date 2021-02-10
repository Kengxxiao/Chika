using Chika.RSAMiddleware;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Chika.GameCore
{
    public class BGameSDK
    {
        private const string APPKEY = "fe8aac4e02f845b8ad67c427d48bfaf1";
        private RestClient biliSdkClient = new("https://line1-sdk-center-login-sh.biligame.net");
        private RestClient ydaaaClient = new("http://api.ydaaa.com");
        private static MD5 md5 = MD5.Create();

        public static string ydaaaUser = string.Empty;
        public static string ydaaaKey = string.Empty;

        private static readonly Dictionary<string, object> bgsdkParams = new()
        {
            { "access_key", "" },
            { "apk_sign", "4502a02a00395dec05a4134ad593224d" }, //2.4.10
            { "app_id", 1370 },
            { "brand", "Chika"},
            { "c", 1 },
            { "captcha_type", 1},
            { "challenge", "" },
            { "channel_id", 1 },
            { "client_timestamp", 0 },
            { "cur_buvid", "XZ" },
            { "domain", "line1-sdk-center-login-sh.biligame.net" },
            { "domain_switch_count", 0 },
            { "dp", "1920*1080" },
            { "fingerprint", "" },
            { "game_id", 1370 },
            { "gt_user_id", "" },
            { "imei", "000000000000000" },
            { "isRoot", 0 },
            { "mac", "" },
            { "merchant_id", 1 },
            { "model", "ChikaBackend" },
            { "net", 4 },
            { "oaid", "" },
            { "old_buvid", "XZ" },
            { "operators", 1 },
            { "original_domain", "" },
            { "pf_ver", "10.0.0" },
            { "platform_type", 3 },
            { "pwd", "" },
            { "sdk_log_type", 1 },
            { "sdk_type", 1 },
            { "sdk_ver", "3.4.2" },
            { "seccode", "" },
            { "server_id", 1592 },
            { "support_abis", "x86,armeabi-v7a,armeabi" },
            { "timestamp", 0 },
            { "udid", "Jxcv" },
            { "uid", "" },
            { "user_id", ""},
            { "validate", "" },
            { "ver", "2.4.10" },
            { "version", 1 },
            { "version_code", 90 }
        };
        public BGameSDK()
        {
            biliSdkClient.UserAgent = "Mozilla/5.0 BSGameSDK";
            biliSdkClient.AddDefaultHeaders(new()
            {
                { "Content-Type", "application/x-www-form-urlencoded" }
            });
            biliSdkClient.ConfigureWebRequest(x => x.KeepAlive = true);
        }
        private string BuildSdkReqBody(string access_key, string pwd = "", string username = "", string validate = "", string challenge = "", string gt_user_id = "")
        {
            var ts = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
            var dic = new Dictionary<string, object>();
            foreach (var c in bgsdkParams)
            {
                dic.Add(c.Key, c.Value);
            }
            dic["client_timestamp"] = ts;
            dic["timestamp"] = ts;
            dic["access_key"] = access_key;
            if (string.IsNullOrEmpty(pwd))
            {
                dic.Remove("pwd");
                dic.Remove("user_id");
            }
            else
            {
                dic["pwd"] = pwd;
                dic["user_id"] = username;
            }
            if (string.IsNullOrEmpty(validate))
            {
                dic.Remove("captcha_type");
                dic.Remove("validate");
                dic.Remove("seccode");
                dic.Remove("challenge");
                dic.Remove("gt_user_id");
            }
            else
            {
                dic["validate"] = validate;
                dic["seccode"] = $"{validate}|jordan";
                dic["challenge"] = challenge;
                dic["gt_user_id"] = gt_user_id;
            }
            //sign
            var sb = new StringBuilder();
            foreach (var c in dic)
            {
                sb.Append(c.Value);
            }
            sb.Append(APPKEY);
            dic.Add("sign", BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").ToLower());
            sb.Clear();
            dic["pwd"] = HttpUtility.UrlEncode(Encoding.UTF8.GetBytes(pwd));
            foreach (var c in dic)
            {
                sb.Append($"{c.Key}={c.Value}&");
            }
            return sb.ToString()[..^1];
        }
        private async Task<Dictionary<string, object>> Request(string url, string data)
        {
            var req = new RestRequest(url, Method.POST);
            req.AddParameter("", data, ParameterType.RequestBody);
            var ret = await biliSdkClient.ExecuteAsync(req);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(ret.Content);
        }
        private async Task<Dictionary<string, object>> RequestClientRsa()
        {
            return await Request("/api/client/rsa", BuildSdkReqBody(""));
        }
        private async Task<Dictionary<string, object>> RequestClientLogin(string username, string pwd, string validate = "", string challenge = "", string gt_user_id = "")
        {
            return await Request("/api/client/login", BuildSdkReqBody("", pwd, username, validate, challenge, gt_user_id));
        }
        private async Task<Dictionary<string, object>> RequestClientStartCaptcha()
        {
            return await Request("/api/client/start_captcha", BuildSdkReqBody(""));
        }
        public async Task<Dictionary<string, object>> RequestBiliLogin(string username, string pwd)
        {
            var ret = await RequestClientRsa();
            var rsa = new RSACryptoService("", ret["rsa_key"].ToString().Replace("-----BEGIN PUBLIC KEY-----\n", "").Replace("\n-----END PUBLIC KEY-----", ""));
            var ret2 = await RequestClientLogin(username, rsa.Encrypt(ret["hash"].ToString() + pwd));
            while (ret2.ContainsKey("need_captch") && !ret2.ContainsKey("access_key"))
            {
                string challenge;
                string gt_user_id;
                string validate;
                while (true)
                {
                    var captcha = await RequestClientStartCaptcha();
                    var req = new RestRequest($"/start_handle?username={ydaaaUser}&appkey={ydaaaKey}&gt={captcha["gt"]}&challenge={captcha["challenge"]}&handle_method=three_on&referer={HttpUtility.UrlEncode(Encoding.UTF8.GetBytes($"https://game.bilibili.com/sdk/geetest/?captcha_type=1&challenge={captcha["challenge"]}&gt={captcha["gt"]}&userid={captcha["gt_user_id"]}&gs=1"))}&dev_username=Kengxxiao", Method.GET);
                    var ret3 = JsonConvert.DeserializeObject<dynamic>((await ydaaaClient.ExecuteAsync(req)).Content);
                    var retCode = (int)ret3["code"];
                    if (retCode == 200)
                    {
                        validate = (string)ret3["data"]["validate"];
                        challenge = (string)captcha["challenge"];
                        gt_user_id = (string)captcha["gt_user_id"];
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"geetest识别失败，错误信息{ret3["msg"]}");
                    }
                }
                ret = await RequestClientRsa();
                rsa = new RSACryptoService("", ret["rsa_key"].ToString().Replace("-----BEGIN PUBLIC KEY-----\n", "").Replace("\n-----END PUBLIC KEY-----", ""));
                ret2 = await RequestClientLogin(username, rsa.Encrypt(ret["hash"].ToString() + pwd), validate, challenge, gt_user_id);
            }
            return ret2;
        }
    }
}
