using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore.SqlServer;

namespace MTTelegramBotLibrary
{
    public class Device
    {
        [Key]
        public long DeviceID { get; set; }
        public long ClientID { get; set; }
        public string DeviceKey { get; set; }
        public string DeviceName { get; set; }
    }
    public class Chat
    {
        [Key]
        public int ChatEntryID { get; set; }
        public long ChatID { get; set; }
        public ChatState ChatState { get; set; }

        public string ChatData { get; set; }
    }

    public enum ChatState
    {
        RegisteringDevice,
        RemovingDevice            
    }
    public class DB : DbContext
    {
        private readonly SqlConnection _connection;
        public DB(DbContextOptions<DB> options)
        {
            _connection =(SqlConnection)options.FindExtension<Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal.SqlServerOptionsExtension>().Connection;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_connection);
        }
        public DbSet<Device> Devices { get; set; }
        
        public DbSet<Chat> Chats { get; set; }
    }


}
