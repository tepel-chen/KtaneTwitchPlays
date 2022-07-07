using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoteTypes
{
	Detonation,
	VSModeToggle,
	Solve
}

public class VoteData
{
	// Name of the vote (Displayed over !notes3 when in game)
	internal string Name
	{
		get => Votes.CurrentVoteType == VoteTypes.Solve ? $"モジュール{Votes.voteModule.Code}({Votes.voteModule.TranslatedText})の解除" : _name;
		set => _name = value;
	}

	// Action to execute if the vote passes
	internal Action onSuccess;

	// Checks the validity of a vote
	internal List<Tuple<Func<bool>, string>> validityChecks;

	private string _name;
}

public static class Votes
{
	private static float VoteTimeRemaining = -1f;
	internal static VoteTypes CurrentVoteType;

	public static bool Active => voteInProgress != null;
	internal static int TimeLeft => Mathf.CeilToInt(VoteTimeRemaining);
	internal static int NumVoters => Voters.Count;

	internal static TwitchModule voteModule;

	internal static readonly Dictionary<VoteTypes, VoteData> PossibleVotes = new Dictionary<VoteTypes, VoteData>()
	{
		{
			VoteTypes.Detonation, new VoteData {
				Name = "爆弾を強制爆発",
				validityChecks = new List<Tuple<Func<bool>, string>>
				{
					CreateCheck(() => TwitchGame.Instance.VoteDetonateAttempted, "Sorry, {0}, a detonation vote was already attempted on this bomb. Another one cannot be started.")
				},
				onSuccess = () => TwitchGame.Instance.Bombs[0].CauseExplosionByVote()
			}
		},
		{
			VoteTypes.VSModeToggle, new VoteData {
				Name = "VSモードのOn/Off変更",
				validityChecks = null,
				onSuccess = () => {
					OtherModes.Toggle(TwitchPlaysMode.VS);
					IRCConnection.SendMessage($"{OtherModes.GetName(OtherModes.nextMode)} mode will be enabled next round.");
				}
			}
		},
		{
			VoteTypes.Solve, new VoteData {
				validityChecks = new List<Tuple<Func<bool>, string>>
				{
					CreateCheck(() => !TwitchPlaySettings.data.EnableVoteSolve, "{0} - モジュールの投票による解除は無効化されています。"),
					CreateCheck(() => voteModule.Votesolving, "{0} - そのモジュールの投票による解除はすでに進行中です"),
					CreateCheck(() => OtherModes.currentMode == TwitchPlaysMode.VS, "{0} -  モジュールの投票による解除はVSモードでは無効です。"),
					CreateCheck(() => TwitchGame.Instance.VoteSolveCount >= 2, "{0} - モジュールの投票による解除は二回までです。新しく投票を始めることができません。"),
					CreateCheck(() =>
						voteModule.BombComponent.GetModuleID().IsBossMod() &&
						((double)TwitchGame.Instance.CurrentBomb.BombSolvedModules / TwitchGame.Instance.CurrentBomb.BombSolvableModules >= .10f ||
						TwitchGame.Instance.CurrentBomb.BombStartingTimer - TwitchGame.Instance.CurrentBomb.CurrentTimer < 120),
						"{0} - ボスモジュールの投票による解除は、全モジュールの10%が解除される前、かつ開始2分後でのみ可能です。"),
					CreateCheck(() =>
						((double)TwitchGame.Instance.CurrentBomb.BombSolvedModuleIDs.Count(x => !x.IsBossMod()) /
						TwitchGame.Instance.CurrentBomb.BombSolvableModuleIDs.Count(x => !x.IsBossMod()) <= 0.75f) &&
						!voteModule.BombComponent.GetModuleID().IsBossMod(),
						"{0} - ボスモジュールでないモジュールの投票による解除は、全モジュールの75%が解除された後でのみ可能です。"),
					CreateCheck(() => voteModule.Claimed, "{0} - 割り当てされているモジュールの投票による解除は行えません。"),
					CreateCheck(() => voteModule.ClaimQueue.Count > 0, "{0} - 投票による解除を行おうとしたモジュールには、割り当てのキューがされています。"),
					CreateCheck(() => (int)voteModule.ScoreMethods.Sum(x => x.CalculateScore(null)) <= 8 && !voteModule.BombComponent.GetModuleID().IsBossMod(), "{0} - モジュールは8点以上でなければ投票による解除は行えません。"),
					CreateCheck(() => TwitchGame.Instance.CommandQueue.Any(x => x.Message.Text.StartsWith($"!{voteModule.Code} ")), "{0} - 投票による解除を行おうとしたモジュールには、コマンドのキューがされています。"),
					CreateCheck(() => GameplayState.MissionToLoad != "custom", "{0} - ミッション中にモジュールの投票による解除は行えません。")
				},
				onSuccess = () =>
				{
					voteModule.Solver.SolveModule($"モジュール({voteModule.TranslatedText})は自動で解除されます");
					voteModule.SetClaimedUserMultidecker("投票による自動解除");
					voteModule.Votesolving = true;
					TwitchPlaySettings.SetRewardBonus((TwitchPlaySettings.GetRewardBonus() * 0.75f).RoundToInt());
					IRCConnection.SendMessage($"モジュール{voteModule.Code}({voteModule.TranslatedText})が自動解除されたことにより、爆弾報酬が25%減少します。");
				}
			}
		}
	};

