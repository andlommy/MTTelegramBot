using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Linq;
using MTTelegramBotLibrary;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Collections.Generic;
using Azure.Identity;
using Azure.Core;
using System.Threading;

namespace MTTelegramBot
{
    public static class OnBotMessage
    {
        private static Handlers h;
        private static ConfigurationRoot config;
        private static CancellationToken ct;
        [FunctionName("OnBotMessage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {                
                config = (ConfigurationRoot)new ConfigurationBuilder().AddEnvironmentVariables().Build();
                
                
                
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                Update upd = JsonConvert.DeserializeObject<Update>(requestBody);

                h = new Handlers(config, log);
                ct = new CancellationToken();
                log.LogInformation("Request: " + requestBody.ToString());
                await h.HandleUpdateAsync(upd, ct,log);
                
                return new OkObjectResult(String.Empty);
            }
            catch (Exception ex)
            {
                log.LogError("Requestfailed : " + ex.Message);
                log.LogError("Requestfailed : " + ex.StackTrace);
                log.LogError("Requestfailed : " + ex.Source);
                log.LogError("Requestfailed : " + ex.Data);
                return new OkObjectResult(String.Empty);
            }
        }

        
    }
}
