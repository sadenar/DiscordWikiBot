﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace DiscordWikiBot
{
	class Configuring
	{
		[Command("guildDomain"), Description("configuring-help-domain")]
		public async Task SetDomain(CommandContext ctx,
			[Description("configuring-help-domain-value"), RemainingText] string value)
		{
			string prevDomain = Config.GetDomain(ctx.Guild.Id.ToString());
			string lang = Config.GetLang(ctx.Guild.Id.ToString());

			// List of Wikimedia projects
			string[] wmfProjects = {
				".wikipedia.org",
				".wiktionary.org",
				".wikibooks.org",
				".wikinews.org",
				".wikiquote.org",
				".wikisource.org",
				".wikiversity.org",
				".wikivoyage.org",
				".wikimedia.org",
				"www.mediawiki.org",
				"www.wikidata.org"
			};

			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators")
			{
				await ctx.RespondAsync(Locale.GetMessage("denied", lang));
				return;
			};
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, "help guildDomain", Config.GetValue("prefix")));
				return;
			}

			// Check if matches Wikimedia project
			bool isWmfProject = false;
			if (value != "-" && wmfProjects.Any(value.Contains))
			{
				isWmfProject = true;
			}

			if (!isWmfProject)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-domain", lang, "`" + string.Join("`, `", wmfProjects) + "`"));
				return;
			}

			// Check for return to default
			if (value == "-")
			{
				value = Config.GetDomain();
			}

			// Do action and respond
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "domain", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				EventStreams.Subscribe(value);
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-domain", lang, value));
			}
			if (succeeds == Config.RESULT_RESET)
			{
				// Unsubscribe if this domain is not being used elsewhere
				if (value != prevDomain && !Config.IsValuePresent("domain", prevDomain)) {
					EventStreams.Unsubscribe(value);
				}
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		[Command("guildLang"), Description("configuring-help-lang")]
		public async Task SetLanguage(CommandContext ctx,
			[Description("configuring-help-lang-value")] string value)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());

			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators")
			{
				await ctx.RespondAsync(Locale.GetMessage("denied", lang));
				return;
			};
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, "help guildLang", Config.GetValue("prefix")));
				return;
			}

			// Check for return to default
			if (value == "-")
			{
				value = Config.GetLang();
			}

			// Check if it is a valid language
			if (!IsValidLanguage(value))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-lang", lang));
				return;
			}

			// Use new language in this command only
			lang = value;

			// Do action and respond
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "lang", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-lang", lang, value.ToUpper()));
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		[Command("guildTW"), Description("configuring-help-tw")]
		public async Task SetTranslate(CommandContext ctx,
			[Description("configuring-help-tw-channel")] DiscordChannel channel,
			[Description("configuring-help-tw-value"), RemainingText] string value)
		{
			string chanId = channel.Id.ToString();
			string chanPrevId = Config.GetTWChannel(ctx.Guild.Id.ToString());
			string chanPrevLang = Config.GetTWLang(ctx.Guild.Id.ToString());
			string lang = Config.GetLang(ctx.Guild.Id.ToString());

			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators")
			{
				await ctx.RespondAsync(Locale.GetMessage("denied", lang));
				return;
			};
			await ctx.TriggerTypingAsync();

			// Check for return to default
			if (value == "-")
			{
				chanId = Config.GetTWChannel();
				value = Config.GetTWLang();
			}

			// Check for required parameters
			if (channel == null)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-channel", lang, "help guildTW", Config.GetValue("prefix")));
				return;
			}

			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, "help guildTW", Config.GetValue("prefix")));
				return;
			}

			// Do action and respond
			int succeedsChan = Config.SetOverride(ctx.Guild.Id.ToString(), "translatewiki-channel", chanId);
			int succeedsLang = Config.SetOverride(ctx.Guild.Id.ToString(), "translatewiki-lang", value);

			if (succeedsChan == Config.RESULT_CHANGE && succeedsLang == Config.RESULT_CHANGE)
			{
				// Different channel and language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki", lang, channel.Mention, value.ToUpper()));
				TranslateWiki.Init(chanId, value);
			}

			if (succeedsChan == Config.RESULT_CHANGE && succeedsLang == Config.RESULT_RESET)
			{
				// Different channel, default language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-channel", lang, channel.Mention));
				TranslateWiki.Remove(chanId, chanPrevLang);
				TranslateWiki.Init(chanId, value);
			}

			if (succeedsChan == Config.RESULT_CHANGE && succeedsLang == Config.RESULT_SAME)
			{
				// Different channel, same language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-channel", lang, channel.Mention));
				TranslateWiki.Remove(chanPrevId, value);
				TranslateWiki.Init(chanId, value);
			}

			if ( succeedsChan == Config.RESULT_RESET && succeedsLang == Config.RESULT_CHANGE
				|| (
					succeedsChan == Config.RESULT_SAME
					&& (succeedsLang == Config.RESULT_RESET || succeedsLang == Config.RESULT_CHANGE)
				)
			)
			{
				// Same or default channel, different language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-lang", lang, value.ToUpper()));
				TranslateWiki.Remove(chanId, chanPrevLang);
				TranslateWiki.Init(chanId, value);
			}

			if (succeedsChan == Config.RESULT_RESET && succeedsLang == Config.RESULT_RESET)
			{
				// Reset both channel and language with -
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-reset", lang));
				TranslateWiki.Remove(channel.Id.ToString(), value);
			}

			if (succeedsChan == Config.RESULT_STRANGE || succeedsLang == Config.RESULT_STRANGE)
			{
				// Other strange errors
				await ctx.RespondAsync(Locale.GetMessage("configuring-error-strange", lang));
			}
		}

		[Command("guildWiki"), Description("configuring-help-wiki")]
		public async Task SetWiki(CommandContext ctx,
			[Description("configuring-help-wiki-value"), RemainingText] string value)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());

			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators")
			{
				await ctx.RespondAsync(Locale.GetMessage("denied", lang));
				return;
			};
			await ctx.TriggerTypingAsync();

			// Check for return to default
			if (value == "-")
			{
				value = Config.GetWiki();
			}

			// Check for required parameters
			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, "help guildWiki", Config.GetValue("prefix")));
				return;
			}

			if (!value.Contains("/wiki/$1"))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-wiki", lang));
			}

			// Provide some changes
			value = value.Replace("<", String.Empty).Replace(">", String.Empty);

			// Do action and respond
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "wiki", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				Linking.Init(ctx.Guild.Id.ToString());
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-wiki", lang, value));
			}
			if (succeeds == Config.RESULT_RESET)
			{
				Linking.Remove(ctx.Guild.Id.ToString());
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		private async Task RespondOnErrors(int response, CommandContext ctx, string lang)
		{
			if (response == Config.RESULT_RESET)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-reset", lang));
			}

			if (response == Config.RESULT_SAME)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-error-same", lang));
			}

			if (response == Config.RESULT_STRANGE)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-error-strange", lang));
			}
		}

		private bool IsValidLanguage(string name)
		{
			return CultureInfo
				.GetCultures(CultureTypes.NeutralCultures)
				.Any(c => c.Name == name);
		}
	}
}
