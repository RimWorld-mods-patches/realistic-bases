using System;
using System.Reflection;
using HarmonyLib;
using RealisticBases.Persistence;
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

        // The force flags are one-shot debug-action state and are not saved:
        // Multiplayer runs debug actions on every client, but a saved or
        // window-set flag would exist on one client only.
        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableP0Generator, "enableP0Generator", true);
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
            if (MultiplayerSession.Active && Current.Game != null)
                listing.Label("This Multiplayer game keeps the host's saved setting (generator " +
                    (GeneratorSwitch.Enabled ? "on" : "off") + "). Changes here apply to single-player games.");
            listing.Label("No traps or IEDs are spawned; no resources are injected after setup.");
            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            GeneratorSwitch.AdoptSetting(Current.Game?.GetComponent<P0WorldState>());
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
            if (!GeneratorSwitch.Enabled ||
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

    // The generator switch generation reads. The game saves its own copy, so
    // every Multiplayer client generates with the host's value; the mod
    // setting is the player's preference and is copied into a single-player
    // game when it loads and whenever the setting changes.
    internal static class GeneratorSwitch
    {
        internal static bool Enabled =>
            Current.Game?.GetComponent<P0WorldState>()?.GeneratorEnabled ?? RealisticBasesMod.Settings.enableP0Generator;

        // Debug actions run on every Multiplayer client, so they set both.
        internal static void Set(bool enabled)
        {
            RealisticBasesMod.Settings.enableP0Generator = enabled;
            P0WorldState state = Current.Game?.GetComponent<P0WorldState>();
            if (state != null) state.GeneratorEnabled = enabled;
        }

        internal static void AdoptSetting(P0WorldState state)
        {
            if (state == null || MultiplayerSession.Active) return;
            state.GeneratorEnabled = RealisticBasesMod.Settings.enableP0Generator;
        }
    }

    // Detects a running Multiplayer session through the public API that the
    // Multiplayer mod ships, without referencing it.
    internal static class MultiplayerSession
    {
        private static Func<bool> inMultiplayer;

        internal static bool Active => (inMultiplayer ??= Resolve())();

        private static Func<bool> Resolve()
        {
            MethodInfo getter = AccessTools.TypeByName("Multiplayer.API.MP")?
                .GetProperty("IsInMultiplayer", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
            return getter == null ? () => false : (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
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
