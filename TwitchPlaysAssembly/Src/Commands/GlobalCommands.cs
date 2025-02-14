﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

using Random = UnityEngine.Random;

/// <summary>Commands that can generally be used at any time.</summary>
static class GlobalCommands
{
	/// <name>Help</name>
	/// <syntax>help</syntax>
	/// <summary>Gives you some help on how to play TP.</summary>
	[Command(@"(manual|help)")]
	public static void Help(string user, bool isWhisper)
	{
		string[] alphabet = new string[26] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
		string[] randomCodes =
		{
			TwitchPlaySettings.data.EnableLetterCodes ? alphabet[Random.Range(0, alphabet.Length)] + alphabet[Random.Range(0, alphabet.Length)] : Random.Range(1, 100).ToString(),
			TwitchPlaySettings.data.EnableLetterCodes ? alphabet[Random.Range(0, alphabet.Length)] + alphabet[Random.Range(0, alphabet.Length)] : Random.Range(1, 100).ToString()
		};

		IRCConnection.SendMessage(string.Format("!{0} manual [マニュアルへのリンク] | マニュアルは {1}?lang=ja から探すことができます。", randomCodes[0], TwitchPlaySettings.data.RepositoryUrl), user, !isWhisper);
		IRCConnection.SendMessage(string.Format("!{0} help [モジュールのコマンド] | コマンドについては https://tepel-chen.github.io/tpCommands/ を参照してください。", randomCodes[1], UrlHelper.CommandReference), user, !isWhisper);
	}

