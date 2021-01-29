using Chika.GameCore;
using LiteDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Chika
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            var litedb = new LiteDatabase(AppDomain.CurrentDomain.BaseDirectory + "chika.db");
            //¥”json∂¡»°
            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                litedb.Dispose();
            };
            var tmpAccountFile = JsonConvert.DeserializeObject<List<dynamic>>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "chika_account.json"));
            var coll = litedb.GetCollection<BsonDocument>("game_account");
            var lst = new List<BsonDocument>();
            foreach (var acc in tmpAccountFile)
            {
                var fd = coll.FindById((string)acc.uid);
                if (acc.disabled != null && acc.disabled == 1)
                {
                    if (fd != null)
                        coll.Delete(fd["_id"]);
                    continue;
                }
                if (fd == null)
                {
                    lst.Add(new BsonDocument
                    {
                        {"_id", (string)acc.uid },
                        {"access_key", (string)acc.access_key }
                        //{"viewer_id", (string)acc.viewer_id }
                    });
                }
                else if (fd["access_key"].AsString != (string)acc.access_key)
                {
                    fd["access_key"] = (string)acc.access_key;
                    coll.Update(fd);
                }
            }
            if (lst.Count != 0)
            {
                coll.InsertBulk(lst);
                lst.Clear();
            }
            tmpAccountFile = null;
            //ÃÌº”’À∫≈≥ÿ
            var gameVersion = "2.4.10";
            foreach (var acc in coll.FindAll())
            {
                Console.WriteLine($"{acc["_id"].AsString} {acc["access_key"].AsString}");
                var client = new GameClient(gameVersion, new()
                {
                    { "uid", acc["_id"].AsString },
                    { "access_key", acc["access_key"].AsString },
                    { "platform", "1" },
                    { "channel_id", "1" },
                    //{ "viewer_id", acc["viewer_id"].AsString }
                }, Guid.NewGuid().ToString("D"));
                if (!client.disable)
                {
                    if (GameAccountPool.standaloneGameClientInstance == null)
                    {
                        GameAccountPool.standaloneGameClientInstance = client;
                        continue;
                    }
                    else
                        GameAccountPool.gameClientPool.Add(client);
                }
                else
                {
                    coll.Delete(acc["_id"]);
                }
                if (GameAccountPool.gameClientPool.Count == 8)
                    break;
            }
            GameAccountPool.gameClientPool = GameAccountPool.gameClientPool.ToList();
            services.AddSingleton(litedb);
            services.AddSingleton<IHostedService, RefreshService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
