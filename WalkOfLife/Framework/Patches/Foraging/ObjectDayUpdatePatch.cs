using HarmonyLib;
using JetBrains.Annotations;
using StardewValley;
using TheLion.Stardew.Professions.Framework.Extensions;
using SObject = StardewValley.Object;

namespace TheLion.Stardew.Professions.Framework.Patches
{
	[UsedImplicitly]
	internal class ObjectDayUpdatePatch : BasePatch
	{
		/// <summary>Construct an instance.</summary>
		internal ObjectDayUpdatePatch()
		{
			Original = RequireMethod<SObject>(nameof(SObject.DayUpdate));
		}

		#region harmony patches

		/// <summary>Patch to add quality to Ecologist Mushroom Boxes.</summary>
		[HarmonyPostfix]
		private static void ObjectDayUpdatePostfix(SObject __instance)
		{
			if (!__instance.bigCraftable.Value || __instance.ParentSheetIndex != 128 ||
			    __instance.heldObject.Value is null || !Game1.MasterPlayer.HasProfession("Ecologist"))
				return;

			__instance.heldObject.Value.Quality = Utility.Professions.GetEcologistForageQuality();
		}

		#endregion harmony patches
	}
}