using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Rules;
using UnityEngine;

public class ButtonComponentSolver : ComponentSolver
{
	public ButtonComponentSolver(TwitchModule module) :
		base(module)
	{
		ModuleInformation buttonInfo = ComponentSolverFactory.GetModuleInfo("ButtonComponentSolver", "!{0} tap [ボタンを押してすぐに離す] | !{0} hold [ボタンを長押しする] | !{0} release 7 [7が表示されている時にボタンを離す]");
		ModuleInformation buttonInfoModified = ComponentSolverFactory.GetModuleInfo("ButtonComponentModifiedSolver", "Click the button with !{0} tap. Click the button at a time with !{0} tap 8:55 8:44 8:33. Hold the button with !{0} hold. Release the button with !{0} release 9:58 9:49 9:30.");

		buttonInfo.moduleTranslatedName = "ボタン";
		buttonInfo.manualCode = "https://ktane.timwi.de/HTML/The%20Button%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E3%83%9C%E3%82%BF%E3%83%B3).html";

		var buttonModule = (ButtonComponent) module.BombComponent;
		buttonModule.GetComponent<Selectable>().OnCancel += buttonModule.OnButtonCancel;
		_button = buttonModule.button;
		ModInfo = VanillaRuleModifier.IsSeedVanilla() ? buttonInfo : buttonInfoModified;
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		bool isModdedSeed = VanillaRuleModifier.IsSeedModded();
		inputCommand = inputCommand.ToLowerInvariant().Trim();
		if (!_held && inputCommand.EqualsAny("tap", "click"))
		{
			yield return "tap";
			yield return DoInteractionClick(_button);
		}
		if (!_held && (inputCommand.StartsWith("tap ") ||
					   inputCommand.StartsWith("click ")))
		{
			if (!isModdedSeed)
				yield break;
			yield return "tap2";

			IEnumerator releaseCoroutine = ReleaseCoroutineModded(inputCommand.Substring(inputCommand.IndexOf(' ')));
			while (releaseCoroutine.MoveNext())
				yield return releaseCoroutine.Current;
		}
		else if (!_held && inputCommand.Equals("hold"))
		{
			yield return "hold";

			_held = true;
			DoInteractionStart(_button);
			yield return new WaitForSeconds(2.0f);
		}
		else if (_held)
		{
			string[] commandParts = inputCommand.Split(' ');
			if (commandParts.Length != 2 || !commandParts[0].Equals("release")) yield break;
			IEnumerator releaseCoroutine;

			if (!isModdedSeed)
			{
				if (!int.TryParse(commandParts[1], out int second))
					yield break;
				if (second >= 0 && second <= 9)
					releaseCoroutine = ReleaseCoroutineVanilla(second);
				else
					yield break;
			}
			else
				releaseCoroutine = ReleaseCoroutineModded(inputCommand.Substring(inputCommand.IndexOf(' ')));

			while (releaseCoroutine.MoveNext())
				yield return releaseCoroutine.Current;
		}
	}

	private IEnumerator ReleaseCoroutineVanilla(int second)
	{
		yield return "release";
		TimerComponent timerComponent = Module.Bomb.Bomb.GetTimer();
		string secondString = second.ToString();
		float timeRemaining = float.PositiveInfinity;
		while (timeRemaining > 0.0f && _held)
		{
			timeRemaining = timerComponent.TimeRemaining;

			if (Module.Bomb.CurrentTimerFormatted.Contains(secondString))
			{
				DoInteractionEnd(_button);
				_held = false;
			}

			yield return $"trycancel キャンセルが実行されたので、ボタンは{(_held ? "離" : "押")}されませんでした。";
		}
	}

