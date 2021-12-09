using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MTTelegramBotLibrary;
using Telegram.Bot;
using Microsoft.Extensions.Configuration;

namespace MTTelegramBot
{
    public static class OnMTMessage
    {
        private static Handlers h;
        private static ConfigurationRoot config;
        [FunctionName("OnMTMessage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            config = (ConfigurationRoot)new ConfigurationBuilder().AddEnvironmentVariables().Build();
            h = new Handlers(config,log);
            string token = req.Query["token"];
            if (token != null)
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);                
                Device dev = h.CheckToken(token,log);
                if (dev != null)
                {
                    string messagedate = data.messagedate;
                    string message = data.message;
                    string from = data.from;
                    h.ForwardSMSToRecepient(dev, message,log);
                }
                else
                {
                    return new UnauthorizedResult();
                }
            }
            else
            {
                return new UnauthorizedResult();
            }
            
            return new OkObjectResult("");
        }
    }
}
