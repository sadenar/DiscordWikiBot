﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using Newtonsoft.Json;

namespace DiscordWikiBot
{
	class Linking
	{
		// Link pattern: [[]], [[|, {{}} or {{|
		private static string pattern = string.Format("(?:{0}|{1})",
			"(\\[{2})([^\\[\\]\\|\n]+)(?:\\|[^\\[\\]\\|\n]*)?]{2}",
			"({{2})([^#][^{}\\|\n]+)(?:\\|*[^{}\n]*)?}{2}");

		// Site information storage
		public class SiteInfo
		{
			public InterwikiMap iw;
			public NamespaceCollection ns;
			public bool isCaseSensitive;
		}

		// Permanent site information for main wiki
		public static InterwikiMap IWList;
		public static NamespaceCollection NSList;
		public static bool IsCaseSensitive;

		static public void Init()
		{
			// Fetch site information for default wiki
			SiteInfo data = FetchSiteInfo(Program.Config.Wiki).Result;
			IWList = data.iw;
			NSList = data.ns;
			IsCaseSensitive = data.isCaseSensitive;
		}

		static public Task Answer(MessageCreateEventArgs e)
		{
			// Stop answering to bots
			if (e.Message.Author.IsBot) return Task.FromResult(0);

			// Remove code from the message
			string content = e.Message.Content;
			content = Regex.Replace(content, "```(.|\n)*?```", String.Empty);
			content = Regex.Replace(content, "`.*?`", String.Empty);

			// Start digging for links
			string msg = "";
			MatchCollection matches = Regex.Matches(content, pattern);
			List<string> links = new List<string>();

			if (matches.Count > 0)
			{
				// Go through matches
				foreach (Match link in matches)
				{
					string str = AddLink(link);
					if (!links.Contains(str))
					{
						msg += str;
						links.Add(str);
					}
				}

				// Check if message is not empty and send it
				if (msg != "")
				{
					msg = (links.Count > 1 ? "Ссылки:\n" : "Ссылка: ") + msg;
					
					return e.Message.RespondAsync(msg);
				}
			}

			return Task.FromResult(0);
		}

		static public string AddLink(Match link)
		{
			string linkFormat = Program.Config.Wiki;
			bool linkIsLanguageVersion = true;
			GroupCollection groups = link.Groups;
			string type = ( groups[1].Value.Length == 0 ? groups[3].Value : groups[1].Value).Trim();
			string str = ( groups[2].Value.Length == 0 ? groups[4].Value : groups[2].Value ).Trim();

			// Temporary site info for other wikis
			InterwikiMap tempIWList = null;
			NamespaceCollection tempNSList = null;
			bool tempIsCaseSensitive = false;

			// Remove escaping symbols before Markdown syntax in Discord
			// (it converts \ to / anyway)
			str = str.Replace("\\", "");

			// Check for invalid page titles
			if (IsInvalid(str)) return "";

			// Storages for prefix and namespace data
			string iw = "%%%%%";
			string ns = "";

			if (str.Length > 0)
			{
				// Add template namespace for template links and remove substitution
				if (type == "{{")
				{
					ns = NSList["template"].CustomName;
					str = Regex.Replace(str, "^(?:subst|подст):", "");
				}

				// Check if link contains interwikis
				Match iwMatch = Regex.Match(str, "^:?([A-Za-z-]+):");
				while (type == "[[" && iwMatch.Length > 0)
				{
					string prefix = iwMatch.Groups[1].Value.ToLower();
					InterwikiMap list = (tempIWList != null ? tempIWList : IWList);
					if (list.Contains(prefix))
					{
						string oldLinkFormat = linkFormat;
						linkFormat = list[prefix].Url;
						linkIsLanguageVersion = (list[prefix].LanguageAutonym != null || list[prefix].SiteName != null);

						// Fetch temporary site information if necessary and store new prefix
						if (iw != "" || oldLinkFormat.Replace(iw, prefix) != linkFormat)
						{
							SiteInfo data = FetchSiteInfo(linkFormat).Result;
							tempIWList = data.iw;
							tempNSList = data.ns;
							tempIsCaseSensitive = data.isCaseSensitive;
						}
						iw = prefix;

						str = Regex.Replace(str, $":?{prefix}:", "", RegexOptions.IgnoreCase).Trim();
						iwMatch = Regex.Match(str, "^:?([A-Za-z-]+):");
					} else
					{
						// Return the regex that can’t be matched
						iwMatch = Regex.Match(str, "^\b$");
					}
				}

				// Check if link contains namespace
				Match nsMatch = Regex.Match(str, "^:?(.*):");
				if (nsMatch.Length > 0)
				{
					string prefix = nsMatch.Groups[1].Value.ToUpper();
					if (linkFormat == Program.Config.Wiki)
					{
						if (NSList.Contains(prefix))
						{
							ns = NSList[prefix].CustomName;
							str = Regex.Replace(str, $":?{prefix}:", "", RegexOptions.IgnoreCase);
						}
					} else if (linkIsLanguageVersion)
					{
						if (tempNSList.Contains(prefix))
						{
							ns = tempNSList[prefix].CustomName;
							str = Regex.Replace(str, $":?{prefix}:", "", RegexOptions.IgnoreCase);
						}
					}
				}

				// If there is only namespace, return nothing
				if (ns != "" && str.Length == 0) return "";

				// Rewrite other text
				if (str.Length > 0)
				{
					// Capitalise first letter if wiki does not allow lowercase titles
					if ((linkFormat == Program.Config.Wiki && !IsCaseSensitive) || (linkFormat != Program.Config.Wiki && !tempIsCaseSensitive))
					{
						str = str[0].ToString().ToUpper() + str.Substring(1);
					}

					// Clear temporary site info
					tempIWList = null;
					tempNSList = null;
					tempIsCaseSensitive = false;

					// Add namespace before any transformations
					if (ns != "")
					{
						str = string.Join(":", new[] { ns, str });
					}

					// Do a set of character conversions
					str = EncodePageTitle(str);
				}
				return $"<{linkFormat}>\n".Replace("$1", str);
			}
			return "";
		}

