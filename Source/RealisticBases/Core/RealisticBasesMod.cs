using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RealisticBases.Core
{
    public sealed class RealisticBasesSettings : ModSettings
    {
        public bool enableP0Generator = true;
        public bool forceRejectNext;
        public bool forcePartialFailureNext;
        public string forceP2ProfileNext;
        public int forceP4WallLayersNext;
        public bool probeProceduralNext;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableP0Generator, "enableP0Generator", true);
            Scribe_Values.Look(ref forceRejectNext, "forceRejectNext", false);
            Scribe_Values.Look(ref forcePartialFailureNext, "forcePartialFailureNext", false);
            Scribe_Values.Look(ref forceP2ProfileNext, "forceP2ProfileNext", string.Empty);
            Scribe_Values.Look(ref forceP4WallLayersNext, "forceP4WallLayersNext", 0);
            Scribe_Values.Look(ref probeProceduralNext, "probeProceduralNext", false);
        }
    }

    public sealed class RealisticBasesMod : Mod
    {
        public static RealisticBasesSettings Settings { get; private set; }

        public RealisticBasesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RealisticBasesSettings>();
            new Harmony("romangr.realisticbases.p0").PatchAll();
            P0Log.Message("BOOT", "assembly loaded; dedicated generator disabled=" + (!Settings.enableP0Generator));
        }

        public override string SettingsCategory() => "Realistic Bases — P0 probes";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "Enable guarded P0 generator for newly visited ordinary hostile human settlements",
                ref Settings.enableP0Generator,
                "On by default. Does not affect player maps, non-human factions, existing maps, or settlements with an explicit generator override.");
            listing.CheckboxLabeled("Force next eligible plan rejection", ref Settings.forceRejectNext);
            listing.CheckboxLabeled("Force next eligible partial-spawn failure and recovery", ref Settings.forcePartialFailureNext);
            listing.Label("P0 diagnostic only. No traps or IEDs are spawned; no resources are injected after setup.");
            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            P0Log.Message("SETTINGS", "generator=" + Settings.enableP0Generator +
                " forceRejectNext=" + Settings.forceRejectNext +
                " forcePartialFailureNext=" + Settings.forcePartialFailureNext +
                " forceP2ProfileNext=" + Settings.forceP2ProfileNext +
                " forceP4WallLayersNext=" + Settings.forceP4WallLayersNext);
        }
    }

    [DefOf]
    public static class RBDefOf
    {
        public static Verse.JobDef RB_P3_BoundedRearm;
        public static MapGeneratorDef RB_P0_Base_Faction;
        public static DutyDef RB_P0_WorkSettlement;
        public static DutyDef RB_P0_HoldPost;
        public static DutyDef RB_P3_DefendPost;
        public static DutyDef RB_P3_ManMortar;
        public static DutyDef RB_P3_Support;
        public static DutyDef RB_P3_Relief;
        public static DutyDef RB_P3_InnerFallback;
        public static DutyDef RB_P3_OffDuty;

        static RBDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RBDefOf));
        }
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.MapGeneratorDef), MethodType.Getter)]
    internal static class Settlement_MapGeneratorDef_P0Patch
    {
        private static void Postfix(Settlement __instance, ref MapGeneratorDef __result)
        {
            if (!RealisticBasesMod.Settings.enableP0Generator ||
                __instance == null ||
                __instance.GetType() != typeof(Settlement) ||
                __instance.Faction == null ||
                __instance.Faction.IsPlayer ||
                !__instance.Faction.def.humanlikeFaction ||
                __instance.def.mapGenerator != null ||
                __result != MapGeneratorDefOf.Base_Faction)
            {
                return;
            }

            __result = RBDefOf.RB_P0_Base_Faction;
            P0Log.Message("GEN_SELECT", "settlement=" + __instance.ID +
                " faction=" + __instance.Faction.def.defName +
                " generator=" + __result.defName);
        }
    }

    internal static class P0Log
    {
        internal const string Prefix = "[RealisticBases:P0]";

        internal static void Message(string eventName, string details)
        {
            Log.Message(Prefix + " event=" + eventName + " tick=" + GenTicks.TicksGame + " " + details);
        }

        internal static void Error(string eventName, string details)
        {
            Log.Error(Prefix + " event=" + eventName + " tick=" + GenTicks.TicksGame + " " + details);
        }
    }
}
