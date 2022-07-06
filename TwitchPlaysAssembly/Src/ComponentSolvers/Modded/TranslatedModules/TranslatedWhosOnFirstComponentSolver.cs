﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class TranslatedWhosOnFirstComponentSolver : ComponentSolver
{
	public TranslatedWhosOnFirstComponentSolver(TwitchModule module) :
		base(module)
	{
		Component component = module.BombComponent.GetComponent(ComponentType);
		_buttons = (KMSelectable[]) ButtonsField.GetValue(component);
		ModInfo = ComponentSolverFactory.GetModuleInfo(GetModuleType(), "!{0} what? [press the button that says \"WHAT?\"] | !{0} press 3 [press the third button in english reading order] | The phrase must match exactly | Not case sensitive | If the language used asks for pressing a literally blank button, use \"!{0} literally blank\"").Clone();

		LanguageCode = TranslatedModuleHelper.GetLanguageCode(component, ComponentType);
		ModInfo.moduleDisplayName = $"Who's on First Translated{TranslatedModuleHelper.GetModuleDisplayNameAddon(component, ComponentType)}";
		Module.HeaderText = ModInfo.moduleDisplayName;
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.Trim();

		string[] split = inputCommand.ToLowerInvariant().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (split.Length == 2 && split[0] == "press" && int.TryParse(split[1], out int buttonIndex) && buttonIndex > 0 && buttonIndex < 7)
		{
			yield return null;
			yield return DoInteractionClick(_buttons[buttonIndex - 1]);
		}
		else
		{
			List<string> buttonLabels = _buttons.Select(button => button.GetComponentInChildren<TextMesh>().text.ToUpperInvariant()).ToList();

			if (inputCommand.Equals("literally blank", StringComparison.InvariantCultureIgnoreCase))
				inputCommand = "\u2003\u2003";

			if (LanguageCode == "he")   // The Hebrew version expects the input back-to-front
				inputCommand = inputCommand.Reverse().Join();

			int index = buttonLabels.IndexOf(inputCommand.ToUpperInvariant());
			if (index < 0)
			{
				yield return null;
				yield return buttonLabels.Any(label => label == " ")
					? "sendtochaterror The module is not ready for input yet."
					: $"sendtochaterror!f There isn't any label that contains \"{inputCommand.Replace("\u2003\u2003", "Literally Blank")}\".";
				yield break;
			}
			yield return null;
			yield return DoInteractionClick(_buttons[index]);
		}
	}

	private static readonly Type ComponentType = ReflectionHelper.FindType("WhosOnFirstTranslatedModule");
	private static readonly FieldInfo ButtonsField = ComponentType.GetField("buttons", BindingFlags.Public | BindingFlags.Instance);

	private readonly KMSelectable[] _buttons;
}
