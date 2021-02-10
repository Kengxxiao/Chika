using Chika.GameCore;
using Chika.Model;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using static Chika.Model.ChikaModel;
using static Chika.Model.ChikaModel.ChikaGetLogResponse;

namespace Chika.Controllers
{
    [Route("{controller}")]
    [ApiController]
    public class AccountController : Controller
    {
        private LiteDatabase _ldb;
        public AccountController(LiteDatabase db)
        {
            _ldb = db;
        }
        [HttpPut("sckey/{qq}/{sckey}")]
        public IActionResult SetSCKEY(long qq, string sckey)
        {
            var wechat = _ldb.GetCollection<BsonDocument>("wechat_sckey");
            var pp = wechat.FindById(qq);
            if (pp != null)
            {
                pp["sckey"] = sckey;
                wechat.Update(pp);
                return Ok();
            }
            wechat.Insert(new BsonDocument
            {
                {"_id", qq },
                {"sckey", sckey }
            });
            return Ok();
        }
        [HttpGet("profile_test/{vid}")]
        public IActionResult GetTestProfile(long vid)
        {
            return new ObjectResult(GameAccountPool.standaloneGameClientInstance.DefaultRequest("/profile/get_profile", new()
            {
                { "target_viewer_id", vid }
            }, true));
        }
        [HttpGet("profile/{qq}")]
        public Profile GetProfile(long qq)
        {
            var gameUser = _ldb.GetCollection<BsonDocument>("game_user");
            var pp = gameUser.FindById(qq);
            if (pp != null)
            {
                var pr = GameAccountPool.standaloneGameClientInstance.GetProfile(pp["viewer_id"].AsInt64);
                return pr;
            }
            return new Profile
            {
                HasValue = false
            };
        }
        [HttpDelete("remove/{qq}/{groupId}")]
        public IActionResult Remove(long qq, long groupId)
        {
            var gameUser = _ldb.GetCollection<BsonDocument>("game_user");
            var user = gameUser.FindById(qq);
            if (user == null)
                return Ok();
            user["groups"].AsArray.Remove(groupId);
            if (!user["groups"].AsArray.Any())
            {
                gameUser.Delete(qq);
                return Ok();
            }
            gameUser.Update(user);
            return Ok();
        }
        [HttpGet("logs/{qq}")]
        public IActionResult GetLog(long qq)
        {
            var gameUser = _ldb.GetCollection<BsonDocument>("game_user");
            var battleLog = _ldb.GetCollection<BsonDocument>("battle_log");
            var user = gameUser.FindById(qq);
            if (user == null)
                return new ObjectResult(new ChikaGetLogResponse
                {
                    Ret = 2
                });
            long viewerId = user["viewer_id"].AsInt64;
            var tmp = battleLog.Find(x => x["viewer_id"].AsInt64 == viewerId).OrderByDescending(x => x["time"].AsDateTime).Take(5);
            var updd = new List<ChikaLog>();
            foreach (var pa in tmp)
            {
                updd.Add(new ChikaLog
                {
                    Time = pa["time"].AsDateTime,
                    A_before = pa["a_before"].AsInt32,
                    A_after = pa["a_after"].AsInt32,
                    Ga_before = pa["ga_before"].AsInt32,
                    Ga_after = pa["ga_after"].AsInt32
                });
            }
            return new ObjectResult(new ChikaGetLogResponse
            {
                Ret = 0,
                ViewerId = viewerId,
                Logs = updd
            });
        }
        [HttpPost("update")]
        public Profile Update(ChikaUpdateQQRequest qqReq)
        {
            if (GameAccountPool.standaloneGameClientInstance == null)
                return new Profile()
                {
                    NoClient = true
                };
            var gameUserVerify = _ldb.GetCollection<BsonDocument>("game_user_verify");
            if (gameUserVerify.FindById(qqReq.ViewerId) == null)
            {
                var verify = Guid.NewGuid().ToString("N")[0..6];
                gameUserVerify.Insert(new BsonDocument
                    {
                        {"_id", qqReq.ViewerId },
                        {"verify", "Chika" + verify }
                    });
                return new Profile()
                {
                    SignVerify = "Chika" + verify
                };
            }
            var pfile = GameAccountPool.standaloneGameClientInstance.GetProfile(qqReq.ViewerId);
            if (pfile.HasValue)
            {
                var loadedVerify = gameUserVerify.FindById(qqReq.ViewerId);
                if (!pfile.SignVerify.EndsWith(loadedVerify["verify"].AsString))
                {
                    var verify = Guid.NewGuid().ToString("N")[0..6];
                    loadedVerify["verify"] = "Chika" + verify;
                    gameUserVerify.Update(loadedVerify);
                    return new Profile()
                    {
                        SignVerify = "Chika" + verify
                    };
                }

                var gameUser = _ldb.GetCollection<BsonDocument>("game_user");
                var gameUserRecord = _ldb.GetCollection<BsonDocument>("game_user_record");

                var user = gameUser.FindById(qqReq.Qq);
                BsonValue docId = null;
                if (user != null)
                {
                    user["viewer_id"] = qqReq.ViewerId;
                    if (!user["groups"].AsArray.Contains(qqReq.Group))
                    {
                        user["groups"].AsArray.Add(qqReq.Group);
                    }
                    gameUser.Update(user);
                }
                else
                {
                    var dic = new BsonDocument
                    {
                        { "_id", qqReq.Qq },
                        { "viewer_id", qqReq.ViewerId },
                        { "groups", new BsonArray(){ qqReq.Group } }
                    };
                    docId = gameUser.Insert(dic);
                }

                var userRecord = gameUserRecord.FindById(qqReq.ViewerId);
                if (userRecord != null)
                {
                    if (docId != null)
                        user = gameUser.FindById(docId);
                    user["arena_group"] = pfile.Arena_group;
                    user["arena_rank"] = pfile.Arena_rank;
                    user["grand_arena_group"] = pfile.Grand_arena_group;
                    user["grand_arena_rank"] = pfile.Grand_arena_rank;
                }
                else
                {
                    var dic = new BsonDocument
                    {
                        {"_id", qqReq.ViewerId },
                        {"arena_group", pfile.Arena_group },
                        {"arena_rank", pfile.Arena_rank },
                        {"grand_arena_group", pfile.Grand_arena_group },
                        {"grand_arena_rank", pfile.Grand_arena_rank }
                    };
                    gameUserRecord.Insert(dic);
                }
            }
            pfile.SignVerify = "";
            return pfile;
        }
    }
}
