using RimWorld;
using Verse;
using HarmonyLib;

namespace RandomStartMod
{
    [StaticConstructorOnStartup]
    public static class RandomStartPatcher
    {
        static RandomStartPatcher()
        {
            var harmony = new Harmony("com.bogknight.RandomStart");
            harmony.PatchAll();
            Mod mod = LoadedModManager.GetMod<RandomStartMod>();
            if (mod == null)
            {
                Log.Error("[Random Start] Could not find the loaded Random Start mod instance to log its version.");
                return;
            }

            Util.LogMessage($"{mod.Content.ModMetaData.ModVersion}");
        }
    }
}
