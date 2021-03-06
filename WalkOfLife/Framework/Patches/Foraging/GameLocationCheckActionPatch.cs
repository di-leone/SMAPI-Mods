using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;
using TheLion.Stardew.Common.Harmony;
using TheLion.Stardew.Professions.Framework.Extensions;
using SObject = StardewValley.Object;

namespace TheLion.Stardew.Professions.Framework.Patches
{
	internal class GameLocationCheckActionPatch : BasePatch
	{
		/// <summary>Construct an instance.</summary>
		internal GameLocationCheckActionPatch()
		{
			Original = RequireMethod<GameLocation>(nameof(GameLocation.checkAction));
		}

		#region harmony patches

		/// <summary>
		///     Patch to nerf Ecologist forage quality + add quality to foraged minerals for Gemologist + increment respective
		///     mod data fields.
		/// </summary>
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> GameLocationCheckActionTranspiler(
			IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator, MethodBase original)
		{
			var helper = new ILHelper(original, instructions);

			/// From: if (who.professions.Contains(<botanist_id>) && objects[key].isForage()) objects[key].Quality = 4
			/// To: if (who.professions.Contains(<ecologist_id>) && objects[key].isForage() && !IsForagedMineral(objects[key]) objects[key].Quality = GetEcologistForageQuality()

			try
			{
				helper
					.FindProfessionCheck(Farmer.botanist) // find index of botanist check
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Ldarg_0) // start of objects[key].isForage() check
					)
					.ToBufferUntil( // copy objects[key]
						new CodeInstruction(OpCodes.Callvirt,
							typeof(OverlaidDictionary).PropertyGetter("Item"))
					)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Brfalse_S) // end of check
					)
					.GetOperand(out var shouldntSetCustomQuality) // copy failed check branch destination
					.Advance()
					.InsertBuffer() // insert objects[key]
					.Insert( // check if is foraged mineral and branch if true
						new CodeInstruction(OpCodes.Call,
							typeof(SObjectExtensions).MethodNamed(nameof(SObjectExtensions.IsForagedMineral))),
						new CodeInstruction(OpCodes.Brtrue_S, (Label) shouldntSetCustomQuality)
					)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Ldc_I4_4) // start of objects[key].Quality = 4
					)
					.ReplaceWith( // replace with custom quality
						new(OpCodes.Call,
							typeof(Utility.Professions).MethodNamed(
								nameof(Utility.Professions.GetEcologistForageQuality)))
					);
			}
			catch (Exception ex)
			{
				ModEntry.Log($"Failed while patching modded Ecologist forage quality.\nHelper returned {ex}",
					LogLevel.Error);
				return null;
			}

			/// Injected: else if (who.professions.Contains(<gemologist_id>) && IsForagedMineral(objects[key])) objects[key].Quality = GetMineralQualityForGemologist()

			var gemologistCheck = iLGenerator.DefineLabel();
			try
			{
				helper
					.FindProfessionCheck(Farmer.botanist) // return to botanist check
					.Retreat() // retreat to start of check
					.ToBufferUntil( // copy entire section until done setting quality
						true,
						false,
						new CodeInstruction(OpCodes.Br_S)
					)
					.AdvanceUntil( // change previous section branch destinations to injected section
						new CodeInstruction(OpCodes.Brfalse_S)
					)
					.SetOperand(gemologistCheck)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Brfalse_S)
					)
					.SetOperand(gemologistCheck)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Brtrue_S)
					)
					.SetOperand(gemologistCheck)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Br_S)
					)
					.Advance()
					.InsertBuffer() // insert copy
					.Return()
					.AddLabels(gemologistCheck) // add destination label for branches from previous section
					.AdvanceUntil( // find repeated botanist check
						new CodeInstruction(OpCodes.Ldc_I4_S, Farmer.botanist)
					)
					.SetOperand(Utility.Professions.IndexOf("Gemologist")) // replace with gemologist check
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Ldarg_0)
					)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Brfalse_S)
					)
					.GetOperand(out var shouldntSetCustomQuality) // copy next section branch destination
					.RetreatUntil(
						new CodeInstruction(OpCodes.Ldarg_0) // start of call to isForage()
					)
					.RemoveUntil( // right before call to IsForagedMineral()
						new CodeInstruction(OpCodes.Callvirt,
							typeof(OverlaidDictionary).PropertyGetter("Item"))
					)
					.Advance()
					.ReplaceWith( // remove 'not' and set correct branch destination
						new(OpCodes.Brfalse_S, (Label) shouldntSetCustomQuality)
					)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Call,
							typeof(Utility.Professions).MethodNamed(
								nameof(Utility.Professions.GetEcologistForageQuality)))
					)
					.SetOperand(
						typeof(Utility.Professions).MethodNamed(nameof(Utility.Professions
							.GetGemologistMineralQuality))); // set correct custom quality method call
			}
			catch (Exception ex)
			{
				ModEntry.Log($"Failed while adding modded Gemologist foraged mineral quality.\nHelper returned {ex}",
					LogLevel.Error);
				return null;
			}

			/// Injected: CheckActionSubroutine(objects[key], this, who)
			/// After: Game1.stats.ItemsForaged++

			try
			{
				helper
					.FindNext(
						new CodeInstruction(OpCodes.Callvirt,
							typeof(Stats).PropertySetter(nameof(Stats.ItemsForaged)))
					)
					.Advance()
					.InsertBuffer(5, 4) // SObject objects[key]
					.Insert(
						new CodeInstruction(OpCodes.Ldarg_0),
						new CodeInstruction(OpCodes.Ldarg_3)
					)
					.Insert(
						new CodeInstruction(OpCodes.Call,
							typeof(GameLocationCheckActionPatch).MethodNamed(nameof(CheckActionSubroutine)))
					);
			}
			catch (Exception ex)
			{
				ModEntry.Log($"Failed while adding Ecologist and Gemologist counter increment.\nHelper returned {ex}",
					LogLevel.Error);
				return null;
			}

			/// From: if (random.NextDouble() < 0.2)
			/// To: if (random.NextDouble() < who.professions.Contains(100 + <forager_id>) ? 0.4 : 0.2

			var notPrestigedForager = iLGenerator.DefineLabel();
			var resumeExecution = iLGenerator.DefineLabel();
			try
			{
				helper
					.FindProfessionCheck(Utility.Professions.IndexOf("Forager"))
					.Retreat()
					.ToBufferUntil(
						true,
						true,
						new CodeInstruction(OpCodes.Brfalse_S)
					)
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Ldc_R8, 0.2)
					)
					.AddLabels(notPrestigedForager)
					.InsertBuffer()
					.RetreatUntil(
						new CodeInstruction(OpCodes.Ldc_I4_S, Utility.Professions.IndexOf("Forager"))
					)
					.SetOperand(100 + Utility.Professions.IndexOf("Forager"))
					.AdvanceUntil(
						new CodeInstruction(OpCodes.Brfalse_S)
					)
					.SetOperand(notPrestigedForager)
					.Advance()
					.Insert(
						new CodeInstruction(OpCodes.Ldc_R8, 0.4),
						new CodeInstruction(OpCodes.Br_S, resumeExecution)
					)
					.Advance()
					.AddLabels(resumeExecution);
			}
			catch (Exception ex)
			{
				ModEntry.Log($"Failed while adding prestiged Foraged double forage bonus.\nHelper returned {ex}",
					LogLevel.Error);
				return null;
			}

			return helper.Flush();
		}

		#endregion harmony patches

		#region private methods

		private static void CheckActionSubroutine(SObject obj, GameLocation location, Farmer who)
		{
			if (who.HasProfession("Ecologist") && obj.isForage(location) && !obj.IsForagedMineral())
				ModEntry.Data.Increment<uint>("ItemsForaged");
			else if (who.HasProfession("Gemologist") && obj.IsForagedMineral())
				ModEntry.Data.Increment<uint>("MineralsCollected");
		}

		#endregion private methods
	}
}