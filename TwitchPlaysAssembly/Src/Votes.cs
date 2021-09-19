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
	internal string name
	{
		get => Votes.CurrentVoteType == VoteTypes.Solve ? $"Solve module {Votes.voteModule.Code} ({Votes.voteModule.HeaderText})" : _name;
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
				name = "爆弾の起爆",
				validityChecks = new List<Tuple<Func<bool>, string>>
				{
					createCheck(() => TwitchGame.Instance.VoteDetonateAttempted, "@{0} - 投票による起爆はこのゲームで一度拒否されています。再び爆弾の起爆の投票を開始することはできません。")
				},
				onSuccess = () => TwitchGame.Instance.Bombs[0].CauseExplosionByVote()
			}
		},
		{
			VoteTypes.VSModeToggle, new VoteData {
				name = "VSモードの開始",
				validityChecks = null,
				onSuccess = () => {
					OtherModes.Toggle(TwitchPlaysMode.VS);
					IRCConnection.SendMessage($"次のゲームは{OtherModes.GetName(OtherModes.nextMode)}モードになります。");
				}
			}
		},
		{
			VoteTypes.Solve, new VoteData {
				validityChecks = new List<Tuple<Func<bool>, string>>
				{
					createCheck(() => !TwitchPlaySettings.data.EnableVoteSolve, "@{0} - 投票による自動解除は無効化されています。"),
					createCheck(() => voteModule.Solver.AttemptedForcedSolve, "@{0} - そのモジュールはすでに投票により自動解除されました。"),
					createCheck(() => OtherModes.currentMode == TwitchPlaysMode.VS, "@{0} - 投票による自動解除はVSモードでは無効化されています。"),
					createCheck(() => TwitchGame.Instance.VoteSolveCount >= 2, "@{0} - すでに2回の投票による自動解除が行われました。これ以上投票による自動解除を開始することはできません。"),
					createCheck(() =>
						voteModule.BombComponent.GetModuleID().IsBossMod() &&
						((double)TwitchGame.Instance.CurrentBomb.BombSolvedModules / TwitchGame.Instance.CurrentBomb.BombSolvableModules >= .10f ||
						TwitchGame.Instance.CurrentBomb.BombStartingTimer - TwitchGame.Instance.CurrentBomb.CurrentTimer < 120),
						"@{0} - ボスモジュールは10%未満のモジュールが解除されていて、かつ2分以上経過した後にのみ投票による自動解除が行えます。"),
					createCheck(() =>
						((double)TwitchGame.Instance.CurrentBomb.BombSolvedModuleIDs.Count(x => !x.IsBossMod()) /
						TwitchGame.Instance.CurrentBomb.BombSolvableModuleIDs.Count(x => !x.IsBossMod()) <= 0.75f) &&
						!voteModule.BombComponent.GetModuleID().IsBossMod(),
						"@{0} - 投票による自動解除は爆弾の75%以上のモジュールが解除された場合のみ開始することができます。"),
					createCheck(() => voteModule.Claimed, "@{0} - 投票による自動解除は割り当てが行われていないモジュールのみに行うことができます。"),
					createCheck(() => voteModule.ClaimQueue.Count > 0, "@{0} - そのモジュールは割り当ての予約がされているため、投票による自動解除を開始することができません。"),
					createCheck(() => (int)voteModule.ScoreMethods.Sum(x => x.CalculateScore(null)) <= 8 && !voteModule.BombComponent.GetModuleID().IsBossMod(), "@{0} - 投票による自動解除は点数が8以上のモジュールのみに行うことができます。"),
					createCheck(() => TwitchGame.Instance.CommandQueue.Any(x => x.Message.Text.StartsWith($"!{voteModule.Code} ")), "@{0} - そのモジュールのコマンドがキューされているため、投票による自動解除を開始することができません。"),
					createCheck(() => GameplayState.MissionToLoad != "custom", "@{0} - ミッション中には投票による自動解除を開始することができません。")
				},
				onSuccess = () =>
				{
					voteModule.Solver.SolveModule($"モジュール({voteModule.HeaderText})は自動的に解除されます。");
					TwitchPlaySettings.SetRewardBonus((TwitchPlaySettings.GetRewardBonus() * 0.75f).RoundToInt());
					IRCConnection.SendMessage($"モジュール{voteModule.Code} ({voteModule.HeaderText})が投票により自動解除され、報酬が25%減少しました。");
				}
			}
		}
	};

	private static readonly Dictionary<string, bool> Voters = new Dictionary<string, bool>();

	private static Coroutine voteInProgress = null;
	private static IEnumerator VotingCoroutine()
	{
		int oldTime;
		while (VoteTimeRemaining >= 0f)
		{
			oldTime = TimeLeft;
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

			IRCConnection.SendMessage($"割り当てが行われている{numAddedNoVotes}人が投票を行わなかったため、{numAddedNoVotes}票の反対票が追加されます。");
		}

		int yesVotes = Voters.Count(pair => pair.Value);
		bool votePassed = (yesVotes >= Voters.Count * (TwitchPlaySettings.data.MinimumYesVotes[CurrentVoteType] / 100f));
		IRCConnection.SendMessage($"投票の結果{yesVotes}/{Voters.Count}が賛成しました。投票は{(votePassed ? "可決" : "否決")}されました。");
		if (!votePassed && CurrentVoteType == VoteTypes.Solve)
			voteModule.SetBannerColor(voteModule.unclaimedBackgroundColor);
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
					TwitchGame.Instance.VoteSolveCount++;
					voteModule.SetBannerColor(voteModule.MarkedBackgroundColor);
					break;
			}
		}

		CurrentVoteType = act;
		VoteTimeRemaining = TwitchPlaySettings.data.VoteCountdownTime;
		Voters.Clear();
		Voters.Add(user, true);
		IRCConnection.SendMessage($"{user}により\"{PossibleVotes[CurrentVoteType].name}\"の投票が開始されました! 「!vote VoteYea」か「!vote VoteNay」で投票してください。");
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
			IRCConnection.SendMessage($"{user} - 現在投票は行われていません。");
			return;
		}

		if (Voters.ContainsKey(user) && Voters[user] == vote)
		{
			IRCConnection.SendMessage($"{user} - すでに{(vote ? "賛成" : "反対")}に投票しています。");
			return;
		}

		Voters[user] = vote;
		IRCConnection.SendMessage($"{user}は{(vote ? "賛成" : "反対")}しました。");
	}

	public static void RemoveVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"{user} - 現在投票は行われていません。");
			return;
		}

		if (!Voters.ContainsKey(user))
		{
			IRCConnection.SendMessage($"{user} - あなたはまだ投票していません。");
			return;
		}

		Voters.Remove(user);
		IRCConnection.SendMessage($"{user} - 投票を削除しました。");
	}
	#endregion

	public static void StartVote(string user, VoteTypes act, TwitchModule module = null)
	{
		if (!TwitchPlaySettings.data.EnableVoting)
		{
			IRCConnection.SendMessage($"@{user} - 投票は無効化されています。");
			return;
		}

		if (Active)
		{
			IRCConnection.SendMessage($"@{user} -　現在投票は進行中です");
			return;
		}

		CreateNewVote(user, act, module);
	}

	public static void TimeLeftOnVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"@{user} - 現在進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage($"{PossibleVotes[CurrentVoteType].name}の投票は残り{TimeLeft}秒です。");
	}

	public static void CancelVote(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"@{user} - 現在進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage("投票はキャンセルされました。");
		if (CurrentVoteType == VoteTypes.Solve)
			voteModule.SetBannerColor(voteModule.unclaimedBackgroundColor);
		DestroyVote();
	}

	public static void EndVoteEarly(string user)
	{
		if (!Active)
		{
			IRCConnection.SendMessage($"@{user} - 現在進行中の投票はありません。");
			return;
		}
		IRCConnection.SendMessage("投票は時間を切り上げて終了しました。");
		VoteTimeRemaining = 0f;
	}

	private static Tuple<Func<bool>, string> createCheck(Func<bool> func, string str) => new Tuple<Func<bool>, string>(func, str);
}