using Chika.Model;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Chika.GameCore
{
    public class GameClient
    {
        private string UDID = string.Empty;
        private static readonly string BASE_URL = "https://le1-prod-all-gs-gzlj.bilibiligame.net";

        private RestClient _gameClient = new RestClient(BASE_URL);

        private string _lastRequestId = "";
        private string _sid = "";
        private string _viewerId = "";
        private string _shortUdid = "";

        private int _resultCode = 0;

        private Dictionary<string, object> _savedAccount;
        private Random rdm = new Random();

        public bool disable = false;
        public GameClient(string gameVersion, Dictionary<string, object> account, string udid) : this(gameVersion)
        {
            UDID = udid;
            GameLogin(account);
            if (!disable)
            {
                GameStart();
                CheckAgreement();
            }
        }

        public GameClient(string gameVersion)
        {
            //初始化游戏参数
            _gameClient.UserAgent = "priconne/14 CFNetwork/1197 Darwin/20.0.0";
            _gameClient.AddDefaultHeaders(new()
            {
                { "Content-Type", "application/x-www-form-urlencoded" },
                { "APP-VER", gameVersion },
                { "RES-KEY", "fa59108a953acd69530cff2755f367d4" } //resKey
            });
            _gameClient.ConfigureWebRequest(r => r.KeepAlive = true);
            RestartGameClient();
            InitServerHeader();
        }

        private void RestartGameClient()
        {
            do
            {
                var statusResp = DefaultRequest("/source_ini/get_maintenance_status?format=json", new()
                {
                    { "viewer_id", 0 }
                }, false);
                _resultCode = statusResp.data_headers.result_code;
                if (_resultCode == 1)
                {
                    Console.WriteLine("get_maintenance_status result_code {0}", _resultCode);
                    _gameClient.AddDefaultHeaders(new()
                    {
                        { "MANIFEST-VER", (string)statusResp.data.manifest_ver },
                        { "RES-VER", (string)statusResp.data.res_ver },
                        { "EXCEL-VER", (string)statusResp.data.excel_ver }
                    });
                }
                else
                {
                    Console.WriteLine("get_maintenance_status != 1 \n{0}", JsonConvert.SerializeObject(statusResp));
                    Thread.Sleep(60000);
                }
                
            }
            while (_resultCode != 1);
        }

        private SHA1 sha1 = SHA1.Create();
        private byte[] AddSignatureHeader(RestRequest req, string query, Dictionary<string, object> data)
        {
            if (!string.IsNullOrEmpty(_lastRequestId))
                req.AddHeader("REQUEST-ID", _lastRequestId);
            if (!string.IsNullOrEmpty(_sid))
                req.AddHeader("SID", Cryptographer.CalcSessionId(_sid));
            if (!string.IsNullOrEmpty(_viewerId))
                data["viewer_id"] = Convert.ToBase64String(CryptAES.EncryptRJ256Api(Encoding.UTF8.GetBytes(_viewerId)));
            if (!string.IsNullOrEmpty(_shortUdid))
                req.AddHeader("SHORT-UDID", Cryptographer.Encode(_shortUdid));
            req.AddHeader("UDID", Cryptographer.Encode(UDID));
            byte[] encryptedMsgpack = CryptAES.EncryptRJ256Api(MessagePackSerializer.Serialize(data));
            var param1 = UDID + new Uri(BASE_URL + query).AbsolutePath + Convert.ToBase64String(encryptedMsgpack).Trim() + _viewerId;
            var sb = new StringBuilder();
            foreach (var b in sha1.ComputeHash(Encoding.UTF8.GetBytes(param1)))
            {
                sb.Append(b.ToString("x2"));
            }
            req.AddHeader("PARAM", sb.ToString().ToLower());
            sb.Clear();
            return encryptedMsgpack;
        }

        private void InitServerHeader()
        {
            _gameClient.AddDefaultHeaders(new()
            {
                { "BATTLE-LOGIC-VERSION", "3" },
                { "BUNDLE-VER", "" },
                { "CHANNEL-ID", "1000" },
                { "DEVICE", "1" },
                { "DEVICE-ID", UDID.ToUpper() },
                { "DEVICE-NAME", "iPad8,9" },
                { "GRAPHICS-DEVICE-NAME", "Apple A12Z GPU" },
                { "IP-ADDRESS", "1.1.1.1" },
                { "KEYCHAIN", "" },
                { "LOCALE", "CN" },
                { "PLATFORM", "1" }, 
                { "PLATFORM-ID", "1" },
                { "PLATFORM-OS-VERSION", "iOS 14.3" },
                { "REGION-CODE", "" },
                { "X-Unity-Version", "2017.4.37c2" },

                { "SIGN", "" } //libNetHTProtect
            });
        }
        public dynamic DefaultRequest(string url, Dictionary<string, object> param, bool encrypted)
        {
            var req = new RestRequest(url, Method.POST);
            if (!encrypted)
            {
                req.AddParameter("", JsonConvert.SerializeObject(param), ParameterType.RequestBody);
                var resp = _gameClient.Execute(req);
                return JsonConvert.DeserializeObject<dynamic>(resp.Content);
            }
            else
            {
                var encResp = AddSignatureHeader(req, url, param);
                req.AddParameter("", encResp, ParameterType.RequestBody);
                var exeReq = _gameClient.Execute(req);
                if (!exeReq.IsSuccessful)
                {
                    //nginx error
                    Thread.Sleep(500);
                    //重新请求
                    return DefaultRequest(url, param, encrypted);
                }
                var resp = MessagePackSerializer.Deserialize<dynamic>(CryptAES.DecryptRJ256Api(exeReq.Content));
                if (resp["data_headers"]["result_code"] != 1 && url == "/tool/sdk_login")
                {
                    disable = true;
                    //risk
                    return null;
                }
                if (resp["data_headers"]["result_code"] != 1)
                {
                    if (resp["data_headers"]["result_code"] == 3600)
                    {
                        Console.WriteLine("{0} result_code 3600", param["target_viewer_id"]);
                        return null;
                    }
                    Console.WriteLine("{0} result_code != 1 \n{1} 准备重启GameClient", url, JsonConvert.SerializeObject(resp));
                    _resultCode = resp["data_headers"]["result_code"];
                    while (_resultCode != 1)
                    {
                        Console.WriteLine("重启GameClient中...");
                        Thread.Sleep(2000);
                        RestartGameClient();
                    }
                    GameLogin();
                    GameStart();
                    CheckAgreement();
                    return DefaultRequest(url, param, encrypted);
                }
                if (resp != null && resp["data_headers"] != null)
                {
                    if (resp["data_headers"]["viewer_id"] != null && !string.IsNullOrEmpty(resp["data_headers"]["viewer_id"].ToString()))
                        _viewerId = resp["data_headers"]["viewer_id"].ToString();
                    if (!string.IsNullOrEmpty(resp["data_headers"]["sid"]))
                        _sid = resp["data_headers"]["sid"];
                    if (!string.IsNullOrEmpty(resp["data_headers"]["request_id"]))
                        _lastRequestId = resp["data_headers"]["request_id"];
                    if (!string.IsNullOrEmpty(resp["data_headers"]["short_udid"].ToString()))
                        _shortUdid = resp["data_headers"]["short_udid"].ToString();
                }
                return resp;
            }
        }
        public void GameLogin(Dictionary<string, object> accountRequest = null)
        {
            if (_savedAccount == null && accountRequest != null)
                _savedAccount = accountRequest;
            var viewerId = CryptAES.DecryptRJ256Api((string)_savedAccount["viewer_id"]);
            _savedAccount["viewer_id"] = Encoding.UTF8.GetString(CryptAES.EncryptRJ256Api(viewerId));

            _savedAccount["captcha_code"] = "";
            _savedAccount["captcha_type"] = "";
            _savedAccount["challenge"] = "";
            _savedAccount["gt_user_id"] = "";
            _savedAccount["image_token"] = "";
            _savedAccount["seccode"] = "";
            _savedAccount["validate"] = "";

            var test = DefaultRequest("/tool/sdk_login", _savedAccount, true);
            if (test == null || _shortUdid.ToLower() == "false")
            {
                Console.WriteLine("风控，取消该账号{0}\n{1}", Encoding.UTF8.GetString(viewerId), JsonConvert.SerializeObject(test));
                disable = true;
            }
        }
        public void CheckAgreement()
        {
            DefaultRequest("/check/check_agreement", new(), true);
            Console.WriteLine("check_agreement");
        }
        public void GameStart()
        {
            DefaultRequest("/check/game_start", new()
            {
                { "app_type", 0 },
                { "campaign_data", "" },
                { "campaign_user", rdm.Next(0, 100000) * 2 }
            }, true);
            Console.WriteLine("game_start");
            Console.WriteLine("viewer_id {0}", _viewerId);
            Console.WriteLine("request_id {0}", _lastRequestId);
            Console.WriteLine("sid {0}", _sid);
        }

        public Profile GetProfile(long viewerId)
        {
            var profile = DefaultRequest("/profile/get_profile", new()
            {
                { "target_viewer_id", viewerId }
            }, true);
            var himariProfile = new Profile();
            if (profile != null)
            {
                himariProfile.HasValue = true;

                himariProfile.Arena_rank = profile["data"]["user_info"]["arena_rank"];
                himariProfile.Arena_group = profile["data"]["user_info"]["arena_group"];
                himariProfile.Arena_time = profile["data"]["user_info"]["arena_time"];

                himariProfile.Grand_arena_rank = profile["data"]["user_info"]["grand_arena_rank"];
                himariProfile.Grand_arena_group = profile["data"]["user_info"]["grand_arena_group"];
                himariProfile.Grand_arena_time = profile["data"]["user_info"]["grand_arena_time"];

                himariProfile.SignVerify = profile["data"]["user_info"]["user_comment"];
            }
            return himariProfile;
        }
    }
}
