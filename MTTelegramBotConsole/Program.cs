using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using MTTelegramBotLibrary;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace MTTelegramBotConsole
{
    class Program
    {
        private static TelegramBotClient Bot;
        private static ConfigurationRoot config;
        private static string dbtoken;
        private static Handlers h;
        public static async Task Main(string[] args)
        {
            config  = (ConfigurationRoot)new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false).Build();
            Bot = new TelegramBotClient(config.GetValue<string>("BotID"));
            var t = Bot.GetWebhookInfoAsync();
            await Bot.SetWebhookAsync("https://mttelegramsmsbot.azurewebsites.net/api/OnBotMessage?code=0FE0aiAcg6czKC4hro7uoTboNB8XRjOB48wIV0uj1LjyldmajfGhZQ==");
            dbtoken = await HttpAppAuthenticationAsync();
            h = new Handlers(config, null);
            
            //await Bot.SetWebhookAsync(string.Empty);
            User me = await Bot.GetMeAsync();
            Console.Title = me.Username ?? "My awesome Bot";

            using var cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new ReceiverOptions() { AllowedUpdates = { } };
            Bot.StartReceiving(h.HandleUpdateAsync,
                               h.HandleErrorAsync,
                               receiverOptions,
                               cts.Token);

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
        }

        private static async Task<string> HttpAppAuthenticationAsync()
        {
            //  Constants - get it from app config
            var tenant = config.GetValue<string>("TenantId");
            var resource = config.GetValue<string>("Resource");
            var clientID = config.GetValue<string>("ClientId");
            var secret = config.GetValue<string>("ClientSecret");
            using (var webClient = new WebClient())
            {
                var requestParameters = new NameValueCollection();
                requestParameters.Add("resource", resource);
                requestParameters.Add("client_id", clientID);
                requestParameters.Add("grant_type", "client_credentials");
                requestParameters.Add("client_secret", secret);

                var url = $"https://login.microsoftonline.com/" + tenant + "/oauth2/token";
                var responsebytes = await webClient.UploadValuesTaskAsync(url, "POST", requestParameters);
                var responsebody = Encoding.UTF8.GetString(responsebytes);
                var obj = JsonConvert.DeserializeObject<JObject>(responsebody);
                var token = obj["access_token"].Value<string>();

                return token;
            }
        }
    }
}
