using HarmonyLib;
using RimWorld;

namespace RandomStartMod
{
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.GetFirstConfigPage))]
    [HarmonyPriority(Priority.Last)]
    internal static class Scenario_GetFirstConfigPage_Perspective_Patch
    {
        private static readonly string[] PagesToSkip =
        {
            "PerspectiveShift.Page_ChoosePerspective",
            "PerspectiveShift.Page_ChooseStartingCharacter",
            "SurveillanceShift.Page_ChooseAwareness",
            "SurveillanceShift.Page_FogStyle"
        };

        [HarmonyPostfix]
        private static void Postfix(ref Page __result)
        {
            if (!RandomStartData.startedFromRandom)
            {
                return;
            }

            Page previous = null;
            Page current = __result;
            while (current != null)
            {
                Page next = current.next;
                if (ShouldSkip(current))
                {
                    if (current.nextAct != null)
                    {
                        if (previous != null)
                        {
                            previous.nextAct = current.nextAct;
                        }
                        else if (next != null)
                        {
                            next.nextAct = current.nextAct;
                        }
                    }

                    if (previous == null)
                    {
                        __result = next;
                    }
                    else
                    {
                        previous.next = next;
                    }

                    if (next != null)
                    {
                        next.prev = previous;
                    }
                }
                else
                {
                    previous = current;
                }

                current = next;
            }

            if (__result != null)
            {
                __result.prev = null;
            }
        }

        private static bool ShouldSkip(Page page)
        {
            string typeName = page.GetType().FullName;
            foreach (string pageName in PagesToSkip)
            {
                if (typeName == pageName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
