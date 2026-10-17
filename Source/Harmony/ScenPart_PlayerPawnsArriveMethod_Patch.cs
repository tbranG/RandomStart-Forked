using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RandomStartMod
{
    [HarmonyPatch(typeof(ScenPart_PlayerPawnsArriveMethod), "GenerateIntoMap")]
    internal class ScenPart_PlayerPawnsArriveMethod_Patch
    {
        [HarmonyPrefix]
        static bool Prefix(Map map, ScenPart_PlayerPawnsArriveMethod __instance)
        {
            if (!RandomStartData.startedFromRandom)
            {
                return true;
            }

            RandomStartSettings settings = LoadedModManager.GetMod<RandomStartMod>().GetSettings<RandomStartSettings>();
            if (!settings.removeStartingItems && !settings.addRandomItems && !settings.alwaysSpawnEssentials)
            {
                return true;
            }

            Util.LogMessage("Patching PlayerPawnsArriveMethod");

            if (Find.GameInitData == null)
            {
                return false;
            }

            List<List<Thing>> list = new List<List<Thing>>();

            float totalRandomItemMarketValue = 0.0f;


            foreach (Pawn startingAndOptionalPawn in Find.GameInitData.startingAndOptionalPawns)
            {
                List<Thing> list3 = new List<Thing>();
                List<Thing> list4 = new List<Thing>();
                list3.Add(startingAndOptionalPawn);
                list.Add(list3);
                if (!settings.removeStartingItems)
                {
                    foreach (
                        ThingDefCount item in Find.GameInitData.startingPossessions[
                            startingAndOptionalPawn
                        ]
                    )
                    {
                        list4.Add(StartingPawnUtility.GenerateStartingPossession(item));
                    }
                }
                int num1 = 0;
                foreach (Thing item in list4)
                {
                    if (item.def.CanHaveFaction)
                    {
                        item.SetFactionDirect(Faction.OfPlayer);
                    }
                    list[num1].Add(item);
                    totalRandomItemMarketValue += item.MarketValue;
                    num1++;
                    if (num1 >= list.Count)
                    {
                        num1 = 0;
                    }
                }
            }

            if (!settings.removeStartingItems)
            {
                List<Thing> list2 = new List<Thing>();
                foreach (ScenPart allPart in Find.Scenario.AllParts)
                {
                    list2.AddRange(allPart.PlayerStartingThings());
                }
                int num2 = 0;
                foreach (Thing item2 in list2)
                {
                    if (item2.def.CanHaveFaction)
                    {
                        item2.SetFactionDirect(Faction.OfPlayer);
                    }
                    list[num2].Add(item2);
                    totalRandomItemMarketValue += item2.MarketValue;
                    num2++;
                    if (num2 >= list.Count)
                    {
                        num2 = 0;
                    }
                }
            }


            if (settings.addRandomItems)
            {
                Util.LogMessage("Adding random items to starting pawns");
                int techLevelLimit = settings.randomItemTechLevelLimit;
                int num = 0;
                var attempts = 0;
                for (int i = 0; i < settings.randomItemRange.RandomInRange; i++)
                {
                    //ThingSetMakerDef thingSetMakerDef = ThingSetMakerDefOf.MapGen_DefaultStockpile;
                    //randomItems.AddRange(thingSetMakerDef.root.Generate(default(ThingSetMakerParams)));
                    attempts = 0;
                    ThingDef newThing;
                    Thing newItem;
                    ThingDef newStuff;
                    IEnumerable<ThingDef> possibleItems = DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x.category == ThingCategory.Item && (int)x.techLevel <= techLevelLimit && (int)x.techLevel > 0);
                    if (settings.enableMarketValueLimit)
                    {
                        do
                        {
                            newThing = possibleItems.RandomElement();
                            newStuff = GenStuff.RandomStuffFor(newThing);
                            newItem = ThingMaker.MakeThing(newThing, newStuff);
                            attempts++;
                        }
                        while (totalRandomItemMarketValue + (newItem.MarketValue * newThing.stackLimit) > settings.randomItemTotalMarketValueLimit && attempts <= 20);
                    }
                    else
                    {
                        newThing = possibleItems.RandomElement();
                        newStuff = GenStuff.RandomStuffFor(newThing);
                        newItem = ThingMaker.MakeThing(newThing, newStuff);
                    }

                    if (attempts >= 20)
                    {
                        break;
                    }
                    for (int j = 0; j < newThing.stackLimit; j++)
                    {
                        if (newItem.def.CanHaveFaction)
                        {
                            newItem.SetFactionDirect(Faction.OfPlayer);
                        }
                        list[num].Add(ThingMaker.MakeThing(newThing, newStuff));
                        totalRandomItemMarketValue += newItem.MarketValue;
                    }
                    num++;
                    if (num >= list.Count)
                    {
                        num = 0;
                    }
                    if (attempts >= 20)
                    {
                        break;
                    }
                }
                Util.LogMessage($"Total item market cost: {totalRandomItemMarketValue} Limit: {(settings.enableMarketValueLimit ? settings.randomItemTotalMarketValueLimit.ToString() : "Disabled")}");
            }

            if (settings.alwaysSpawnEssentials)
            {
                Util.LogMessage("Spawning essential starting items");
                AddEssentialItems(list);
            }

            DropPodUtility.DropThingGroupsNear(MapGenerator.PlayerStartSpot, map, list, 110, Find.GameInitData.QuickStarted || __instance.method != PlayerPawnsArriveMethod.DropPods, leaveSlag: true, canRoofPunch: true, forbid: true, allowFogged: false);
            return false;
        }

        private static void AddEssentialItems(List<List<Thing>> thingGroups)
        {
            RandomStartSettings settings = LoadedModManager.GetMod<RandomStartMod>().GetSettings<RandomStartSettings>();
            if (!settings.createRandomScenario) return;

            if (thingGroups.Count == 0)
            {
                Log.Warning("[Random Start] Cannot spawn essential items because no starting pawns were generated.");
                return;
            }

            int pawnCount = Find.GameInitData.startingAndOptionalPawns.Count;
            int groupIndex = 0;
            List<ThingDef> vegetables = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def =>
                    def.plant != null
                    && def.plant.harvestedThingDef != null
                    && def.plant.harvestedThingDef.stackLimit >= 25
                    && def.plant.harvestedThingDef.ingestible != null
                    && (
                        def.plant.harvestedThingDef.ingestible.foodType
                        & FoodTypeFlags.VegetableOrFruit
                    ) != 0
                )
                .Select(def => def.plant.harvestedThingDef)
                .Distinct()
                .ToList();

            if (vegetables.Count > 0)
            {
                ThingDef vegetable = vegetables.RandomElement();
                AddEssentialThing(vegetable, 25, thingGroups, ref groupIndex);

                vegetable = vegetables.RandomElement();
                AddEssentialThing(vegetable, 25, thingGroups, ref groupIndex);
            }
            else
            {
                Log.Warning("[Random Start] No suitable vegetable definitions were found; skipping the essential vegetable stacks.");
            }

            TechLevel colonyTechLevel = Faction.OfPlayer.def.techLevel;
            List<ThingDef> weapons = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def =>
                    def.IsWeapon
                    && def.techLevel > TechLevel.Undefined
                    && def.techLevel <= colonyTechLevel
                )
                .ToList();

            if (weapons.Count > 0)
            {
                int weaponCount = Rand.Range(Math.Max(1, pawnCount - 1), pawnCount);
                for (int i = 0; i < weaponCount; i++)
                {
                    ThingDef weapon = weapons.RandomElement();
                    AddEssentialThing(weapon, 1, thingGroups, ref groupIndex);
                }
            }
            else
            {
                Log.Warning($"[Random Start] No weapon definitions at or below colony tech level {colonyTechLevel} were found; skipping essential weapons.");
            }

            string medicineDefName = colonyTechLevel <= TechLevel.Medieval
                ? "MedicineHerbal"
                : "MedicineIndustrial";
            ThingDef medicine = DefDatabase<ThingDef>.GetNamed(medicineDefName, false);
            if (medicine != null)
            {
                AddEssentialThing(medicine, 5, thingGroups, ref groupIndex);
            }
            else
            {
                Log.Warning($"[Random Start] Could not find {medicineDefName}; skipping essential medicine.");
            }

            string mealDefName = colonyTechLevel <= TechLevel.Medieval
                ? "Pemmican"
                : "MealSurvivalPackaged";
            ThingDef meal = DefDatabase<ThingDef>.GetNamed(mealDefName, false);
            if (meal != null)
            {
                int mealAmount = string.Equals(mealDefName, "MealSurvivalPackaged") ? 5 : 25;
                AddEssentialThing(meal, mealAmount * pawnCount, thingGroups, ref groupIndex);
            }
            else
            {
                Log.Warning($"[Random Start] Could not find {mealDefName}; skipping essential meals.");
            }
        }

        private static void AddEssentialThing(
            ThingDef thingDef,
            int count,
            List<List<Thing>> thingGroups,
            ref int groupIndex
        )
        {
            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(thingDef, GenStuff.RandomStuffFor(thingDef));
                int stackCount = Math.Min(count, thingDef.stackLimit);
                thing.stackCount = stackCount;
                if (thing.def.CanHaveFaction)
                {
                    thing.SetFactionDirect(Faction.OfPlayer);
                }
                thingGroups[groupIndex].Add(thing);
                groupIndex = (groupIndex + 1) % thingGroups.Count;
                count -= stackCount;
            }
        }
    }
}