	/// <name>Bonus Points</name>
	/// <syntax>bonuspoints [player] [points]</syntax>
	/// <summary>Adds points to a player's score.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"bonus(?:score|points) (\S+) (-?[0-9]+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void BonusPoints([Group(1)] string targetPlayer, [Group(2)] int bonus, string user)
	{
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.GiveBonusPoints, targetPlayer, bonus, user);
		Leaderboard.Instance.AddScore(targetPlayer, new Color(.31f, .31f, .31f), bonus);
	}

	/// <name>Bonus Solves</name>
	/// <syntax>bonussolves [player] [solves]</syntax>
	/// <summary>Adds solves to a player.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"bonussolves? (\S+) (-?[0-9]+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void BonusSolves([Group(1)] string targetPlayer, [Group(2)] int bonus, string user)
	{
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.GiveBonusSolves, targetPlayer, bonus, user);
		Leaderboard.Instance.AddSolve(targetPlayer, new Color(.31f, .31f, .31f), bonus);
	}

	/// <name>Bonus Strikes</name>
	/// <syntax>bonusstrikes [player] [strikes]</syntax>
	/// <summary>Adds strikes to a player.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"bonusstrikes? (\S+) (-?[0-9]+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void BonusStrikes([Group(1)] string targetPlayer, [Group(2)] int bonus, string user)
	{
		IRCConnection.SendMessageFormat(TwitchPlaySettings.data.GiveBonusStrikes, targetPlayer, bonus, user);
		Leaderboard.Instance.AddStrike(targetPlayer, new Color(.31f, .31f, .31f), bonus);
	}

	/// <name>Strike Refund</name>
	/// <syntax>srefund [user] (count)</syntax>
	/// <summary>Refunds a strike that happens to the user.</summary>
	/// <restriction>Admin</restriction>
	[Command(@"srefund +(\S+) *?( +[0-9]+)?", AccessLevel.Admin, AccessLevel.Admin)]
	public static void StrikeRefund([Group(1)] string targetPlayer, [Group(2)] int? _count, string user)
	{
		int count = _count ?? 1;
		if (targetPlayer.StartsWith("@"))
			targetPlayer = targetPlayer.Substring(1);

		if (count < 1)
		{
			IRCConnection.SendMessageFormat("@{0} - 取り消しするミスの数は1以上で無ければなりません。", user);
			return;
		}

		int points = TwitchPlaySettings.data.StrikePenalty * count;
		Leaderboard.Instance.AddStrike(targetPlayer, new Color(.31f, .31f, .31f), -1 * count);
		Leaderboard.Instance.AddScore(targetPlayer, new Color(.31f, .31f, .31f), points);

		IRCConnection.SendMessageFormat("@{2}から{0}回のミスと{1}ポイントの減点をを取り消しました。", count, points, targetPlayer);
	}

	/// <name>Strike Transfer</name>
	/// <syntax>stransfer [user] to [user] (count)</syntax>
	/// <summary>Transfers a strike from the first user to the second.</summary>
	/// <restriction>Admin</restriction>
	[Command(@"stransfer +(\S+) +to +(\S+) *?( +[0-9]+)?", AccessLevel.Admin, AccessLevel.Admin)]
	public static void StrikeTransfer([Group(1)] string fromPlayer, [Group(2)] string toPlayer, [Group(3)] int? _count, string user)
	{
		int count = _count ?? 1;
		if (fromPlayer.StartsWith("@"))
			fromPlayer = fromPlayer.Substring(1);
		if (toPlayer.StartsWith("@"))
			toPlayer = toPlayer.Substring(1);

		if (count < 1)
		{
			IRCConnection.SendMessageFormat("@{0} - 移行するミスの数は1以上で無ければなりません。", user);
			return;
		}

		int points = TwitchPlaySettings.data.StrikePenalty * count;
		Leaderboard.Instance.AddStrike(fromPlayer, new Color(.31f, .31f, .31f), -1 * count);
		Leaderboard.Instance.AddScore(fromPlayer, new Color(.31f, .31f, .31f), points);
		Leaderboard.Instance.AddStrike(toPlayer, new Color(.31f, .31f, .31f), count);
		Leaderboard.Instance.AddScore(toPlayer, new Color(.31f, .31f, .31f), -1 * points);

		IRCConnection.SendMessageFormat("@{2}から{3}へ、{0}回のミスと{1}ポイントの減点をを移行ました。", count, points, fromPlayer, toPlayer);
	}

	/// <name>Set Reward</name>
	/// <syntax>reward [points]</syntax>
	/// <summary>Sets the reward that's given out on a succesful defusual.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"reward (-?[0-9]+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void SetReward([Group(1)] int reward) => TwitchPlaySettings.SetRewardBonus(reward);

	/// <name>Add Reward</name>
	/// <syntax>bonusreward [points]</syntax>
	/// <summary>Adds to the reward that's given out on a succesful defusual.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"bonusreward (-?[0-9]+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void AddReward([Group(1)] int reward) => TwitchPlaySettings.AddRewardBonus(reward);

	/// <name>Time Mode</name>
	/// <syntax>timemode [state]</syntax>
	/// <summary>Enables or disables time mode. [state] is either on or off.</summary>
	[Command(@"timemode( *(on)| *off)?")]
	public static void TimeMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.Time, !any, on, user, isWhisper, TwitchPlaySettings.data.EnableTimeModeForEveryone, TwitchPlaySettings.data.TimeModeCommandDisabled);
	/// <name>VS Mode</name>
	/// <syntax>vsmode [state]</syntax>
	/// <summary>Enables or disables VS mode. [state] is either on or off.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vsmode( *(on)| *off)?", AccessLevel.Mod, AccessLevel.Mod)]
	public static void VsMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.VS, !any, on, user, isWhisper, false, TwitchPlaySettings.data.VsModeCommandDisabled);
	/// <name>Zen Mode</name>
	/// <syntax>zenmode [state]</syntax>
	/// <summary>Enables or disables zen mode. [state] is either on or off.</summary>
	[Command(@"zenmode( *(on)| *off)?")]
	public static void ZenMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.Zen, !any, on, user, isWhisper, TwitchPlaySettings.data.EnableZenModeForEveryone, TwitchPlaySettings.data.ZenModeCommandDisabled);
	/// <name>Training Mode</name>
	/// <syntax>trainingmode [state]</syntax>
	/// <summary>Enables or disables training mode. [state] is either on or off.</summary>
	[Command(@"trainingmode( *(on)| *off)?")]
	public static void TrainingMode([Group(1)] bool any, [Group(2)] bool on, string user, bool isWhisper) => SetGameMode(TwitchPlaysMode.Training, !any, on, user, isWhisper, TwitchPlaySettings.data.EnableTrainingModeForEveryone, TwitchPlaySettings.data.TrainingModeCommandDisabled);

	/// <name>Show Mode</name>
	/// <syntax>mode</syntax>
	/// <summary>Sends a message to chat with the current and upcoming mode.</summary>
	[Command(@"modes?")]
	public static void ShowMode(string user, bool isWhisper)
	{
		IRCConnection.SendMessage(string.Format("現在は{0}モードが有効化されています。次のゲームは{1}モードになります。", OtherModes.GetName(OtherModes.currentMode), OtherModes.GetName(OtherModes.nextMode)), user, !isWhisper);
		if (TwitchPlaySettings.data.AnarchyMode)
			IRCConnection.SendMessage("現在アナーキーモードです。", user, !isWhisper);
	}

	/// <name>Reset User</name>
	/// <syntax>resetuser [users]</syntax>
	/// <summary>Resets a user's information on the leaderboard. [users] is a list of usernames seperated by a semicolon.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"resetusers? +(.+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void ResetUser([Group(1)] string parameters, string user, bool isWhisper)
	{
		foreach (string userRaw in parameters.Split(';'))
		{
			string usertrimmed = userRaw.Trim();
			Leaderboard.Instance.GetRank(usertrimmed, out var entry);
			Leaderboard.Instance.GetSoloRank(usertrimmed, out var soloEntry);
			if (entry == null && soloEntry == null)
			{
				IRCConnection.SendMessage($"ユーザー{usertrimmed}はすでにリセットされたか、あるいは見つかりませんでした。", user, !isWhisper);
				continue;
			}
			if (entry != null)
				Leaderboard.Instance.DeleteEntry(entry);
			if (soloEntry != null)
				Leaderboard.Instance.DeleteSoloEntry(soloEntry);
			IRCConnection.SendMessage($"ユーザー{usertrimmed}はリセットされました。", userRaw, !isWhisper);
		}
	}

	#region Voting
	/// <name>Start a vote</name>
	/// <syntax>vote [action]</syntax>
	/// <summary>Starts a vote about doing an action</summary>
	[Command(@"vote (togglevs)")]
	public static void VoteStart(string user, [Group(1)] bool VSMode) => Votes.StartVote(user, VSMode ? VoteTypes.VSModeToggle : 0);

	/// <name>Vote</name>
	/// <syntax>vote [choice]</syntax>
	/// <summary>Vote with yes or no</summary>
	[Command(@"vote ((yes|voteyea)|(no|votenay))")]
	public static void Vote(string user, [Group(2)] bool yesVote) => Votes.Vote(user, yesVote);

	/// <name>Remove vote</name>
	/// <syntax>vote remove</syntax>
	/// <summary>Removes the vote of a user</summary>
	[Command(@"vote remove")]
	public static void RemoveVote(string user) => Votes.RemoveVote(user);

	/// <name>Time left of vote</name>
	/// <syntax>vote time</syntax>
	/// <summary>Shows remaining voting time</summary>
	[Command(@"vote time")]
	public static void ShowVoteTime(string user) => Votes.TimeLeftOnVote(user);

	/// <name>Cancel vote</name>
	/// <syntax>vote cancel</syntax>
	/// <summary>Cancels a voting process</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vote cancel", AccessLevel.Mod, AccessLevel.Mod)]
	public static void CancelVote(string user) => Votes.CancelVote(user);

	/// <name>Force-end vote</name>
	/// <syntax>vote forceend</syntax>
	/// <summary>Skips the countdown of the voting process</summary>
	/// <restriction>Mod</restriction>
	[Command(@"vote forceend", AccessLevel.Mod, AccessLevel.Mod)]
	public static void ForceEndVote(string user) => Votes.EndVoteEarly(user);
	#endregion

	/// <name>My Rank</name>
	/// <syntax>rank</syntax>
	/// <summary>Sends a message with what your current rank is.</summary>
	[Command(@"rank")]
	public static void OwnRank(string user, bool isWhisper) { Leaderboard.Instance.GetRank(user, out var entry); ShowRank(entry, user, user, isWhisper); }

	/// <name>Get Solo Rank</name>
	/// <syntax>rank solo [rank]</syntax>
	/// <summary>Sends a message with who currently has that solo rank.</summary>
	[Command(@"rank solo (\d+)")]
	public static void SoloRank([Group(1)] int desiredRank, string user, bool isWhisper)
	{
		var entries = Leaderboard.Instance.GetSoloEntries(desiredRank);
		ShowRank(entries, user, user, isWhisper, numeric: true);
	}

	/// <name>Get Solo Rank By User</name>
	/// <syntax>rank solo [user]</syntax>
	/// <summary>Sends a message with the solo rank of that user.</summary>
	[Command(@"rank solo (?!\d+$)(.*)")]
	public static void SoloRankByUser([Group(1)] string desiredUser, string user, bool isWhisper) { Leaderboard.Instance.GetSoloRank(desiredUser, out var entry); ShowRank(entry, desiredUser, user, isWhisper); }

	/// <name>Get Rank</name>
	/// <syntax>rank [rank]</syntax>
	/// <summary>Sends a message with who currently has that rank.</summary>
	[Command(@"rank (\d+)")]
	public static void Rank([Group(1)] int desiredRank, string user, bool isWhisper)
	{
		var entries = Leaderboard.Instance.GetEntries(desiredRank);
		ShowRank(entries, user, user, isWhisper, numeric: true);
	}

	/// <name>Get Rank By User</name>
	/// <syntax>rank [user]</syntax>
	/// <summary>Sends a message with the rank of that user.</summary>
	[Command(@"rank (?!\d+$)(.*)")]
	public static void RankByUser([Group(1)] string desiredUser, string user, bool isWhisper) { Leaderboard.Instance.GetRank(desiredUser, out var entry); ShowRank(entry, desiredUser, user, isWhisper); }

	/// <name>Get Previous Log</name>
	/// <syntax>log</syntax>
	/// <summary>Sends a message with the previous log.</summary>
	[Command(@"(log|analysis)")]
	public static void Log() => LogUploader.PostToChat(LogUploader.Instance.previousUrl, "前回の爆弾の解析ログ: {0}");

	/// <name>Get Log</name>
	/// <syntax>lognow</syntax>
	/// <summary>Sends a message with the current log.</summary>
	/// <restriction>SuperUser</restriction>
	[Command("(log|analysis)now", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void LogNow(string user, bool isWhisper) => LogUploader.Instance.GetAnalyzerUrl(url => IRCConnection.SendMessage(url, user, !isWhisper));

	/// <name>Toggle Short URLs</name>
	/// <syntax>shorturl</syntax>
	/// <summary>Toggles shortened URLs.</summary>
	[Command(@"shorturl")]
	public static void ShortURL(string user, bool isWhisper) => IRCConnection.SendMessage(string.Format((UrlHelper.ToggleMode()) ? "Enabling shortened URLs" : "Disabling shortened URLs"), user, !isWhisper);

	/// <name>Build Date</name>
	/// <syntax>builddate</syntax>
	/// <summary>Sends a message with the build date of TP.</summary>
	[Command(@"(?:builddate|version)")]
	public static void BuildDate(string user, bool isWhisper)
	{
		DateTime date = Updater.GetCurrentBuildDateTime();
		IRCConnection.SendMessage($"Date and time this version of TP was built: {date:yyyy-MM-dd HH:mm:ss} UTC", user, !isWhisper);
	}

	/// <name>Read Setting</name>
	/// <syntax>readsetting [setting]</syntax>
	/// <summary>Reads a setting.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(?:read|write|change|set) *settings? +(\S+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void ReadSetting([Group(1)] string settingName, string user, bool isWhisper) => IRCConnection.SendMessage(TwitchPlaySettings.GetSetting(settingName), user, !isWhisper);

	/// <name>Write Setting</name>
	/// <syntax>writesetting [setting] [value]</syntax>
	/// <summary>Writes a setting to a specified value.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"(?:write|change|set) *settings? +(\S+) +(.+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void WriteSetting([Group(1)] string settingName, [Group(2)] string newValue, string user, bool isWhisper)
	{
		var result = TwitchPlaySettings.ChangeSetting(settingName, newValue);
		IRCConnection.SendMessage(result.Second, user, !isWhisper);
		if (result.First)
			TwitchPlaySettings.WriteDataToFile();
	}

	/// <name>Read Module Information</name>
	/// <syntax>readmodule [information] [module]</syntax>
	/// <summary>Reads the information for a module.</summary>
	[Command(@"read *module *(help(?: *message)?|manual(?: *code)?|score|points|statuslight|(?:camera *|module *)?pin *allowed|strike(?: *penalty)|colou?r|(?:valid *)?commands|unclaimable|announce(?:ment| *module)?) +(.+)")]
	public static void ReadModuleInformation([Group(1)] string command, [Group(2)] string parameter, string user, bool isWhisper)
	{
		var modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleDisplayName.ContainsIgnoreCase(parameter)).ToList();
		switch (modules.Count)
		{
			case 0:
				modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleID.ContainsIgnoreCase(parameter)).ToList();
				if (modules.Count == 1) goto case 1;
				if (modules.Count > 1)
				{
					var onemoduleID = modules.Where(x => x.moduleID.EqualsIgnoreCase(parameter)).ToList();
					if (onemoduleID.Count == 1)
					{
						modules = onemoduleID;
						goto case 1;
					}
					goto default;
				}

				IRCConnection.SendMessage($@"{parameter}というモジュールは見つかりませんでした。", user, !isWhisper);
				break;

			case 1:
				var moduleName = $"“{modules[0].moduleDisplayName}” ({modules[0].moduleID})";
				switch (command.ToLowerInvariant())
				{
					case "help":
					case "helpmessage":
					case "help message":
						IRCConnection.SendMessage($"モジュール{moduleName}のヘルプメッセージ: {modules[0].helpText}", user, !isWhisper);
						break;
					case "manual":
					case "manualcode":
					case "manual code":
						IRCConnection.SendMessage($"モジュール{moduleName}のマニュアルページ: {(string.IsNullOrEmpty(modules[0].manualCode) ? modules[0].moduleDisplayName : modules[0].manualCode)}", user, !isWhisper);
						break;
					case "points":
					case "score":
						IRCConnection.SendMessage($"モジュール{moduleName}のスコア: {modules[0].ScoreExplanation}", user, !isWhisper);
						break;
					case "statuslight":
						IRCConnection.SendMessage($"モジュール{moduleName}のステータスライトの位置: {modules[0].statusLightPosition}", user, !isWhisper);
						break;
					case "module pin allowed":
					case "camera pin allowed":
					case "module pinallowed":
					case "camera pinallowed":
					case "modulepin allowed":
					case "camerapin allowed":
					case "modulepinallowed":
					case "camerapinallowed":
					case "pinallowed":
					case "pin allowed":
						IRCConnection.SendMessage($"モジュール{moduleName}のカメラ固定: {(modules[0].CameraPinningAlwaysAllowed ? "常に許可" : "通常禁止")}", user, !isWhisper);
						break;
					case "color":
					case "colour":
						var moduleColor = JsonConvert.SerializeObject(TwitchPlaySettings.data.UnclaimedColor, Formatting.None, new ColorConverter());
						if (modules[0].unclaimedColor != new Color())
							moduleColor = JsonConvert.SerializeObject(modules[0].unclaimedColor, Formatting.None, new ColorConverter());
						IRCConnection.SendMessage($"モジュール{moduleName}の非割り当て状態のときの色: {moduleColor}", user, !isWhisper);
						break;
					case "commands":
					case "valid commands":
					case "validcommands":
						IRCConnection.SendMessage($"モジュール{moduleName}の有効なコマンド: {modules[0].validCommands}", user, !isWhisper);
						break;
					case "announcemodule":
					case "announce module":
					case "announce":
					case "announcement":
						IRCConnection.SendMessage($"モジュール{moduleName}の爆弾開始時のアナウンス: {(modules[0].announceModule ? "する" : "しない")}", user, !isWhisper);
						break;
					case "unclaimable":
						IRCConnection.SendMessage($"モジュール{moduleName}の割り当て禁止: {(modules[0].unclaimable ? "禁止" : "許可")}", user, !isWhisper);
						break;
				}
				break;

			default:
				var oneModule = modules.Where(x => x.moduleDisplayName.EqualsIgnoreCase(parameter)).ToList();
				if (oneModule.Count == 1)
				{
					modules = oneModule;
					goto case 1;
				}

				IRCConnection.SendMessage($"検索文字列に一致するモジュールが複数見つかりました: {modules.Take(5).Select(x => $"“{x.moduleDisplayName}” ({x.moduleID})").Join(", ")}", user, !isWhisper);
				break;
		}
	}

	/// <name>Write Module Information</name>
	/// <syntax>writemodule [information] [module] [value]</syntax>
	/// <summary>Writes the information for a module to a specified value.</summary>
	/// <restriction>Admin</restriction>
	[Command(@"(?:write|change|set) *module *(help(?: *message)?|manual(?: *code)?|score|points|statuslight|(?:camera *|module *)?pin *allowed|strike(?: *penalty)|colou?r|unclaimable|announce(?:ment| *module)?) +(.+);(.*)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void WriteModuleInformation([Group(1)] string command, [Group(2)] string search, [Group(3)] string changeTo, string user, bool isWhisper)
	{
		var modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleDisplayName.ContainsIgnoreCase(search)).ToList();
		switch (modules.Count)
		{
			case 0:
				modules = ComponentSolverFactory.GetModuleInformation().Where(x => x.moduleID.ContainsIgnoreCase(search)).ToList();
				if (modules.Count == 1)
					goto case 1;
				if (modules.Count > 1)
				{
					var onemoduleID = modules.Where(x => x.moduleID.Equals(search, StringComparison.InvariantCultureIgnoreCase)).ToList();
					if (onemoduleID.Count == 1)
					{
						modules = onemoduleID;
						goto case 1;
					}
					goto default;
				}

				IRCConnection.SendMessage($"{search}というモジュールは見つかりませんでした。", user, !isWhisper);
				break;

			case 1:
				var module = modules[0];
				var moduleName = $"“{module.moduleDisplayName}” ({module.moduleID})";
				var defaultModule = ComponentSolverFactory.GetDefaultInformation(module.moduleID);
				switch (command.ToLowerInvariant())
				{
					case "help":
					case "helpmessage":
					case "help message":
						if (string.IsNullOrEmpty(changeTo))
						{
							module.helpTextOverride = false;
							module.helpText = defaultModule.helpText;
						}
						else
						{
							module.helpText = changeTo;
							module.helpTextOverride = true;
						}
						IRCConnection.SendMessage($"モジュール{moduleName}のヘルプメッセージは次に書き換わりました: {module.helpText}", user, !isWhisper);
						break;
					case "manual":
					case "manualcode":
					case "manual code":
						if (string.IsNullOrEmpty(changeTo))
						{
							module.manualCodeOverride = false;
							module.manualCode = defaultModule.manualCode;
						}
						else
						{
							module.manualCode = changeTo;
							module.manualCodeOverride = true;
						}

						IRCConnection.SendMessage($"モジュール{moduleName}のマニュアルは次に書き換わりました: {(string.IsNullOrEmpty(module.manualCode) ? module.moduleDisplayName : module.manualCode)}", user, !isWhisper);
						break;
					case "points":
					case "score":
						var fileModule = Array.Find(ModuleData.LastRead, info => info.moduleID == module.moduleID);
						if (fileModule != null)
						{
							fileModule.scoreString = changeTo;
							module.scoreString = changeTo;
							module.scoreStringOverride = true;
							IRCConnection.SendMessage($"モジュール{moduleName}のスコアは次に書き換わりました: {module.scoreString}", user, !isWhisper);
						}
						break;
					case "statuslight":
						switch (changeTo.ToLowerInvariant())
						{
							case "bl":
							case "bottomleft":
							case "bottom left":
								module.statusLightPosition = StatusLightPosition.BottomLeft;
								break;
							case "br":
							case "bottomright":
							case "bottom right":
								module.statusLightPosition = StatusLightPosition.BottomRight;
								break;
							case "tr":
							case "topright":
							case "top right":
								module.statusLightPosition = StatusLightPosition.TopRight;
								break;
							case "tl":
							case "topleft":
							case "top left":
								module.statusLightPosition = StatusLightPosition.TopLeft;
								break;
							case "c":
							case "center":
								module.statusLightPosition = StatusLightPosition.Center;
								break;
							default:
								module.statusLightPosition = StatusLightPosition.Default;
								break;
						}
						IRCConnection.SendMessage($"モジュール{moduleName}のステータスライトの位置は次に書き換わりました: {module.statusLightPosition}", user, !isWhisper);
						break;
					case "module pin allowed":
					case "camera pin allowed":
					case "module pinallowed":
					case "camera pinallowed":
					case "modulepin allowed":
					case "camerapin allowed":
					case "modulepinallowed":
					case "camerapinallowed":
					case "pinallowed":
					case "pin allowed":
						module.CameraPinningAlwaysAllowed = changeTo.ContainsIgnoreCase("true") || changeTo.ContainsIgnoreCase("yes");
						IRCConnection.SendMessage($"モジュール{moduleName}のカメラ固定が次に変更されました: {(modules[0].CameraPinningAlwaysAllowed ? "常に許可" : "通常禁止")}", user, !isWhisper);
						break;
					case "color":
					case "colour":
						string moduleColor;
						try
						{
							var newModuleColor = SettingsConverter.Deserialize<Color>(changeTo);
							moduleColor = newModuleColor == new Color()
								? JsonConvert.SerializeObject(modules[0].unclaimedColor, Formatting.None, new ColorConverter())
								: changeTo;
							module.unclaimedColor = newModuleColor == new Color()
								? defaultModule.unclaimedColor
								: newModuleColor;
						}
						catch
						{
							moduleColor = JsonConvert.SerializeObject(TwitchPlaySettings.data.UnclaimedColor, Formatting.None, new ColorConverter());
							if (defaultModule.unclaimedColor != new Color())
								moduleColor = JsonConvert.SerializeObject(modules[0].unclaimedColor, Formatting.None, new ColorConverter());
							module.unclaimedColor = defaultModule.unclaimedColor;
						}

						IRCConnection.SendMessage($"モジュール{moduleName}の非割り当て時の色が次に変化しました: {moduleColor}", user, !isWhisper);
						break;
					case "announcemodule":
					case "announce module":
					case "announce":
					case "announcement":
						module.announceModule = changeTo.ContainsIgnoreCase("true") || changeTo.ContainsIgnoreCase("yes");
						IRCConnection.SendMessage($"モジュール{moduleName}の爆弾開始時のアナウンスを次に変更しました: {(modules[0].announceModule ? "する" : "しない")}", user, !isWhisper);
						break;
					case "unclaimable":
						module.unclaimable = changeTo.ContainsIgnoreCase("true") || changeTo.ContainsIgnoreCase("yes");
						IRCConnection.SendMessage($"モジュール{moduleName}の割り当て禁止は次に書き換わりました: {(modules[0].unclaimable ? "禁止" : "許可")}", user, !isWhisper);
						break;
				}
				ModuleData.DataHasChanged = true;
				ModuleData.WriteDataToFile();

				break;
			default:
				var onemodule = modules.Where(x => x.moduleDisplayName.Equals(search)).ToList();
				if (onemodule.Count == 1)
				{
					modules = onemodule;
					goto case 1;
				}

				IRCConnection.SendMessage($"Sorry, there is more than one module matching your search term. They are: {modules.Take(5).Select(x => $"“{x.moduleDisplayName}” ({x.moduleID})").Join(", ")}", user, !isWhisper);
				break;
		}
	}

	/// <name>Reset Setting</name>
	/// <syntax>resetsetting [setting]</syntax>
	/// <summary>Resets a setting back to it's default value.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"(?:erase|remove|reset) ?settings? (\S+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void ResetSetting([Group(1)] string parameter, string user, bool isWhisper)
	{
		var result = TwitchPlaySettings.ResetSettingToDefault(parameter);
		IRCConnection.SendMessage($"{result.Second}", user, !isWhisper);
		if (result.First)
			TwitchPlaySettings.WriteDataToFile();
	}

	/// <name>Timeout User with Reason</name>
	/// <syntax>timeout [user] [length] [reason]</syntax>
	/// <summary>Temporarily bans a user from TP including a reason for the ban.</summary>
	/// <argument name="length">How long the user should be banned for in seconds.</argument>
	/// <restriction>Mod</restriction>
	[Command(@"timeout +(\S+) +(\d+) +(.+)")]
	public static void BanUser([Group(1)] string userToBan, [Group(2)] int banTimeout, [Group(3)] string reason, string user, bool isWhisper) => UserAccess.TimeoutUser(userToBan, user, reason, banTimeout, isWhisper);

	/// <name>Timeout User</name>
	/// <syntax>timeout [user] [length]</syntax>
	/// <summary>Temporarily bans a user from TP.</summary>
	/// <argument name="length">How long the user should be banned for in seconds.</argument>
	/// <restriction>Mod</restriction>
	[Command(@"timeout +(\S+) +(\d+)")]
	public static void BanUserForNoReason([Group(1)] string userToBan, [Group(2)] int banTimeout, string user, bool isWhisper) => UserAccess.TimeoutUser(userToBan, user, null, banTimeout, isWhisper);

	/// <name>Ban User with Reason</name>
	/// <syntax>ban [user] [reason]</syntax>
	/// <summary>Bans a user from TP including a reason for the ban.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"ban +(\S+) +(.+)")]
	public static void BanUser([Group(1)] string userToBan, [Group(2)] string reason, string user, bool isWhisper) => UserAccess.BanUser(userToBan, user, reason, isWhisper);

	/// <name>Ban User</name>
	/// <syntax>ban [user]</syntax>
	/// <summary>Bans a user from TP.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"ban +(\S+)")]
	public static void BanUserForNoReason([Group(1)] string userToBan, string user, bool isWhisper) => UserAccess.BanUser(userToBan, user, null, isWhisper);

	/// <name>Unban User</name>
	/// <syntax>unban [user]</syntax>
	/// <summary>Unbans a user from TP.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"unban +(\S+)")]
	public static void UnbanUser([Group(1)] string userToUnban, string user, bool isWhisper) => UserAccess.UnbanUser(userToUnban, user, isWhisper);

	/// <name>Is Banned</name>
	/// <syntax>isbanned [users]</syntax>
	/// <summary>Checks if the specified users are banned.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(isbanned|banstats|bandata) +(\S+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void IsBanned([Group(1)] string usersToCheck, string user, bool isWhisper)
	{
		bool found = false;
		var bandata = UserAccess.GetBans();
		foreach (string person in usersToCheck.Split(';'))
		{
			string adjperson = person.Trim();
			if (bandata.Keys.Contains(adjperson))
			{
				bandata.TryGetValue(adjperson, out var value);
				if (double.IsPositiveInfinity(value.BanExpiry))
					IRCConnection.SendMessage($"User: {adjperson}, banned by: {value.BannedBy}{(string.IsNullOrEmpty(value.BannedReason) ? $", for the follow reason: {value.BannedReason}." : ".")} This ban is permanent.", user, !isWhisper);
				else
					IRCConnection.SendMessage($"User: {adjperson}, banned by: {value.BannedBy}{(string.IsNullOrEmpty(value.BannedReason) ? $", for the follow reason: {value.BannedReason}." : ".")} Ban duration left: {value.BanExpiry - DateTime.Now.TotalSeconds()}.", user, !isWhisper);
				found = true;
			}
		}
		if (!found)
			IRCConnection.SendMessage("The specified user has no ban data.", user, !isWhisper);
	}

	/// <name>Add Good Player</name>
	/// <syntax>addgood [user]</syntax>
	/// <summary>Adds a player to the good team for versus mode.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"addgood (.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddGood([Group(1)] string targetUser)
	{
		Leaderboard.Instance.MakeGood(targetUser);
		if (TwitchPlaySettings.data.AutoSetVSModeTeams) TwitchGame.Instance.GoodPlayers.Add(targetUser);
		IRCConnection.SendMessage($"@{targetUser}が紅組に割り当てられました。");
	}

	/// <name>Add Evil Player</name>
	/// <syntax>addevil [user]</syntax>
	/// <summary>Adds a player to the evil team for versus mode.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"addevil (.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddEvil([Group(1)] string targetUser)
	{
		Leaderboard.Instance.MakeEvil(targetUser);
		if (TwitchPlaySettings.data.AutoSetVSModeTeams) TwitchGame.Instance.EvilPlayers.Add(targetUser);
		IRCConnection.SendMessage($"@{targetUser}が白組に割り当てられました。");
	}

	/// <name>Join Versus</name>
	/// <syntax>join</syntax>
	/// <summary>Joins either versus mode team.</summary>
	[Command(@"join")]
	public static void JoinAnyTeam(string user)
	{
		bool _inGame = TwitchGame.Instance.VSSetFlag;

		if (!OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage($"@{user} - VSモードは有効ではありません。");
			return;
		}
		if (!TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			IRCConnection.SendMessage($"@{user} - チームは手動で割り当てられます。チームを指定してください。");
			return;
		}
		if (TwitchGame.Instance.VSModePlayers.Values.Contains(user))
		{
			IRCConnection.SendMessage($@"{user} - すでに次のVSモードに参加予定です。");
			return;
		}
		if (TwitchGame.Instance.GoodPlayers.Contains(user) || TwitchGame.Instance.EvilPlayers.Contains(user))
		{
			string team = TwitchGame.Instance.GoodPlayers.Contains(user) ? "紅組" : "白組";
			IRCConnection.SendMessage($@"{user} - すでに{team}に割り当てられています。");
			return;
		}
		if (_inGame && !TwitchPlaySettings.data.VSModePlayerLockout)
		{
			AddVSPlayer(user);
			return;
		}

		var trueRank = Leaderboard.Instance.GetTrueRank(user);
		TwitchGame.Instance.VSModePlayers.Add(trueRank, user);
		IRCConnection.SendMessage($"@{user}　- {(_inGame ? "爆弾はすでに起動しています。" : "")}次の爆弾に参加予定となりました。");
	}

	/// <name>Clear Versus Players</name>
	/// <syntax>clearvsplayers</syntax>
	/// <summary>Clears out the players for versus mode.</summary>
	/// <restriction>Admin</restriction>
	[Command(@"clearvsplayers", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ClearVSPlayers()
	{
		TwitchGame.Instance.VSModePlayers.Clear();
		IRCConnection.SendMessage("VSモードのプレイヤーがリセットされました。");
	}

	/// <name>Versus Players</name>
	/// <syntax>players</syntax>
	/// <summary>Sends a message to chat with the players in each team.</summary>
	[Command(@"players")]
	public static void ReadTeams()
	{
		if (!TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			IRCConnection.SendMessage("現在のモードではこのコマンドを利用できません。");
			return;
		}
		else if (!TwitchGame.Instance.VSSetFlag || TwitchGame.Instance.GoodPlayers.Count == 0 || TwitchGame.Instance.EvilPlayers.Count == 0)
		{
			var _vsCount = TwitchGame.Instance.VSModePlayers.Count;
			if (_vsCount < 2) IRCConnection.SendMessage($"開始するための十分なプレイヤーがいません。(現在{_vsCount}人)");
			else
			{
				string _vsPlayers = string.Join(", @", TwitchGame.Instance.VSModePlayers.Values.ToArray());
				IRCConnection.SendMessage($"{_vsCount}人がVSモードに参加予定です。プレイヤーリスト: {_vsPlayers}");
			}
			return;
		}

		var _gCount = TwitchGame.Instance.GoodPlayers.Count;
		var _eCount = TwitchGame.Instance.EvilPlayers.Count;
		string _gPlayers = string.Join(", @", TwitchGame.Instance.GoodPlayers.ToArray());
		string _ePlayers = string.Join(", @", TwitchGame.Instance.EvilPlayers.ToArray());
		IRCConnection.SendMessage($"{_gCount}人が紅組に参加予定です。プレイヤーリスト: @{_gPlayers}");
		IRCConnection.SendMessage($"{_eCount}人が白組に参加予定です。プレイヤーリスト: @{_ePlayers}");
	}

	/// <name>Join Team</name>
	/// <syntax>join [team]</syntax>
	/// <summary>Joins either the good or evil team for versus mode.</summary>
	[Command(@"join (evil|good|red|white|紅組|白組)")]
	public static void JoinWantedTeam([Group(1)] string team, string user, bool isWhisper)
	{
		OtherModes.Team target;
		switch(team) {
			case "evil":
			case "白組":
			case "white":
				target = OtherModes.Team.Evil;
				break;
			default:
				target = OtherModes.Team.Good;
				break;
		}
		Leaderboard.Instance.GetRank(user, out Leaderboard.LeaderboardEntry entry);
		if (TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			IRCConnection.SendMessage($"@{user} - チームは自動的に割り当てられます。!joinコマンドで参加してください。");
			return;
		}
		// ReSharper disable once SwitchStatementMissingSomeCases
		switch (target)
		{
			case OtherModes.Team.Good:
				if (entry != null && entry.Team == OtherModes.Team.Good)
				{
					IRCConnection.SendMessage($"@{user} - すでに紅組に参加予定です。", user, !isWhisper);
					return;
				}

				if (!Leaderboard.Instance.IsTeamBalanced(OtherModes.Team.Good))
				{
					IRCConnection.SendMessage(
						$"@{user} - 紅組の人数が多すぎるので紅組に参加できません。後でもう一度試してみ{(entry.Team != OtherModes.Team.Evil ? "るか、白組に参加し" : "")}てください。",
						user, !isWhisper);
					return;
				}
				Leaderboard.Instance.MakeGood(user);
				IRCConnection.SendMessage($"@{user}が紅組に参加しました。", user, !isWhisper);
				break;
			case OtherModes.Team.Evil:
				if (entry != null && entry.Team == OtherModes.Team.Evil)
				{
					IRCConnection.SendMessage($"@{user} - すでに白組に参加予定です。", user, !isWhisper);
					return;
				}

				if (!Leaderboard.Instance.IsTeamBalanced(OtherModes.Team.Evil))
				{
					IRCConnection.SendMessage(
						$"@{user} - 白組の人数が多すぎるので白組に参加できません。後でもう一度試してみ{(entry.Team != OtherModes.Team.Evil ? "るか、紅組に参加し" : "")}てください。",
						user, !isWhisper);
					return;
				}
				Leaderboard.Instance.MakeEvil(user);
				IRCConnection.SendMessage($"@{user}が白組に参加しました。", user, !isWhisper);
				break;
		}
	}

	/// <name>Add/Remove Rank</name>
	/// <syntax>add [username] [rank]\nremove [username] [rank]</syntax>
	/// <summary>Adds or removes a user from a rank. [rank] can be multiple ranks seperated by spaces.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(add|remove) +(\S+) +(.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddRemoveRole([Group(1)] string command, [Group(2)] string targetUser, [Group(3)] string roles, string user, bool isWhisper)
	{
		bool stepdown = command.Equals("remove", StringComparison.InvariantCultureIgnoreCase) && targetUser.Equals(user, StringComparison.InvariantCultureIgnoreCase);
		if (!stepdown && !UserAccess.HasAccess(user, AccessLevel.Mod, true))
			return;

		var level = AccessLevel.User;
		foreach (string lvl in roles.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			switch (lvl)
			{
				case "mod":
				case "moderator":
					level |= (stepdown || UserAccess.HasAccess(user, AccessLevel.SuperUser, true)) ? AccessLevel.Mod : AccessLevel.User;
					break;
				case "admin":
				case "administrator":
					level |= (stepdown || UserAccess.HasAccess(user, AccessLevel.SuperUser, true)) ? AccessLevel.Admin : AccessLevel.User;
					break;
				case "superadmin":
				case "superuser":
				case "super-user":
				case "super-admin":
				case "super-mod":
				case "supermod":
					level |= (stepdown || UserAccess.HasAccess(user, AccessLevel.SuperUser, true)) ? AccessLevel.SuperUser : AccessLevel.User;
					break;

				case "defuser":
					level |= AccessLevel.Defuser;
					break;

				case "no-points":
				case "no-score":
				case "noscore":
				case "nopoints":
					level |= UserAccess.HasAccess(user, AccessLevel.Mod, true) ? AccessLevel.NoPoints : AccessLevel.User;
					break;
			}
		}

		if (level == AccessLevel.User)
			return;

		if (command.EqualsIgnoreCase("add"))
		{
			UserAccess.AddUser(targetUser, level);
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.AddedUserPower, level, targetUser), user, !isWhisper);
		}
		else
		{
			UserAccess.RemoveUser(targetUser, level);
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.RemoveUserPower, level, targetUser), user, !isWhisper);
		}
		UserAccess.WriteAccessList();
	}

	/// <name>Moderators</name>
	/// <syntax>moderators</syntax>
	/// <summary>If enabled, sends to chat a list of users who have the moderator rank or above.</summary>
	[Command(@"(tpmods|moderators)")]
	public static void Moderators(string user, bool isWhisper)
	{
		if (!TwitchPlaySettings.data.EnableModeratorsCommand)
		{
			IRCConnection.SendMessage("The moderators command has been disabled.", user, !isWhisper);
			return;
		}
		KeyValuePair<string, AccessLevel>[] moderators = UserAccess.GetUsers().Where(x => !string.IsNullOrEmpty(x.Key) && x.Key != "_usernickname1" && x.Key != "_usernickname2" && x.Key != (TwitchPlaySettings.data.TwitchPlaysDebugUsername.StartsWith("_") ? TwitchPlaySettings.data.TwitchPlaysDebugUsername.ToLowerInvariant() : "_" + TwitchPlaySettings.data.TwitchPlaysDebugUsername.ToLowerInvariant())).ToArray();
		string finalMessage = "Current moderators: ";

		string[] streamers = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.Streamer).OrderBy(x => x.Key).Select(x => x.Key).ToArray();
		string[] superusers = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.SuperUser).OrderBy(x => x.Key).Select(x => x.Key).ToArray();
		string[] administrators = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.Admin).OrderBy(x => x.Key).Select(x => x.Key).ToArray();
		string[] mods = moderators.Where(x => UserAccess.HighestAccessLevel(x.Key) == AccessLevel.Mod).OrderBy(x => x.Key).Select(x => x.Key).ToArray();

		if (streamers.Length > 0)
			finalMessage += $"Streamers: {streamers.Join(", ")}{(superusers.Length > 0 || administrators.Length > 0 || mods.Length > 0 ? " - " : "")}";
		if (superusers.Length > 0)
			finalMessage += $"Super Users: {superusers.Join(", ")}{(administrators.Length > 0 || mods.Length > 0 ? " - " : "")}";
		if (administrators.Length > 0)
			finalMessage += $"Administrators: {administrators.Join(", ")}{(mods.Length > 0 ? " - " : "")}";
		if (mods.Length > 0)
			finalMessage += $"Moderators: {mods.Join(", ")}";

		IRCConnection.SendMessage(finalMessage, user, !isWhisper);
	}

	/// <name>Get Access</name>
	/// <syntax>getaccess [users]</syntax>
	/// <summary>Gets the access levels of the specified users.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"(getaccess|accessstats|accessdata) +(.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void GetAccess([Group(2)] string targetUsers, string user, bool isWhisper)
	{
		foreach (string person in targetUsers.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
			IRCConnection.SendMessage(string.Format("ユーザー{0}のアクセスレベル: {1}", person, UserAccess.LevelToString(UserAccess.HighestAccessLevel(person))), user, !isWhisper);
	}

	/// <name>Run Help</name>
	/// <syntax>run</syntax>
	/// <summary>Sends information about how to use the run command to chat.</summary>
	[Command(@"run")]
	public static void RunHelp()
	{
		string[] validDistributions = TwitchPlaySettings.data.ModDistributionSettings.Where(x => x.Value.Enabled && !x.Value.Hidden).Select(x => x.Key).ToArray();
		IRCConnection.SendMessage(validDistributions.Length > 0
			? $"使用方法: !run <モジュール数> <分配> | 有効な分配: {validDistributions.Join(", ")}"
			: "!runは無効化されています。");
	}

	/// <name>Run VS</name>
	/// <syntax>run [modules] [distribution] [goodhp] [evilhp]</syntax>
	/// <summary>Runs a versus mode bomb.</summary>
	[Command(@"run +(\d+) +(.*) +(\d+) +(\d+)")]
	public static IEnumerator RunVSHP(string user, bool isWhisper, [Group(1)] int modules,
	[Group(2)] string distributionName, [Group(3)] int GoodHP, [Group(4)] int EvilHP, KMGameInfo inf) => RunWrapper(
	user, isWhisper,
	() =>
	{
		if (!TwitchPlaySettings.data.ModDistributionSettings.TryGetValue(distributionName, out var distribution))
		{
			IRCConnection.SendMessage($"{distributionName}という分配は存在しません。");
			return null;
		}
		if (TwitchPlaySettings.data.AutoSetVSModeTeams)
		{
			if (TwitchGame.Instance.VSModePlayers.Count < 2)
			{
				IRCConnection.SendMessage("Not enough players for VSMode");
				return null;
			}

			string[] allPlayers = TwitchGame.Instance.VSModePlayers.Values.ToArray();

			if (TwitchPlaySettings.data.VSModeBalancedTeams)
			{
				for (int i = 0; i < allPlayers.Length; i++) AddVSPlayer(allPlayers[i]);
			}
			else
			{
				int goodCount = allPlayers.Length * TwitchPlaySettings.data.VSModeGoodSplit / 100;

				if (allPlayers.Length < 4)
				{
					AddGood(allPlayers[0]);
				}
				else
				{
					for (int i = 0; i < goodCount; i++) AddGood(allPlayers[i]);
				}

				for (int i = TwitchGame.Instance.GoodPlayers.Count; i < allPlayers.Length; i++) AddEvil(allPlayers[i]);
			}
			TwitchGame.Instance.VSSetFlag = true;
			TwitchGame.Instance.VSModePlayers.Clear();
		}
		else
		{
			if (!Leaderboard.Instance.IsAnyEvil())
			{
				IRCConnection.SendMessage("There are no evil players designated, the VS bomb cannot be run");
				return null;
			}

			if (!Leaderboard.Instance.IsAnyGood())
			{
				IRCConnection.SendMessage("There are no good players designated, the VS bomb cannot be run");
				return null;
			}
		}

		OtherModes.goodHealth = GoodHP;
		OtherModes.evilHealth = EvilHP;

		return RunDistribution(user, modules, inf, distribution);
	}, true);

	/// <name>Assign Any</name>
	/// <syntax>assignany [user]</syntax>
	/// <summary>Assigns a user to a versus mode team. Tries to keep the teams balanced.</summary>
	/// <restriction>Mod</restriction>
	[Command(@"assignany (.+)", AccessLevel.Mod, AccessLevel.Mod)]
	public static void AddVSPlayer([Group(1)] string targetUser)
	{
		int diff = TwitchGame.Instance.GoodPlayers.Count - TwitchGame.Instance.EvilPlayers.Count;
		if (diff > 1)
		{
			AddEvil(targetUser);
		}
		else if (diff < -1)
		{
			AddGood(targetUser);
		}
		else
		{
			int rand = Random.Range(0, 2);
			if (rand == 0)
			{
				AddEvil(targetUser);
			}
			else
			{
				AddGood(targetUser);
			}
		}
	}

	/// <name>Run Specific</name>
	/// <syntax>run [distribution] [modules]</syntax>
	/// <summary>Runs a distribution with a set number of modules. [distribution] can be vanilla, light, mixed, heavy and mods. There are also a few combinations like mixedlight and extralight. Which goes from all vanilla to all modded modules.</summary>
	[Command(@"run +(.*) +(\d+)")]
	public static IEnumerator RunSpecific(string user, bool isWhisper, [Group(1)] string distributionName, [Group(2)] int modules, KMGameInfo inf) => RunSpecific(user, isWhisper, modules, distributionName, inf);
	[Command(@"run +(\d+) +(.*)")]
	public static IEnumerator RunSpecific(string user, bool isWhisper, [Group(1)] int modules, [Group(2)] string distributionName, KMGameInfo inf) => RunWrapper(user, isWhisper, () =>
	{
		if (!TwitchPlaySettings.data.ModDistributionSettings.TryGetValue(distributionName, out var distribution))
		{
			IRCConnection.SendMessage($"{distributionName}という分配は存在しません。");
			return null;
		}

		if (OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage("Sorry, you cannot use this format of run when VS mode is on");
			return null;
		}
		return RunDistribution(user, modules, inf, distribution);
	});

	/// <name>Run Mission</name>
	/// <syntax>run [mission name]</syntax>
	/// <summary>Runs a named mission. Mods can give any mission ID to run.</summary>
	[Command(@"run +(?!.* +\d+$|\d+ +.*$)(.+)")]
	public static IEnumerator RunMission(string user, bool isWhisper, [Group(1)] string textAfter, KMGameInfo inf) => RunWrapper(user, isWhisper, () =>
	{
		if (OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage("You cannot run missions when VS mode is on");
			return null;
		}

		string missionID = null;
		string failureMessage = null;
		if (UserAccess.HasAccess(user, AccessLevel.Mod, true))
			missionID = ResolveMissionID(inf, textAfter, out failureMessage);

		if (missionID == null && TwitchPlaySettings.data.CustomMissions.ContainsKey(textAfter))
			missionID = ResolveMissionID(inf, TwitchPlaySettings.data.CustomMissions[textAfter], out failureMessage);

		if (missionID == null)
		{
			IRCConnection.SendMessage(failureMessage);
			return null;
		}

		return RunMissionCoroutine(missionID);
	});

	/// <name>Run Raw</name>
	/// <syntax>runraw [mission id]</syntax>
	/// <summary>Runs a mission by it's full ID. Examples: mod_TwitchPlays_tpFMNHell or firsttime. Will softlock if required modules are mission or ID is incorrect.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"runraw +(.+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator RunRaw([Group(1)] string missionName) => RunMissionCoroutine(missionName);

	/// <name>Run Raw Seed</name>
	/// <syntax>runrawseed [seed] [mission id]</syntax>
	/// <summary>The same as Run Raw but allows you to specify a seed.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"runrawseed +(\d+) +(.+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator RunRawSeed([Group(1)] string seed, [Group(2)] string missionName) => RunMissionCoroutine(missionName, seed);

	/// <name>Profile Help</name>
	/// <syntax>profile help</syntax>
	/// <summary>Gives you help on the profile commands.</summary>
	[Command(@"profiles? help")]
	public static void ProfileHelp(string user, bool isWhisper) =>
		IRCConnection.SendMessage("!profile enable <名前> [プロファイルを有効化する] | !profile disable <名前> [プロファイルを無効化する] | !profile enabled [有効なプロファイル一覧] | !profile list [プロファイル一覧] ", user, !isWhisper);

	/// <name>Profile Enable</name>
	/// <syntax>profile enable [name]</syntax>
	/// <summary>Enables a profile.</summary>
	[Command(@"profiles? +(?:enable|activate) +(.+)")]
	public static void ProfileEnable([Group(1)] string profileName, string user, bool isWhisper) => ProfileWrapper(profileName, user, isWhisper, (filename, profileString) =>
	{
		IRCConnection.SendMessage(ProfileHelper.Add(filename) ?
			$"次のプロファイルが有効化されました: {profileString}." :
			string.Format(TwitchPlaySettings.data.ProfileActionUseless, profileString, "enabled"), user, !isWhisper);
	});

	/// <name>Profile Disable</name>
	/// <syntax>profile disable [name]</syntax>
	/// <summary>Disables a profile.</summary>
	[Command(@"profiles? +(?:disable|deactivate) +(.+)")]
	public static void ProfileDisable([Group(1)] string profileName, string user, bool isWhisper) => ProfileWrapper(profileName, user, isWhisper, (filename, profileString) =>
	{
		IRCConnection.SendMessage(ProfileHelper.Remove(filename) ?
			$"次のプロファイルが無効化されました: {profileString}." :
			string.Format(TwitchPlaySettings.data.ProfileActionUseless, profileString, "disabled"), user, !isWhisper);
	});

	/// <name>Profile Enabled</name>
	/// <syntax>profile enabled</syntax>
	/// <summary>Lists out the enabled profiles.</summary>
	[Command(@"profiles? +enabled(?:list)?")]
	public static void ProfilesListEnabled(string user, bool isWhisper) => IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.ProfileListEnabled, ProfileHelper.Profiles.Select(str => str.Replace('_', ' ')).Intersect(TwitchPlaySettings.data.ProfileWhitelist).DefaultIfEmpty("(none)").Join(", ")), user, !isWhisper);

	/// <name>Profile List</name>
	/// <syntax>profile list</syntax>
	/// <summary>Lists out all the profiles available.</summary>
	[Command(@"profiles? +(?:list|all)?")]
	public static void ProfilesListAll(string user, bool isWhisper) => IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.ProfileListAll, TwitchPlaySettings.data.ProfileWhitelist.Join(", ")), user, !isWhisper);

	/// <name>Profile Add/Remove Module</name>
	/// <syntax>profile add [module] [profile]\nprofile remove [module] [profile]</syntax>
	/// <summary>Adds or removes a module from a profile. [module] can be a partial module name or ID and can be surrounded with quotes if the name has a space. [profile] can be a partial profile name.</summary>
	/// <restriction>Admin</restriction>
	[Command("profiles? +(?:(add)|remove) +(\"?)(.+)\\2 +(.+)", AccessLevel.Admin, AccessLevel.Admin)]
	public static void ProfileModule(string user, bool isWhisper, [Group(1)] bool adding, [Group(3)] string module, [Group(4)] string profileName)
	{
		if (!ComponentSolverFactory.GetModuleInformation().Search(module, modInfo => modInfo.moduleDisplayName, out ModuleInformation moduleInfo, out string message) &&
			!ComponentSolverFactory.GetModuleInformation().Search(module, modInfo => modInfo.moduleID, out moduleInfo, out message))
		{
			IRCConnection.SendMessage(message);
			return;
		}

		if (!Directory.GetFiles(ProfileHelper.ProfileFolder, "*.json").Search(profileName, path => Path.GetFileNameWithoutExtension(path).Replace('_', ' '), out string profilePath, out message))
		{
			IRCConnection.SendMessage(message);
			return;
		}

		var cleanProfileName = Path.GetFileNameWithoutExtension(profilePath).Replace('_', ' ');
		var success = ProfileHelper.SetState(profilePath, moduleInfo.moduleID, !adding);
		IRCConnection.SendMessage(success ?
			$"{moduleInfo.moduleTranslatedName ?? moduleInfo.moduleDisplayName}が{cleanProfileName}{(adding ? "に追加" : "から消去")}されました。" :
			$"{moduleInfo.moduleTranslatedName ?? moduleInfo.moduleDisplayName}はすでに{cleanProfileName}に存在{(adding ? "します" : "していません")}。",
			user, !isWhisper
		);
	}

	/// <name>Profile Disabled By</name>
	/// <syntax>profile disabled by [name]</syntax>
	/// <summary>Gets the modules disabled by a profile.</summary>
	[Command(@"profiles? +disabled +by +(.+)")]
	public static void ProfileDisabledBy([Group(1)] string profileName, string user, bool isWhisper) => ProfileWrapper(profileName, user, isWhisper, (filename, profileString) =>
	{
		var moduleIDs = ComponentSolverFactory.GetModuleInformation().Select(modInfo => modInfo.moduleID);
		var profilePath = Path.Combine(ProfileHelper.ProfileFolder, filename + ".json");
		var modules = ProfileHelper.GetProfile(profilePath).DisabledList.Where(modID => moduleIDs.Contains(modID));
		IRCConnection.SendMessage($"{profileString}によって無効化されているモジュール: {modules.Join(", ")}");
	});

	/// <name>Holdables</name>
	/// <syntax>holdables</syntax>
	/// <summary>Sends the list of available holdables to chat.</summary>
	[Command(@"holdables")]
	public static void Holdables(string user, bool isWhisper) => IRCConnection.SendMessage("The following holdables are present: {0}", user, !isWhisper, TwitchPlaysService.Instance.Holdables.Keys.Select(x => $"!{x}").Join(", "));

	/// <name>Disable Moderators</name>
	/// <syntax>disablemods</syntax>
	/// <summary>Disables all permission granted by the moderator rank.</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"disablemods", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void DisableModerators()
	{
		UserAccess.ModeratorsEnabled = false;
		IRCConnection.SendMessage("All moderators temporarily disabled.");
	}

	/// <name>Enable Moderators</name>
	/// <syntax>enablemods</syntax>
	/// <summary>Enables all permission granted by the moderator rank.</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"enablemods", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void EnableModerators()
	{
		UserAccess.ModeratorsEnabled = true;
		IRCConnection.SendMessage("All moderators restored.");
	}

	/// <name>Reload Data</name>
	/// <syntax>reloaddata</syntax>
	/// <summary>Reloads all the data that TP uses. Like settings, access levels, etc.</summary>
	/// <restriction>SuperUser</restriction>
	[Command("reloaddata", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator ReloadData(string user, bool isWhisper)
	{
		bool streamer = UserAccess.HasAccess(user, AccessLevel.Streamer);
		bool superuser = UserAccess.HasAccess(user, AccessLevel.SuperUser);

		TwitchPlaySettings.LoadDataFromFile();
		UserAccess.LoadAccessList();
		yield return ComponentSolverFactory.LoadDefaultInformation(true);

		if (streamer)
			UserAccess.AddUser(user, AccessLevel.Streamer);
		if (superuser)
			UserAccess.AddUser(user, AccessLevel.SuperUser);

		IRCConnectionManagerHoldable.TwitchPlaysDataRefreshed = true;
		IRCConnection.SendMessage("Data reloaded", user, !isWhisper);
	}

	/// <name>Reload Score Info</name>
	/// <syntax>reloadscoreinfo</syntax>
	/// <summary>Reloads the scoring info of all modules.</summary>
	/// <restriction>ScoringManager</restriction>
	[Command("reloadscoreinfo", AccessLevel.ScoringManager, AccessLevel.ScoringManager)]
	public static IEnumerator ReloadScoreInfo(string user, bool isWhisper)
	{
		yield return ComponentSolverFactory.LoadDefaultInformation(true);

		IRCConnection.SendMessage("Score info reloaded", user, !isWhisper);
	}


	/// <name>Silence Mode</name>
	/// <syntax>silencemode</syntax>
	/// <summary>Toggles silence mode. Silence mode prevents TP from sending messages to chat.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"silencemode", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void SilenceMode() => IRCConnection.ToggleSilenceMode();

	/// <name>Elevator</name>
	/// <syntax>elevator</syntax>
	/// <summary>Sends the current state of the elevator into the chat.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"elevator", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void Elevator() => TPElevatorSwitch.Instance?.ReportState();

	/// <name>Change Elevator</name>
	/// <syntax>elevator on\nelevator off\nelevator toggle</syntax>
	/// <summary>Changes the enabled state of the elevator.</summary>
	[Command(@"elevator (on|off|flip|toggle|switch|press|push)")]
	public static IEnumerator Elevator([Group(1)] string command)
	{
		if (TPElevatorSwitch.Instance == null || TPElevatorSwitch.Instance.ElevatorSwitch == null || !TPElevatorSwitch.Instance.ElevatorSwitch.gameObject.activeInHierarchy)
			return null;

		var on = TPElevatorSwitch.IsON;
		switch (command)
		{
			case "on" when !TPElevatorSwitch.IsON:
			case "off" when TPElevatorSwitch.IsON:
			case "flip":
			case "toggle":
			case "switch":
			case "press":
			case "push":
				on = !on;
				break;
			case "on":
			case "off":
				TPElevatorSwitch.Instance.ReportState();
				return null;
		}

		return TPElevatorSwitch.Instance.ToggleSetupRoomElevatorSwitch(on);
	}

	private static readonly HashSet<string> confirming = new HashSet<string>();

	/// <name>Opt out</name>
	/// <syntax>optout</syntax>
	/// <summary>Opts out of having your rank and points being displayed.</summary>
	[Command(@"opt[- ]?out")]
	public static void OptOut(string user)
	{
		if (!confirming.Contains(user))
		{
			confirming.Add(user);
			IRCConnection.SendMessage("Are you sure that you want to opt out? You cannot undo this action. Use !optout again to confirm.");
			return;
		}

		confirming.Remove(user);

		Leaderboard.Instance.OptOut(user);
		IRCConnection.SendMessage($"{user} has opted out.");
	}

	/// <name>Restart</name>
	/// <syntax>restart</syntax>
	/// <summary>Restarts the game by closing and reopening it.</summary>
	/// <restriction>SuperUser</restriction>
	[Command("(?:restart|reboot)(?:game)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void RestartGame()
	{
		if (SteamManager.Initialized) // If the game was launched through Steam, we have to relaunch it with Steam services.
		{
			// Creating this file will allow the game to use Steam services.
			// This file cannot be deleted until the game tries to initialize Steam.
			// Since that can't be determined from the outside, TwitchPlaysService will delete it after Steam initializes.
			File.WriteAllText("steam_appid.txt", "341800");

			Process.Start(Process.GetCurrentProcess().MainModule.FileName, Environment.GetCommandLineArgs().Skip(1).Join());

			Application.Quit();
		}
		else
		{
			// The game can only normally have one instance open because the boot.config file has the single-instance argument in it.
			// To get around that we'll remove the argument from the file and then replace the original contents after the second instance launches.

			string bootConfigPath = Path.Combine(Application.dataPath, "boot.config");
			string originalContents = File.ReadAllText(bootConfigPath);
			File.WriteAllText(bootConfigPath, originalContents.Replace("single-instance=", ""));

			Process
				.Start(Process.GetCurrentProcess().MainModule.FileName, Environment.GetCommandLineArgs().Skip(1).Join())
				.WaitForInputIdle(); // Wait until the game is accepting input so we don't put back the original contents too early.

			File.WriteAllText(bootConfigPath, originalContents);

			Application.Quit();
		}
	}

	/// <name>Quit</name>
	/// <syntax>quit</syntax>
	/// <summary>Quits KTANE.</summary>
	/// <restriction>SuperUser</restriction>
	[Command("(?:quit|end)(?:game)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void QuitGame() => SceneManager.Instance.Exit();

	/// <name>Check For Updates</name>
	/// <syntax>checkforupdates</syntax>
	/// <summary>Checks to see if there is a new build of TP on Dropbox. Only applicable if you are using the Dropbox version of TP.</summary>
	/// <restriction>SuperUser</restriction>
	[Command("(?:checkforupdates?|cfu)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator CheckForUpdates()
	{
		yield return null;
		IRCConnection.SendMessage("自動アップデートは無効化されています。");
	}

	/// <name>Update</name>
	/// <syntax>update\nupdate force</syntax>
	/// <summary>If there is an update available, installs it and restarts the game. Adding the word "force" will update regardless if there is an update available. Only applicable if you are using the Dropbox version of TP.</summary>
	/// <restriction>SuperUser</restriction>
	[Command("update(?:game|tp|twitchplays)?( force)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator Update([Group(1)] bool force) => Updater.Update(force);

	[Command("revert(?:game|tp|twitchplays)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static IEnumerator Revert() => Updater.Revert();

	/// <name>Reset Leaderboard</name>
	/// <syntax>leaderboard reset</syntax>
	/// <summary>Resets all the information on the leaderboard.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"leaderboard reset", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void ResetLeaderboard(string user, bool isWhisper)
	{
		Leaderboard.Instance.ResetLeaderboard();
		IRCConnection.SendMessage("Leaderboard Reset.", user, !isWhisper);
	}

	/// <name>Disable Whitelist</name>
	/// <syntax>disablewhitelist</syntax>
	/// <summary>Disables the whitelist.</summary>
	[Command(@"disablewhitelist", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void DisableWhitelist()
	{
		TwitchPlaySettings.data.EnableWhiteList = false;
		TwitchPlaySettings.WriteDataToFile();
		TwitchPlaysService.Instance.UpdateUiHue();
		IRCConnection.SendMessage("Whitelist disabled.");
	}

	/// <name>Enable Whitelist</name>
	/// <syntax>enablewhitelist</syntax>
	/// <summary>Enables the whitelist.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"enablewhitelist", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void EnableWhitelist()
	{
		TwitchPlaySettings.data.EnableWhiteList = true;
		TwitchPlaySettings.WriteDataToFile();
		TwitchPlaysService.Instance.UpdateUiHue();
		IRCConnection.SendMessage("Whitelist enabled.");
	}

	/// <name>Mimic</name>
	/// <syntax>mimic [player] [command]</syntax>
	/// <summary>Makes it seem like another player ran the specified command. Only works with players of the same rank or lower.</summary>
	/// <restriction>SuperUser</restriction>
	[Command(@"(?:issue|say|mimic)(?: ?commands?)?(?: ?as)? (\S+) (.+)", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void Mimic([Group(1)] string targetPlayer, [Group(2)] string newMessage, IRCMessage message)
	{
		if (message.IsWhisper)
		{
			IRCConnection.SendMessage($"Sorry {message.UserNickName}, issuing commands as other users is not allowed in whispers", message.UserNickName, false);
			return;
		}

		if (UserAccess.HighestAccessLevel(message.UserNickName) < UserAccess.HighestAccessLevel(targetPlayer))
		{
			IRCConnection.SendMessage($"Sorry {message.UserNickName}, you may not issue commands as {targetPlayer}");
			return;
		}

		IRCConnection.ReceiveMessage(targetPlayer, message.UserColorCode, newMessage);
	}

	/// <name>Skip Command</name>
	/// <syntax>skipcommand</syntax>
	/// <summary>Forcibly skips the currently running command. It is only recommended to use this to skip a command that is stuck. This may cause issues and should be used with caution.</summary>
	/// <restriction>SuperUser</restriction>
	[Command("skip(?:coroutine|command|cmd)?", AccessLevel.SuperUser, AccessLevel.SuperUser)]
	public static void Skip()
	{
		TwitchPlaysService.Instance.CoroutineQueue.SkipCurrentCoroutine = true;
	}

	/// <name>Run as</name>
	/// <syntax>runas [color] [username]#[discrminator] [command]</syntax>
	/// <summary>Runs a command as the specified Discord user</summary>
	/// <restriction>Streamer</restriction>
	[Command(@"runas (#.{6}) (.*?)#([0-9]{4}) (!.*?)$", AccessLevel.Streamer, AccessLevel.Streamer)]
	public static void RunCommandAs([Group(1)] string color, [Group(2)] string username, [Group(3)] string discriminator, [Group(4)] string command) =>
		IRCConnection.ReceiveMessage($"{username}#{discriminator}", color, command);

	//As of now, Debugging commands are streamer only, apart from whispertest, which are superuser and above.
	[Command("whispertest", AccessLevel.SuperUser, AccessLevel.SuperUser), DebuggingOnly]
	public static void WhisperTest(string user) => IRCConnection.SendMessage("Test successful", user, false);

	[Command("secondary camera", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void EnableSecondaryCamera() => GameRoom.ToggleCamera(false);

	[Command("main camera", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void EnableMainCamera() => GameRoom.ToggleCamera(true);

	[Command(@"(move|rotate) ?camera ?([xyz]) (-?[0-9]+(?:\\.[0-9]+)*)", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void ChangeCamera([Group(1)] string action, [Group(2)] string axis, [Group(3)] float number, string user, bool isWhisper)
	{
		if (GameRoom.IsMainCamera)
		{
			IRCConnection.SendMessage("Please switch to the secondary camera using \"!secondary camera\" before attempting to move it.", user, !isWhisper);
			return;
		}

		Vector3 vector = new Vector3();
		switch (axis)
		{
			case "x": vector = new Vector3(number, 0, 0); break;
			case "y": vector = new Vector3(0, number, 0); break;
			case "z": vector = new Vector3(0, 0, number); break;
		}

		switch (action)
		{
			case "move": GameRoom.MoveCamera(vector); break;
			case "rotate": GameRoom.RotateCamera(vector); break;
		}

		CameraChanged(user, isWhisper);
	}

	[Command("reset ?camera", AccessLevel.Streamer, AccessLevel.Streamer), DebuggingOnly]
	public static void ResetCamera(string user, bool isWhisper)
	{
		GameRoom.ResetCamera();
		CameraChanged(user, isWhisper);
	}

	[Command(null)]
	public static bool DefaultCommand(string cmd, string user, bool isWhisper)
	{
		if (!TwitchPlaySettings.data.GeneralCustomMessages.ContainsKey(cmd.ToLowerInvariant()))
			return
				TwitchPlaySettings.data.IgnoreCommands
					.Contains(cmd.ToLowerInvariant()); //Ignore the command if it's in IgnoreCommands
		IRCConnection.SendMessage(TwitchPlaySettings.data.GeneralCustomMessages[cmd.ToLowerInvariant()], user, !isWhisper);
		return true;
	}

	#region Private methods
	private static void SetGameMode(TwitchPlaysMode mode, bool toggle, bool on, string user, bool isWhisper, bool enabledForEveryone, string disabledMessage)
	{
		if (!UserAccess.HasAccess(user, AccessLevel.Mod, true) && !enabledForEveryone && !TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage(string.Format(disabledMessage, user), user, !isWhisper);
			return;
		}

		if (toggle)
			OtherModes.Toggle(mode);
		else
			OtherModes.Set(mode, on);
		IRCConnection.SendMessage($"次のゲームは{OtherModes.GetName(OtherModes.nextMode)}モードになります。", user, !isWhisper);
	}

	private static void ShowRank(Leaderboard.LeaderboardEntry entry, string targetUser, string user, bool isWhisper, bool numeric = false) => ShowRank(entry == null ? null : new[] { entry }, targetUser, user, isWhisper, numeric);

	private static void ShowRank(IList<Leaderboard.LeaderboardEntry> entries, string targetUser, string user, bool isWhisper, bool numeric = false)
	{
		if (entries != null)
		{
			entries = entries.Where(entry => entry != null).ToList();
			if (entries.Count == 0)
			{
				entries = null;
			}
		}

		if (entries == null && numeric)
			IRCConnection.SendMessage(TwitchPlaySettings.data.RankTooLow, user, !isWhisper);
		else if (entries == null)
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.DoYouEvenPlayBro, targetUser), user, !isWhisper);
		else
		{
			foreach (var entry in entries)
			{
				string txtSolver = "";
				string txtSolo = "";
				if (entry.TotalSoloClears > 0)
				{
					var recordTimeSpan = TimeSpan.FromSeconds(entry.RecordSoloTime);
					txtSolver = TwitchPlaySettings.data.SolverAndSolo;
					txtSolo = string.Format(TwitchPlaySettings.data.SoloRankQuery, entry.SoloRank, (int) recordTimeSpan.TotalMinutes, recordTimeSpan.Seconds);
				}
				IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.RankQuery, entry.UserName, entry.OptOut ? "--" : entry.Rank.ToString(), entry.SolveCount, entry.StrikeCount, txtSolver, txtSolo, entry.OptOut ? "--" : entry.SolveScore.ToString("0.##")), user, !isWhisper);
			}
		}
	}

	private static int GetMaximumModules(KMGameInfo inf, int maxAllowed = int.MaxValue) => Math.Min(TPElevatorSwitch.IsON ? 54 : inf.GetMaximumBombModules(), maxAllowed);

	private static string ResolveMissionID(KMGameInfo inf, string targetID, out string failureMessage)
	{
		failureMessage = null;
		var missions = ModManager.Instance.ModMissions;

		var mission = missions.Find(x => x.name.EqualsIgnoreCase(targetID)) ??
			missions.Find(x => Regex.IsMatch(x.name, $"^mod_.+_{Regex.Escape(targetID)}", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase));
		if (mission == null)
		{
			failureMessage = $"Unable to find a mission with an ID of “{targetID}”.";
			return null;
		}

		var availableMods = inf.GetAvailableModuleInfo().Where(x => x.IsMod).Select(y => y.ModuleId).ToList();
		if (MultipleBombs.Installed())
			availableMods.Add("Multiple Bombs");
		var missingMods = new HashSet<string>();
		var modules = ComponentSolverFactory.GetModuleInformation().ToList();

		var generatorSetting = mission.GeneratorSetting;
		var componentPools = generatorSetting.ComponentPools;
		int moduleCount = 0;
		foreach (var componentPool in componentPools)
		{
			moduleCount += componentPool.Count;
			var modTypes = componentPool.ModTypes;
			if (modTypes == null || modTypes.Count == 0) continue;
			foreach (string mod in modTypes.Where(x => !availableMods.Contains(x)))
			{
				missingMods.Add(modules.Find(x => x.moduleID == mod)?.moduleDisplayName ?? mod);
			}
		}
		if (missingMods.Count > 0)
		{
			failureMessage = $"Mission \"{targetID}\" was found, however, the following mods are not installed / loaded: {string.Join(", ", missingMods.OrderBy(x => x).ToArray())}";
			return null;
		}
		if (moduleCount > GetMaximumModules(inf))
		{
			failureMessage = TPElevatorSwitch.IsON
				? $"Mission “{targetID}” was found; however, this mission has too many modules to use in the elevator."
				: $"Mission “{targetID}” was found; however, there is no bomb case with at least {moduleCount} modules currently installed and enabled.";
			return null;
		}

		return mission.name;
	}

	private static IEnumerator RunWrapper(string user, bool isWhisper, Func<IEnumerator> action, bool VSOnly = false)
	{
		yield return null;
		if (TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.PostGame && TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.Setup)
		{
			IRCConnection.SendMessage("You can't use the !run command right now.");
			yield break;
		}

		if (VSOnly && !OtherModes.VSModeOn)
		{
			IRCConnection.SendMessage("That formatting can only be used in VS mode.");
			yield break;
		}

		if (!((TwitchPlaySettings.data.EnableRunCommand && (!TwitchPlaySettings.data.EnableWhiteList || UserAccess.HasAccess(user, AccessLevel.Defuser, true))) || UserAccess.HasAccess(user, AccessLevel.Mod, true) || TwitchPlaySettings.data.AnarchyMode) || isWhisper)
		{
			IRCConnection.SendMessageFormat(TwitchPlaySettings.data.RunCommandDisabled, user);
			yield break;
		}
		yield return action();
	}

	private static void ProfileWrapper(string profileName, string user, bool isWhisper, Action<string, string> action)
	{
		if (TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.PostGame
			&& TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.Setup
			&& TwitchPlaysService.Instance.CurrentState != KMGameInfo.State.Gameplay)
		{
			IRCConnection.SendMessage("You can't use a !profile command right now.");
			return;
		}

		var profileString = ProfileHelper.GetProperProfileName(profileName);
		if (TwitchPlaySettings.data.ProfileWhitelist.Contains(profileString))
			action(profileString.Replace(' ', '_'), profileString);
		else
			IRCConnection.SendMessage(string.Format(TwitchPlaySettings.data.ProfileNotWhitelisted, profileName), user, !isWhisper);
	}

	private static IEnumerator RunDistribution(string user, int modules, KMGameInfo inf, ModuleDistributions distribution)
	{
		if (!distribution.Enabled && !UserAccess.HasAccess(user, AccessLevel.Mod, true) && !TwitchPlaySettings.data.AnarchyMode)
		{
			IRCConnection.SendMessage($"分配{distribution.DisplayName}は無効化されています。");
			return null;
		}

		if (modules < distribution.MinModules)
		{
			IRCConnection.SendMessage($"分配{distribution.DisplayName}の最小のモジュール数は{distribution.MinModules}です。");
			return null;
		}

		int maxModules = GetMaximumModules(inf, distribution.MaxModules);
		if (modules > maxModules)
		{
			if (modules > distribution.MaxModules)
				IRCConnection.SendMessage($"分配{distribution.DisplayName}の最大のモジュール数は{distribution.MaxModules}です。");
			else
				IRCConnection.SendMessage($"最大のモジュール数は{maxModules}\"です。");
			return null;
		}

		var mission = ScriptableObject.CreateInstance<KMMission>();
		mission.PacingEventsEnabled = TwitchPlaySettings.data.PacingEventsOnRunBomb;
		mission.DisplayName = modules + " " + distribution.DisplayName;
		mission.Description = modules + " " + distribution.DisplayName;
		try
		{
			mission.GeneratorSetting = distribution.GenerateMission(modules, OtherModes.TimeModeOn, out int rewardPoints);
			rewardPoints = (rewardPoints * OtherModes.ScoreMultiplier).RoundToInt();
			TwitchPlaySettings.SetRewardBonus(rewardPoints);
			IRCConnection.SendMessage("爆弾を解除した時の報酬: " + rewardPoints);
		}
		catch (InvalidOperationException e)
		{
			IRCConnection.SendMessage($"Sorry, the distribution {distribution.DisplayName} cannot be run: {e.Message}");
			return null;
		}

		return RunMissionCoroutine(mission);
	}

	private static IEnumerator RunMissionCoroutine(KMMission mission, string seed = "-1")
	{
		if (TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.PostGame)
		{
			// Press the “back” button
			var e = PostGameCommands.Continue();
			while (e.MoveNext())
				yield return e;

			// Wait until we’re back in the setup room
			yield return new WaitUntil(() => TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.Setup);
		}

		TwitchPlaysService.Instance.GetComponent<KMGameCommands>().StartMission(mission, seed);
		OtherModes.RefreshModes(KMGameInfo.State.Transitioning);
	}

	private static IEnumerator RunMissionCoroutine(string missionId, string seed = "-1")
	{
		if (TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.PostGame)
		{
			// Press the “back” button
			var e = PostGameCommands.Continue();
			while (e.MoveNext())
				yield return e;

			// Wait until we’re back in the setup room
			yield return new WaitUntil(() => TwitchPlaysService.Instance.CurrentState == KMGameInfo.State.Setup);
		}

		TwitchPlaysService.Instance.GetComponent<KMGameCommands>().StartMission(missionId, seed);
		OtherModes.RefreshModes(KMGameInfo.State.Transitioning);
	}

	private static void CameraChanged(string user, bool isWhisper)
	{
		Transform camera = GameRoom.SecondaryCamera.transform;

		DebugHelper.Log($"Camera Position = {Math.Round(camera.localPosition.x, 3)},{Math.Round(camera.localPosition.y, 3)},{Math.Round(camera.localPosition.z, 3)}");
		DebugHelper.Log($"Camera Euler Angles = {Math.Round(camera.localEulerAngles.x, 3)},{Math.Round(camera.localEulerAngles.y, 3)},{Math.Round(camera.localEulerAngles.z, 3)}");
		IRCConnection.SendMessage($"Camera Position = {Math.Round(camera.localPosition.x, 3)},{Math.Round(camera.localPosition.y, 3)},{Math.Round(camera.localPosition.z, 3)}, Camera Euler Angles = {Math.Round(camera.localEulerAngles.x, 3)},{Math.Round(camera.localEulerAngles.y, 3)},{Math.Round(camera.localEulerAngles.z, 3)}", user, !isWhisper);
	}
	#endregion
}