	private static readonly Dictionary<string, bool> Voters = new Dictionary<string, bool>();

	private static Coroutine voteInProgress;
	private static IEnumerator VotingCoroutine()
	{
		while (VoteTimeRemaining >= 0f)
		{
			var oldTime = TimeLeft;
			VoteTimeRemaining -= Time.deltaTime;

			if (TwitchGame.BombActive && TimeLeft != oldTime) // Once a second, update notes.
				TwitchGame.ModuleCameras.SetNotes();
			yield return null;
		}

		if (TwitchGame.BombActive && (CurrentVoteType == VoteTypes.Detonation || CurrentVoteType == VoteTypes.Solve))
		{
			// Add claimed users who didn't vote as "no"
			int numAddedNoVotes = 0;
			List<string> usersWithClaims = TwitchGame.Instance.Modules
				.Where(m => !m.Solved && m.PlayerName != null).Select(m => m.PlayerName).Distinct().ToList();
			foreach (string user in usersWithClaims)
			{
				if (!Voters.ContainsKey(user))
				{
					++numAddedNoVotes;
					Voters.Add(user, false);
				}
			}

			if (numAddedNoVotes == 1)
				IRCConnection.SendMessage("投票しなかった割り当てを持っているユーザーがいたため、1票の反対票が加わります。");
			else if (numAddedNoVotes > 1)
				IRCConnection.SendMessage($"投票しなかった割り当てを持っているユーザーが{numAddedNoVotes}人いたため、{numAddedNoVotes}票の反対票が加わります。");
		}

		int yesVotes = Voters.Count(pair => pair.Value);
		bool votePassed = (yesVotes >= Voters.Count * (TwitchPlaySettings.data.MinimumYesVotes[CurrentVoteType] / 100f));
		IRCConnection.SendMessage($"投票は{Voters.Count}中{yesVotes}の賛成で終わりました。投票は {(votePassed ? "成功" : "失敗")}しました。");
		if (!votePassed && CurrentVoteType == VoteTypes.Solve)
		{
			voteModule.SetBannerColor(voteModule.unclaimedBackgroundColor);
			voteModule.SetClaimedUserMultidecker(null);
		}
		if (votePassed)
		{
			PossibleVotes[CurrentVoteType].onSuccess();
		}

		DestroyVote();
	}

