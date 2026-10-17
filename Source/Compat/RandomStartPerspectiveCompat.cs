using System;
using System.Linq;
using System.Reflection;
using Verse;

namespace RandomStartMod.Compat
{
    public enum PerspectiveStartMode
    {
        Director,
        Authentic,
        Swap,
        Dynamic
    }

    public enum SurveillanceStartMode
    {
        Omniscience,
        LimitedAwareness
    }

    public static class RandomStartPerspectiveCompat
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

        private static readonly Type perspectiveStateType = FindType("PerspectiveShift.State");
        private static readonly Type surveillanceStateType = FindType("SurveillanceShift.State");

        public static bool PerspectiveShiftAvailable => perspectiveStateType != null;
        public static bool SurveillanceShiftAvailable => surveillanceStateType != null;

        public static void ApplySelectedModes(RandomStartSettings settings, bool assignRandomPawn)
        {
            ApplySurveillanceMode(settings.surveillanceMode);
            Pawn pawn = assignRandomPawn
                && PerspectiveShiftAvailable
                && settings.perspectiveMode != PerspectiveStartMode.Director
                    ? SelectStartingPawn()
                    : null;
            ApplyPerspectiveMode(settings.perspectiveMode, pawn);
        }

        public static void RefreshSurveillanceVisibility()
        {
            RecalculateSurveillanceVisibility();
        }

        private static Pawn SelectStartingPawn()
        {
            if (Find.GameInitData?.startingAndOptionalPawns == null)
            {
                Log.Error("[Random Start] Cannot select a Perspective Shift avatar because starting pawn data is unavailable.");
                return null;
            }

            var candidates = Find.GameInitData.startingAndOptionalPawns
                .Take(Find.GameInitData.startingPawnCount)
                .Where(pawn => pawn != null && !pawn.Dead)
                .ToList();

            if (!candidates.TryRandomElement(out Pawn pawn))
            {
                Log.Error("[Random Start] Cannot select a Perspective Shift avatar because there are no starting colonists.");
                return null;
            }

            return pawn;
        }

        private static void ApplyPerspectiveMode(PerspectiveStartMode mode, Pawn pawn)
        {
            Type stateType = perspectiveStateType;
            if (stateType == null)
            {
                return;
            }

            FieldInfo modeField = stateType.GetField("CurrentMode", PublicStatic);
            MethodInfo clearAvatar = stateType.GetMethod("ClearAvatar", PublicStatic, null, Type.EmptyTypes, null);
            MethodInfo setAvatar = stateType.GetMethod("SetAvatar", PublicStatic, null, new[] { typeof(Pawn), typeof(bool) }, null);
            if (modeField == null || clearAvatar == null || setAvatar == null || !modeField.FieldType.IsEnum)
            {
                Log.Error("[Random Start] Perspective Shift's state API was not found; its mode could not be applied.");
                return;
            }

            if (!InvokeCompatibilityMethod(clearAvatar, null, "clearing the Perspective Shift avatar"))
            {
                return;
            }

            modeField.SetValue(null, Enum.Parse(modeField.FieldType, mode.ToString()));
            if (mode != PerspectiveStartMode.Director && pawn != null)
            {
                InvokeCompatibilityMethod(setAvatar, null, "setting the Perspective Shift avatar", pawn, false);
            }
        }

        private static void ApplySurveillanceMode(SurveillanceStartMode mode)
        {
            Type stateType = surveillanceStateType;
            if (stateType == null)
            {
                return;
            }

            FieldInfo modeField = stateType.GetField("AwarenessMode", PublicStatic);
            if (modeField == null || !modeField.FieldType.IsEnum)
            {
                Log.Error("[Random Start] Surveillance Shift's awareness mode was not found; its mode could not be applied.");
                return;
            }

            string externalMode = mode == SurveillanceStartMode.LimitedAwareness
                ? "LimitedAwareness"
                : "AreaOmniscience";
            modeField.SetValue(null, Enum.Parse(modeField.FieldType, externalMode));
        }

        private static void RecalculateSurveillanceVisibility()
        {
            Type stateType = surveillanceStateType;
            Type fogStyleType = stateType?.Assembly.GetType("SurveillanceShift.FogStyle", false);
            MethodInfo recalculateMaps = fogStyleType?.GetMethod("RecalculateMaps", PublicStatic);
            if (stateType == null || recalculateMaps == null)
            {
                if (stateType != null)
                {
                    Log.Error("[Random Start] Surveillance Shift's visibility refresh method was not found.");
                }
                return;
            }

            InvokeCompatibilityMethod(recalculateMaps, null, "recalculating Surveillance Shift visibility");
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static bool InvokeCompatibilityMethod(MethodInfo method, object target, string action, params object[] arguments)
        {
            try
            {
                method.Invoke(target, arguments);
                return true;
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                Log.Error($"[Random Start] Error while {action}: {cause}");
                return false;
            }
        }
    }
}
