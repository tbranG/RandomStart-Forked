using HarmonyLib;
using RandomStartMod.Compat;
using Verse;

namespace RandomStartMod
{
    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    internal static class Game_InitNewGame_Perspective_Patch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            if (!RandomStartData.startedFromRandom)
            {
                return;
            }

            RandomStartSettings settings = LoadedModManager.GetMod<RandomStartMod>()
                .GetSettings<RandomStartSettings>();
            RandomStartPerspectiveCompat.ApplySelectedModes(settings, assignRandomPawn: true);
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!RandomStartData.startedFromRandom)
            {
                return;
            }

            if (!RandomStartPerspectiveCompat.SurveillanceShiftAvailable)
            {
                return;
            }

            RandomStartSettings settings = LoadedModManager.GetMod<RandomStartMod>()
                .GetSettings<RandomStartSettings>();
            if (settings.surveillanceMode == SurveillanceStartMode.LimitedAwareness)
            {
                RandomStartPerspectiveCompat.RefreshSurveillanceVisibility();
            }
        }
    }
}
