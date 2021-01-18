using LiteDB;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Chika.Model.ChikaModel;

namespace Chika.GameCore
{
    public class RefreshService : BackgroundService
    {
        private LiteDatabase _ldb;
        private RestClient _postHimariClient = new RestClient("http://127.0.0.1:7772");
        public RefreshService(LiteDatabase db)
        {
            _ldb = db;
            _postHimariClient.AddDefaultHeaders(new()
            {
                {
                    "Content-Type",
                    "application/json"
                }
            });
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var random = new Random();
            ThreadPool.SetMinThreads(GameAccountPool.gameClientPool.Count + 1, GameAccountPool.gameClientPool.Count + 1);
            var stopWatch = new Stopwatch();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //var updateList = new List<ChikaUpdateData>();
                    stopWatch.Restart();
                    var gameUserDb = _ldb.GetCollection<BsonDocument>("game_user");
                    var gameUser = gameUserDb.FindAll();
                    //var gameUserDb = gameUser.FindAll().ToList();
                    var gameUserRecord = _ldb.GetCollection<BsonDocument>("game_user_record");
                    var gameUserVerify = _ldb.GetCollection<BsonDocument>("game_user_verify");
                    //击剑记录
                    var battleLog = _ldb.GetCollection<BsonDocument>("battle_log", BsonAutoId.Int32);

                    int maxThread = GameAccountPool.gameClientPool.Count;
                    using ManualResetEvent evt = new ManualResetEvent(false);
                    int tmp = (int)Math.Ceiling(gameUser.Count() * 1.0 / GameAccountPool.gameClientPool.Count);
                    int refreshed = 0;

                    var delete = new List<BsonValue>();

                    //var log = new Dictionary<string, List<long>>();

                    for (int i = 0; i < GameAccountPool.gameClientPool.Count; i++)
                    {
                        ThreadPool.QueueUserWorkItem((object state) =>
                        {
                            Console.WriteLine("[GameClient{0}] Starting... {1} {2}", state, tmp * (int)state, tmp);
                            var gameUserTake = gameUser.Skip(tmp * (int)state).Take(tmp);

                            /*
                            lock (log)
                            {
                                var lst = new List<long>();
                                log.Add($"GameClient{state}", lst);
                                foreach (var gameUserData in gameUserTake)
                                {
                                    log[$"GameClient{state}"].Add(gameUserData["viewer_id"].AsInt64);
                                }
                            }
                            */

                            //for (int j = 0; j <= tmp; j++)
                            foreach (var gameUserData in gameUserTake)
                            {
                                /*
                                var st = j * GameAccountPool.gameClientPool.Count + (int)state;
                                //Console.WriteLine("[Debug] {0}", st);
                                if (st >= gameUser.Count())
                                {
                                    Console.WriteLine("[GameClient{0}] Cancelled FullCount:{1}", state, gameUser.Count());
                                    break;
                                }
                                */
                                var viewerId = gameUserData["viewer_id"].AsInt64;
                                var profileResp = GameAccountPool.gameClientPool[(int)state].GetProfile(viewerId);
                                if (!profileResp.SignVerify.EndsWith(gameUserVerify.FindById(viewerId)["verify"].AsString))
                                {
                                    lock (delete)
                                    {
                                        delete.Add(gameUserData["_id"]);
                                    }
                                    Console.WriteLine("[GameClient{0}] 因为签名不符合预期，删除{1}", state, viewerId);
                                    continue;
                                }
                                var profileOri = gameUserRecord.FindById(viewerId);
                                Console.WriteLine("[GameClient{0}] Got Response {1} {2} {3} {4} {5}", state, viewerId, profileResp.Arena_group, profileResp.Arena_rank, profileResp.Grand_arena_group, profileResp.Grand_arena_rank);
                                if (profileResp.Arena_rank != profileOri["arena_rank"].AsInt32 || profileResp.Grand_arena_rank != profileOri["grand_arena_rank"].AsInt32)
                                {
                                    var grp = gameUserData["groups"].AsArray;
                                    var grpLst = new List<long>();
                                    foreach (var g in grp)
                                    {
                                        grpLst.Add(g.AsInt64);
                                    }
                                    //lock (updateList)
                                    //{
                                        var chika = new ChikaUpdateData
                                        {
                                            qq = gameUserData["_id"].AsInt64,
                                            arena_rank_before = profileOri["arena_rank"].AsInt32,
                                            arena_rank_after = profileResp.Arena_rank,
                                            grand_arena_rank_before = profileOri["grand_arena_rank"].AsInt32,
                                            grand_arena_rank_after = profileResp.Grand_arena_rank,
                                            groups = grpLst
                                        };
                                    var req = new RestRequest("/himari_chika_api/chika_update_2", Method.POST);
                                    var updd = JsonConvert.SerializeObject(chika);
                                    req.AddParameter("", updd, ParameterType.RequestBody);
                                    Console.WriteLine("[RefreshService] Sending to Himari... {0}", updd);
                                    _postHimariClient.Execute(req);
                                    if (battleLog.Count(x => x["viewer_id"].AsInt64 == viewerId) >= 20)
                                    {
                                        var tmp = battleLog.Find(x => x["viewer_id"].AsInt64 == viewerId).OrderByDescending(x => x["time"].AsDateTime).Take(5);
                                        battleLog.DeleteMany(x => x["viewer_id"].AsInt64 == viewerId && !tmp.Contains(x));
                                    }
                                    Thread.Sleep(500);
                                    //Console.WriteLine("[GameClient{0}] PostHimari {1}", state, viewerId);
                                    //}
                                    battleLog.Insert(new BsonDocument
                                    {
                                        {"viewer_id", viewerId },
                                        {"a_before", profileOri["arena_rank"] },
                                        {"a_after", profileResp.Arena_rank },
                                        {"ga_before", profileOri["grand_arena_rank"] },
                                        {"ga_after", profileResp.Grand_arena_rank },
                                        {"time", DateTime.Now }
                                    });
                                    profileOri["arena_rank"] = profileResp.Arena_rank;
                                    profileOri["grand_arena_rank"] = profileResp.Grand_arena_rank;
                                    gameUserRecord.Update(profileOri);
                                }
                                refreshed += 1;
                                Thread.Sleep(random.Next(50));
                            }
                            if (Interlocked.Decrement(ref maxThread) == 0)
                            {
                                evt.Set();
                            }
                        }, i);
                    }
                    evt.WaitOne();
                    stopWatch.Stop();
                    Console.WriteLine("[RefreshService] 已刷新{0}/{1}个用户", refreshed, gameUser.Count());
                    Console.WriteLine("[ChikaStopWatch] Finished in {0} ms", stopWatch.ElapsedMilliseconds);
                    if (delete.Count != 0)
                    {
                        foreach (var p in delete)
                        {
                            gameUserDb.Delete(p);
                        }
                    }
                    //File.WriteAllText("TEST.json", JsonConvert.SerializeObject(log));
                    var dft = 10000 - gameUser.Count() * 20;
                    await Task.Delay(dft < 5000 ? 5000 : dft, stoppingToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine("[RefreshService] {0}", e.Message);
                }
            }
        }
    }
}