		public static async Task<SiteInfo> FetchSiteInfo(string url)
		{
			string urlWiki = "/wiki/$1";
			SiteInfo result = new SiteInfo();
			if (url.Contains(urlWiki))
			{
				// Connect with API if it is a wiki site
				WikiClient wikiClient = new WikiClient
				{
					ClientUserAgent = "DiscordWikiBot/1.0",
				};
				Site site = await Site.CreateAsync(wikiClient, url.Replace(urlWiki, "/w/api.php"));

				// Generate and return the info needed
				result.iw = site.InterwikiMap;
				result.ns = site.Namespaces;

				result.isCaseSensitive = site.SiteInfo.IsTitleCaseSensitive;
			}

			await Task.FromResult(0);
			return result;
		}

		public static bool IsInvalid(string str)
		{
			var anchor = str.Split('#');
			if (anchor.Length > 1)
			{
				str = anchor[0];
			}

			// Following checks are based on MediaWiki page title restrictions:
			// https://www.mediawiki.org/wiki/Manual:Page_title
			string[] illegalExprs =
			{
				"\\<", "\\>",
				"\\[", "\\]",
				"\\{", "\\}",
				"\\|",
				"~{3,}",
				"&(?:[a-z]+|#x?\\d+);"
			};

			foreach(string expr in illegalExprs)
			{
				if (Regex.Match(str, expr, RegexOptions.IgnoreCase).Success) return true;
			}

			return false;
		}

		public static string EncodePageTitle(string str)
		{
			// Following character conversions are based on {{PAGENAMEE}} specification:
			// https://www.mediawiki.org/wiki/Manual:PAGENAMEE_encoding
			char[] specialChars =
			{
				'"',
				'%',
				'&',
				'+',
				'=',
				'?',
				'\\',
				'^',
				'`',
				'~'
			};
			
			// Replace all spaces to underscores
			str = Regex.Replace(str, "\\s{1,}", "_");

			// Percent encoding for special characters
			foreach (var ch in specialChars)
			{
				str = str.Replace(ch.ToString(), Uri.EscapeDataString(ch.ToString()));
			}

			return str;
		}
	}
}