﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;

namespace DiscordWikiBot
{
	class Program
	{
		// Configuration for Discord bot
		public static ConfigJson Config;

		// Instance of Discord client
		public static DiscordClient Client;

		// Available bot commands
		public CommandsNextModule Commands { get; set; }

		public static string Token;

		static void Main(string[] args) => new Program().Run().GetAwaiter().GetResult();

		public async Task Run()
		{
			// Check for a token
			string tokenPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName, @"token.txt");
			if (!File.Exists(tokenPath))
			{
				Client.DebugLogger.LogMessage(LogLevel.Critical, "DiscordWikiBot", "Please create a file called \"token.txt\" before running the bot!", DateTime.Now);
				Console.WriteLine("[Press any key to exit...]");
				Console.ReadKey();
				Environment.Exit(0);
			}
			Token = File.ReadAllText(tokenPath);

			// Get JSON config file
			string json = "";
			string cfgPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName, @"config.json");
			if (!File.Exists(cfgPath))
			{
				Client.DebugLogger.LogMessage(LogLevel.Critical, "DiscordWikiBot", "Please create a JSON file called \"config.json\" before running the bot!", DateTime.Now);
				Console.WriteLine("[Press any key to exit...]");
				Console.ReadKey();
				Environment.Exit(0);
			}
			json = File.ReadAllText(cfgPath);
			Config = JsonConvert.DeserializeObject<ConfigJson>(json);

			// Initialise Discord client
			Client = new DiscordClient(new DiscordConfiguration()
			{
				AutoReconnect = true,
				LargeThreshold = 250,
				LogLevel = LogLevel.Info,
				Token = Token,
				TokenType = TokenType.Bot,
				UseInternalLogHandler = true,
			});

			// Initialise events
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Initialising events", DateTime.Now);

			// Get site information and start linking bot
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Getting wiki site information", DateTime.Now);
			Linking.Init();

			Client.MessageCreated += Linking.Answer;

			// Start EventStreams
			if (Config.Domain != "")
			{
				EventStreams.Init();
			}

			// Set some events for logging the information
			Client.Ready += Client_Ready;
			Client.GuildAvailable += Client_GuildAvailable;
			Client.ClientErrored += Client_ClientErrored;

			// Initialise commands
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Setting up commands", DateTime.Now);
			Commands = Client.UseCommandsNext(new CommandsNextConfiguration
			{
				StringPrefix = "!",
				EnableDms = false,
				EnableMentionPrefix = true,
			});

			Commands.RegisterCommands<Streaming>();

			// Connect and start
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Connecting...", DateTime.Now);
			await Client.ConnectAsync();

			// Make sure not to close down automatically
			await Task.Delay(-1);
		}

		private Task Client_GuildAvailable(GuildCreateEventArgs e)
		{
			// Log the name of the guild that just became available
			e.Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", $"Guild available: {e.Guild.Name}", DateTime.Now);

			return Task.FromResult(0);
		}

		private Task Client_Ready(ReadyEventArgs e)
		{
			// Log the ready event
			e.Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Ready!", DateTime.Now);

			// Go offline since bot is supposed to run 24 hours a day
			// Client.UpdateStatusAsync(null, DSharpPlus.Entities.UserStatus.Invisible).Wait();

			return Task.FromResult(0);
		}

		private Task Client_ClientErrored(ClientErrorEventArgs e)
		{
			// Log the exception and its message
			e.Client.DebugLogger.LogMessage(LogLevel.Error, "DiscordWikiBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

			return Task.FromResult(0);
		}

		public struct ConfigJson
		{
			[JsonProperty("domain")]
			public string Domain { get; private set; }

			[JsonProperty("wiki")]
			public string Wiki { get; private set; }
		}
	}
}