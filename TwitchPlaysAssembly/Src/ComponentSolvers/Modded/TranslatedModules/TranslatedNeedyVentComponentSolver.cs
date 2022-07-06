﻿using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public class TranslatedNeedyVentComponentSolver : ComponentSolver
{
	public TranslatedNeedyVentComponentSolver(TwitchModule module) :
		base(module)
	{
		_yesButton = (MonoBehaviour) YesButtonField.GetValue(module.BombComponent.GetComponent(NeedyVentComponentSolverType));
		_noButton = (MonoBehaviour) NoButtonField.GetValue(module.BombComponent.GetComponent(NeedyVentComponentSolverType));
		ModInfo = ComponentSolverFactory.GetModuleInfo(GetModuleType(), "!{0} yes, !{0} y [answer yes] | !{0} no, !{0} n [answer no]").Clone();

		LanguageCode = TranslatedModuleHelper.GetLanguageCode(module.BombComponent.GetComponent(NeedyVentComponentSolverType), NeedyVentComponentSolverType);
		ModInfo.moduleDisplayName = $"Needy Vent Gas Translated{TranslatedModuleHelper.GetModuleDisplayNameAddon(module.BombComponent.GetComponent(NeedyVentComponentSolverType), NeedyVentComponentSolverType)}";
		Module.HeaderText = ModInfo.moduleDisplayName;
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.ToLowerInvariant().Trim();
		if (inputCommand.EqualsAny("y", "yes", "press y", "press yes"))
		{
			yield return "yes";
			yield return DoInteractionClick(_yesButton);
		}
		else if (inputCommand.EqualsAny("n", "no", "press n", "press no"))
		{
			yield return "no";
			yield return DoInteractionClick(_noButton);
		}
	}

	private static readonly Type NeedyVentComponentSolverType = ReflectionHelper.FindType("VentGasTranslatedModule");
	private static readonly FieldInfo YesButtonField = NeedyVentComponentSolverType.GetField("YesButton", BindingFlags.Public | BindingFlags.Instance);
	private static readonly FieldInfo NoButtonField = NeedyVentComponentSolverType.GetField("NoButton", BindingFlags.Public | BindingFlags.Instance);

	private readonly MonoBehaviour _yesButton;
	private readonly MonoBehaviour _noButton;
}
