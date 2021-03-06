using StardewModdingAPI.Utilities;

namespace TheLion.Stardew.Tools.Configs
{
	/// <summary>The mod user-defined settings.</summary>
	public class ToolConfig
	{
		/// <summary>The Axe features to enable.</summary>
		public AxeConfig AxeConfig { get; set; } = new();

		/// <summary>The Pickaxe features to enable.</summary>
		public PickaxeConfig PickaxeConfig { get; set; } = new();

		/// <summary>The Scythe features to enable.</summary>
		public ScytheConfig ScytheConfig { get; set; } = new();

		/// <summary>Whether charging requires a mod key to activate.</summary>
		public bool RequireModkey { get; set; } = true;
		
		/// <summary>The chosen mod key(s).</summary>
		public KeybindList Modkey { get; set; } = KeybindList.Parse("LeftShift, LeftShoulder");

		/// <summary>How much stamina the shockwave should consume.</summary>
		public float StaminaCostMultiplier { get; set; } = 1.0f;

		/// <summary>The delay in milliseconds between releasing the tool button and triggering the shockwave.</summary>
		public int ShockwaveDelay { get; set; } = 200;
	}
}