	private IEnumerator ReleaseCoroutineModded(string second)
	{
		TimerComponent timerComponent = Module.Bomb.Bomb.GetTimer();
		int target = Mathf.FloorToInt(timerComponent.TimeRemaining);
		bool waitingMusic = true;

		string[] times = second.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
		List<int> result = new List<int>();

		foreach (string time in times)
		{
			string[] split = time.Split(':');
			int minutesInt = 0, hoursInt = 0, daysInt = 0;
			switch (split.Length)
			{
				case 1 when int.TryParse(split[0], out int secondsInt):
				case 2 when int.TryParse(split[0], out minutesInt) && int.TryParse(split[1], out secondsInt):
				case 3 when int.TryParse(split[0], out hoursInt) && int.TryParse(split[1], out minutesInt) && int.TryParse(split[2], out secondsInt):
				case 4 when int.TryParse(split[0], out daysInt) && int.TryParse(split[1], out hoursInt) && int.TryParse(split[2], out minutesInt) && int.TryParse(split[3], out secondsInt):
					result.Add(daysInt * 86400 + hoursInt * 3600 + minutesInt * 60 + secondsInt);
					break;
				default:
					yield break;
			}
		}
		yield return null;

		bool minutes = times.Any(x => x.Contains(":"));
		minutes |= result.Any(x => x >= 60);

		if (!minutes)
		{
			target %= 60;
			result = result.Select(x => x % 60).Distinct().ToList();
		}

		for (int i = result.Count - 1; i >= 0; i--)
		{
			int r = result[i];
			if (!minutes && !OtherModes.Unexplodable)
				waitingMusic &= target + (r > target ? 60 : 0) - r > 30;
			else if (!minutes)
				waitingMusic &= r + (r < target ? 60 : 0) - target > 30;
			else if (!OtherModes.Unexplodable)
			{
				if (r > target)
				{
					result.RemoveAt(i);
					continue;
				}

				waitingMusic &= target - r > 30;
			}
			else
			{
				if (r < target)
				{
					result.RemoveAt(i);
					continue;
				}

				waitingMusic &= r - target > 30;
			}
		}

		if (result.Count == 0)
		{
			yield return
				$"sendtochaterror The button was not {(_held ? "released" : "tapped")} because all of your specified times are {(OtherModes.Unexplodable ? "less" : "greater")} than the time remaining.";
			yield break;
		}

		if (waitingMusic)
			yield return "waiting music";

		while (result.All(x => x != target))
		{
			yield return $"trycancel The button was not {(_held ? "released" : "tapped")} due to a request to cancel.";
			target = (int) (timerComponent.TimeRemaining + (OtherModes.Unexplodable ? -0.25f : 0.25f));
			if (!minutes) target %= 60;
		}

		if (!_held)
			yield return DoInteractionClick(_button);
		else
			DoInteractionEnd(_button);

		_held = false;
	}

	private static Rule ForcedSolveRule()
	{
		Rule rule = new Rule();
		rule.Queries.Add(new Query { Property = ButtonForceSolveQuery, Args = new Dictionary<string, object>() });
		rule.SolutionArgs = new Dictionary<string, object>();
		rule.Solution = ButtonForceSolveSolution;
		return rule;
	}

	private static readonly QueryableButtonProperty ButtonForceSolveQuery = new QueryableButtonProperty
	{
		Name = "forcesolve",
		Text = "If the user with AccessLevel.Admin or higher has issued !# solve on this Big Button instance on Twitch Plays",
		QueryFunc = ((_, __) => true)
	};

	private static readonly Solution ButtonForceSolveSolution = new Solution
	{
		Text = "Force solve The Button.",
		SolutionMethod = (_, __) => 0
	};

	protected override IEnumerator ForcedSolveIEnumerator()
	{
		yield return null;
		ButtonRuleSet ruleset = RuleManager.Instance.ButtonRuleSet;
		ruleset.HoldRuleList.Insert(0, ForcedSolveRule());
		ruleset.RuleList.Insert(0, ForcedSolveRule());
		if (!_held)
			yield return DoInteractionClick(_button);
		else
		{
			DoInteractionEnd(_button);
			_held = false;
		}
		ruleset.HoldRuleList.RemoveAt(0);
		ruleset.RuleList.RemoveAt(0);
	}

	private readonly PressableButton _button;
	private bool _held;
}
