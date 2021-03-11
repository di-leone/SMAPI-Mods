﻿using Harmony;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TheLion.Common.Harmony;
using SObject = StardewValley.Object;

namespace TheLion.AwesomeProfessions.Framework.Patches
{
	internal class GameLocationGetFishPatch : BasePatch
	{
		private static ILHelper _helper;

		/// <summary>Construct an instance.</summary>
		/// <param name="config">The mod settings.</param>
		/// <param name="monitor">Interface for writing to the SMAPI console.</param>
		internal GameLocationGetFishPatch(ModConfig config, IMonitor monitor)
			: base(config, monitor)
		{
			_helper = new ILHelper(monitor);
		}

		/// <summary>Apply internally-defined Harmony patches.</summary>
		/// <param name="harmony">The Harmony instance for this mod.</param>
		protected internal override void Apply(HarmonyInstance harmony)
		{
			harmony.Patch(
				AccessTools.Method(typeof(GameLocation), nameof(GameLocation.getFish)),
				transpiler: new HarmonyMethod(GetType(), nameof(GameLocationGetFishTranspiler))
			);
		}

		/// <summary>Patch for Fisher to reroll reeled fish if first roll resulted in trash.</summary>
		protected static IEnumerable<CodeInstruction> GameLocationGetFishTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
		{
			_helper.Attach(instructions).Log($"Patching method {typeof(GameLocation)}::{nameof(GameLocation.getFish)}");

			Label reroll = iLGenerator.DefineLabel();
			Label resumeExecution = iLGenerator.DefineLabel();
			var hasRerolled = iLGenerator.DeclareLocal(typeof(bool));

			try
			{
				_helper
					.Insert(
						new CodeInstruction(OpCodes.Ldc_I4_0),
						new CodeInstruction(OpCodes.Stloc_S, operand: hasRerolled)
					)
					.FindLast(
						new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(SObject), new Type[] { typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int) }))
					)
					.RetreatUntil(
						new CodeInstruction(OpCodes.Ldloc_1)
					)
					.AddLabel(resumeExecution)
					.Insert(
						new CodeInstruction(OpCodes.Ldloc_1),
						new CodeInstruction(OpCodes.Ldarg_S, operand: (byte)4),				// arg 4 = Farmer who
						new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GameLocationGetFishPatch), nameof(GameLocationGetFishPatch._CanReroll))),
						new CodeInstruction(OpCodes.Brfalse_S, operand: resumeExecution),
						new CodeInstruction(OpCodes.Ldloc_S, operand: hasRerolled),
						new CodeInstruction(OpCodes.Brtrue_S, operand: resumeExecution),
						new CodeInstruction(OpCodes.Ldc_I4_1),
						new CodeInstruction(OpCodes.Stloc_S, operand: hasRerolled),
						new CodeInstruction(OpCodes.Br, operand: reroll)
					)
					.RetreatUntil(
						new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Utility), nameof(Utility.Shuffle)))
					)
					.Retreat(2)
					.AddLabel(reroll);
			}
			catch (Exception ex)
			{
				_helper.Error($"Failed while adding modded Fisher fish reroll.\nHelper returned {ex}").Restore();
			}

			return _helper.Flush();
		}

		/// <summary>Whether a given player is eligible for a fish reroll.</summary>
		/// <param name="index">An item index.</param>
		/// <param name="who">The player.</param>
		private static bool _CanReroll(int index, Farmer who)
		{
			return _IsTrash(index) && Utils.SpecificPlayerHasProfession("fisher", who);
		}

		/// <summary>Whether a given item index corresponds to trash.</summary>
		/// <param name="index">An item index.</param>
		private static bool _IsTrash(int index)
		{
			return index > 166 && index < 173;
		}
	}
}