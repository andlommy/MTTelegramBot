using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Core;

namespace MTTelegramBotLibrary
{
    public class Handlers
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        private static Random random = new Random();
        private static ConfigurationRoot config;
        private static string accesstoken;
        private static ITelegramBotClient bot; 
        public Handlers(ConfigurationRoot cfg,ILogger log)
        {
            config = cfg;
            accesstoken = GetDBToken(log);
            bot = new TelegramBotClient(config.GetValue<string>("BotID"));
        }
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return this.HandleErrorAsync(botClient, exception, cancellationToken, null);
        }
        public  Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken,ILogger log=null)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            log?.LogError(ErrorMessage);
            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            bot = botClient;
            await this.HandleUpdateAsync(update, cancellationToken);
        }
        public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
        {
            await this.HandleUpdateAsync(update, cancellationToken,null);
        }
        public  async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken,ILogger log=null)
        {
            
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(bot, update.Message!,log),
                _ => UnknownUpdateHandlerAsync(bot, update,log)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(bot, exception, cancellationToken);
            }
        }

        private  async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message,ILogger log=null)
        {
            log.LogInformation($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text)
                return;

            var action = message.Text!.Split(' ')[0] switch
            {
                "/info" => HandleGreet(botClient, message, log),
                "/subscribe" => HandleSubscribe(botClient, message, log),
                "/list" => HandleList(botClient, message, log),
                "/unsubscribe" => HandleUnsubscribe(botClient, message, log),
                _ => GeneralText(botClient, message, log)
            };
            Message sentMessage = await action;
            log?.LogInformation($"The message was sent with id: {sentMessage.MessageId}");

            // Send inline keyboard
            // You can process responses in BotOnCallbackQueryReceived handler
            async Task<Message> HandleGreet(ITelegramBotClient botClient, Message message, ILogger log)
            {
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                string response = "This bot will handle incoming SMS notifications from Mikrotik Devices and will forward them to you as they are received";
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: response                                                            );
            }

            async  Task<Message> HandleSubscribe(ITelegramBotClient botClient, Message message, ILogger log)
            {

                var subscribestring = new string(Enumerable.Range(1, 32).Select(_ => chars[random.Next(chars.Length)]).ToArray());
                try
                {
                    AddChatState(message.Chat.Id, ChatState.RegisteringDevice,subscribestring);
                }
                catch
                {
                    
                }
                
                return await botClient.SendTextMessageAsync(message.Chat.Id, "Please provide a name for this MT box");
            }

             async Task<Message> HandleList(ITelegramBotClient botClient, Message message, ILogger log)
            {
                DB DB = BuildDB();
                int devicecount = 0;
                foreach (Device registereddev in DB.Devices.Where(device=>device.ClientID==message.From.Id))
                {
                    await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: String.Format("Device Name: <b>{0}</b>\nDevice Key: <b>{1}</b>",registereddev.DeviceName,registereddev.DeviceKey),ParseMode.Html
                                                            );
                    devicecount++;
                }
                if (devicecount > 0)
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: "Total number of devices: " + devicecount.ToString()
                                                                );
                }
                else
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: "You have no registered devices."
                                                                );
                }
            }

             async Task<Message> HandleUnsubscribe(ITelegramBotClient botClient, Message message, ILogger log)
            {
                DB DB = BuildDB();
                AddChatState(message.Chat.Id, ChatState.RemovingDevice, string.Empty);
                List<KeyboardButton> buttons = new List<KeyboardButton>();
                    foreach (Device registereddev in DB.Devices.Where(device => device.ClientID == message.From.Id))
                {
                    buttons.Add(registereddev.DeviceName);
                }
                ReplyKeyboardMarkup replyKeyboardMarkup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };
                
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Select which device to unregister from notifications"
                                                            , replyMarkup:replyKeyboardMarkup);
            }

            async Task<Message> GeneralText(ITelegramBotClient botClient, Message message,ILogger log)
            {
                log.LogInformation("Handling general text");
                using DB DB = BuildDB();
                Chat ch = GetChatState(message.Chat.Id, DB);
                if (ch != null)
                {
                    log.LogInformation("ChatState is " + ch.ChatState);
                    if (ch.ChatState == ChatState.RegisteringDevice)
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, String.Format("Set your auth string to *{0}*", ch.ChatData), ParseMode.MarkdownV2);
                        Device newdevice = new Device { ClientID = message.From.Id, DeviceKey = ch.ChatData, DeviceName = message.Text.Trim() };
                        DB.Devices.Add(newdevice);
                        RemoveChatState(DB, ch);
                        return await botClient.SendTextMessageAsync(message.Chat.Id, String.Format("You will be messaged when device <b>{0}</b> uses key <b>{1}</b> will call out to the bot", newdevice.DeviceName, newdevice.DeviceKey), ParseMode.Html);
                    }
                    else if (ch.ChatState == ChatState.RemovingDevice)
                    {
                        Device devicetoremove = DB.Devices.SingleOrDefault(device => device.DeviceName == message.Text);
                        if (devicetoremove == null)
                        {
                            return await botClient.SendTextMessageAsync(message.Chat.Id, String.Format("Device {0} not found in list of registered devices", message.Text));
                        }
                        else
                        {
                            DB.Devices.Remove(devicetoremove);
                            RemoveChatState(DB, ch);
                            return await botClient.SendTextMessageAsync(message.Chat.Id, String.Format("Device {0} removed from list of registered devices", devicetoremove.DeviceName), replyMarkup: new ReplyKeyboardRemove());
                        }
                    }
                    else
                    {
                        return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: String.Format("Chat state {0} not handled", ch.ChatState), replyMarkup: new ReplyKeyboardRemove());
                    }
                }
                else
                {
                    log.LogInformation("Generic response to text");
                    const string usage = "Usage:\n" +
                                         "/info   - get information about the bot\n" +
                                         "/subscribe - register a new device for notifications\n" +
                                         "/list   - list registered devices\n" +
                                         "/unsubscribe    - unregister a previously registered device\n";

                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: usage,
                                                                replyMarkup: new ReplyKeyboardRemove());
                }
            }
        }

        private static void RemoveChatState(DB DB, Chat ch)
        {
            DB.Chats.Remove(ch);
            DB.SaveChangesAsync();
        }

        private static Chat GetChatState(long chatID, DB DB)
        {
            return DB.Chats.SingleOrDefault(ch => ch.ChatID == chatID);
        }

        private static void AddChatState(long chatID, ChatState state, string Chatdata)
        {
            using (DB DB = BuildDB())
            {
                Chat ch = DB.Chats.SingleOrDefault(ch => ch.ChatID == chatID);
                if (ch == null)
                {
                    Chat newchat = new Chat() { ChatID = chatID, ChatData = Chatdata, ChatState = state };
                    DB.Chats.Add(newchat);
                    DB.SaveChanges();
                }
            }
        }
        private static DB BuildDB()
        {
            return BuildDB(null);
        }

        private static DB BuildDB(ILogger log)
        {
            log?.LogInformation("in BuildDB");
            var optionsBuilder = new DbContextOptionsBuilder<DB>();
            SqlConnection conn = new SqlConnection(config.GetValue<string>("DBConnectionString"));
            log?.LogInformation("Connection string " + config.GetValue<string>("DBConnectionString"));
            conn.AccessToken = accesstoken;
            log?.LogInformation("Access token: " + accesstoken);
            var options = optionsBuilder.UseSqlServer(conn).Options;
            try
            {
                var DB = new DB(options);
                log?.LogInformation("Ending BuildDB"); 
                return DB;
            }
            catch (Exception ex)
            {
                log.LogError("Requestfailed : " + ex.Message);
                log.LogError("Requestfailed : " + ex.StackTrace);
                log.LogError("Requestfailed : " + ex.Source);
                log.LogError("Requestfailed : " + ex.Data);
            }
            log?.LogInformation("Failing BuildDB");
            return null;           
            
        }

        private  Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update,ILogger log=null)
        {
            log?.LogInformation($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }

        private static string GetDBToken(ILogger log)
        {
            var tokenCred = new DefaultAzureCredential();
            var armToken = tokenCred.GetToken(new TokenRequestContext(scopes: new[] { "https://database.windows.net/" }, parentRequestId: null), default).Token;
            return armToken;
        }

        public Device CheckToken(string token,ILogger log)
        {
            using (DB db = BuildDB())
            {
                Device dev = db.Devices.SingleOrDefault(d => d.DeviceKey.Equals(token));
                return dev;
            }
        }

        public Task ForwardSMSToRecepient(Device device,string message,ILogger log)
        {
            return bot.SendTextMessageAsync(
                device.ClientID,
                String.Format("Message received from device {0}\nMessage: {1}", device.DeviceName,message)
                );
        }
    }
}