	private static void CreateNewVote(string user, VoteTypes act, TwitchModule module = null)
	{
		voteModule = module;
		if (TwitchGame.BombActive && act != VoteTypes.VSModeToggle)
		{
			if (act == VoteTypes.Solve && module == null)
				throw new InvalidOperationException("Module is null in a votesolve! This should not happen, please send this logfile to the TP developers!");

			var validity = PossibleVotes[act].validityChecks.Find(x => x.First());
			if (validity != null)
			{
				IRCConnection.SendMessage(string.Format(validity.Second, user));
				return;
			}

			switch (act)
			{
				case VoteTypes.Detonation:
					TwitchGame.Instance.VoteDetonateAttempted = true;
					break;
				case VoteTypes.Solve:
					if (voteModule is null)
						throw new InvalidOperationException("Votemodule cannot be null");
					TwitchGame.Instance.VoteSolveCount++;
					voteModule.SetBannerColor(voteModule.MarkedBackgroundColor);
					voteModule.SetClaimedUserMultidecker("VOTE IN PROGRESS");
					break;
			}
		}

		CurrentVoteType = act;
		VoteTimeRemaining = TwitchPlaySettings.data.VoteCountdownTime;
		Voters.Clear();
		Voters.Add(user, true);
		IRCConnection.SendMessage($"投票が{user}によって開始されました。({PossibleVotes[CurrentVoteType].Name})!vote VoteYeaか!vote VoteNayで投票に参加しましょう。");
		voteInProgress = TwitchPlaysService.Instance.StartCoroutine(VotingCoroutine());
		if (TwitchGame.Instance.alertSound != null)
			TwitchGame.Instance.alertSound.Play();
		if (TwitchGame.BombActive)
			TwitchGame.ModuleCameras.SetNotes();
	}

	private static void DestroyVote()
	{
		if (voteInProgress != null)
			TwitchPlaysService.Instance.StopCoroutine(voteInProgress);
		voteInProgress = null;
		Voters.Clear();
		voteModule = null;
		if (TwitchGame.BombActive)
			TwitchGame.ModuleCameras.SetNotes();
	}

	internal static void OnStateChange()
	{
		// Any ongoing vote ends.
		DestroyVote();
	}

	#region UserCommands
	public static void Vote(string user, bool vote)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user}, there is no vote currently in progress.");
			return;
		}

		if (Voters.ContainsKey(user) && Voters[user] == vote)
		{
			IRCConnection.SendMessage($"{user}, you've already voted {(vote ? "yes" : "no")}.");
			return;
		}

		Voters[user] = vote;
		IRCConnection.SendMessage($"{user} voted {(vote ? "yes" : "no")}.");
	}

	public static void RemoveVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user}, there is no vote currently in progress.");
			return;
		}

		if (!Voters.ContainsKey(user))
		{
			IRCConnection.SendMessage($"{user}, you haven't voted.");
			return;
		}

		Voters.Remove(user);
		IRCConnection.SendMessage($"{user} has removed their vote.");
	}
	#endregion

	public static void StartVote(string user, VoteTypes act, TwitchModule module = null)
	{
		if (!TwitchPlaySettings.data.EnableVoting)
		{
			IRCConnection.SendMessage($"{user} - 投票は無効化されています。");
			return;
		}

		if (Active)
		{
			IRCConnection.SendMessage($"{user} - 進行中の投票があります。");
			return;
		}

		CreateNewVote(user, act, module);
	}

	public static void TimeLeftOnVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user} - 進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage($"現在の投票({PossibleVotes[CurrentVoteType].Name})の残り時間は{TimeLeft}秒です。");
	}

	public static void CancelVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user} - 進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage("投票がキャンセルされました。");
		if (CurrentVoteType == VoteTypes.Solve)
		{
			voteModule.SetBannerColor(voteModule.unclaimedBackgroundColor);
			voteModule.SetClaimedUserMultidecker(null);
		}
		DestroyVote();
	}

	public static void EndVoteEarly(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user} - 進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage("投票が即座に終了されました。");
		VoteTimeRemaining = 0f;
	}

	private static Tuple<Func<bool>, string> CreateCheck(Func<bool> func, string str) => new Tuple<Func<bool>, string>(func, str);
}