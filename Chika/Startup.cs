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
            //ÃÌº”’À∫≈≥ÿ
            var tmpAccountFileSdk = JsonConvert.DeserializeObject<List<dynamic>>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "chika_account_v2.json"));
            var gameVersion = "2.4.10";
            var geetestFile = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "geetest.txt");
            BGameSDK.apiSecretKey = geetestFile[0];
            foreach (var acc in tmpAccountFileSdk)
            {
                Console.WriteLine($"{acc.username} {acc.password}");
                var client = new GameClient(gameVersion, (string)acc.username, (string)acc.password, Guid.NewGuid().ToString("D"));
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
