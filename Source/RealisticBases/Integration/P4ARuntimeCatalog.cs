using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RealisticBases.Planning;
using RimWorld;
using Verse;

namespace RealisticBases.Integration
{
    // Read the resolved DefDatabase objects. XML inheritance has already been
    // applied by Verse, so DLC and loaded mods participate in the same snapshot.
    public static class P4ARuntimeCatalog
    {
        private static readonly HashSet<string> Stock = new HashSet<string>(StringComparer.Ordinal)
        { "ComponentIndustrial", "ComponentSpacer", "MedicineHerbal", "MedicineIndustrial", "MedicineUltratech", "Chemfuel" };
        private static readonly HashSet<string> Raw = new HashSet<string>(StringComparer.Ordinal)
        { "ComponentIndustrial", "ComponentSpacer", "MedicineHerbal" };
        private static readonly HashSet<string> RawStuff = new HashSet<string>(StringComparer.Ordinal)
        { "Woody", "Leathery", "Fabric", "Metallic" };

        public static TechCatalog Capture()
        {
            List<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading.OrderBy(d => d.defName, StringComparer.Ordinal).ToList();
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading.ToList();
            Dictionary<string, HashSet<string>> producers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            Dictionary<string, HashSet<string>> recipeResearch = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (RecipeDef recipe in recipes)
            {
                List<string> users = Names(Member(recipe, "recipeUsers")).ToList();
                foreach (ThingDef thing in things)
                    if (Names(Member(thing, "recipes")).Contains(recipe.defName)) users.Add(thing.defName);
                HashSet<string> required = new HashSet<string>(Names(Member(recipe, "researchPrerequisites")), StringComparer.Ordinal);
                AddName(required, Member(recipe, "researchPrerequisite"));
                foreach (object product in Elements(Member(recipe, "products")))
                {
                    string name = Name(Member(product, "thingDef"));
                    if (name == null) continue;
                    AddAll(producers, name, users);
                    HashSet<string> previous;
                    if (recipeResearch.TryGetValue(name, out previous)) previous.IntersectWith(required);
                    else recipeResearch[name] = new HashSet<string>(required, StringComparer.Ordinal);
                }
            }

            List<CatalogDef> defs = new List<CatalogDef>();
            foreach (ThingDef thing in things)
            {
                CatalogCategory category;
                if (!Select(thing, out category)) continue;
                object recipeMaker = Member(thing, "recipeMaker");
                AddAll(producers, thing.defName, Names(Member(recipeMaker, "recipeUsers")));
                IEnumerable<string> stuffProvides = Names(Member(Member(thing, "stuffProps"), "categories"));
                IEnumerable<string> thingCategories = Names(Member(thing, "thingCategories"));
                bool raw = Raw.Contains(thing.defName) || thingCategories.Contains("StoneChunks") ||
                    (category == CatalogCategory.Item && stuffProvides.Any(RawStuff.Contains));
                HashSet<string> research = new HashSet<string>(Names(Member(thing, "researchPrerequisites")), StringComparer.Ordinal);
                research.UnionWith(Names(Member(recipeMaker, "researchPrerequisites")));
                AddName(research, Member(recipeMaker, "researchPrerequisite"));
                HashSet<string> recipeRequired;
                if (recipeMaker == null && !raw && recipeResearch.TryGetValue(thing.defName, out recipeRequired))
                    research.UnionWith(recipeRequired);
                object size = Member(thing, "size");
                object offset = Member(thing, "interactionCellOffset");
                object comps = Member(thing, "comps");
                object power = Elements(comps).FirstOrDefault(c => c.GetType().Name == "CompProperties_Power");
                object fuel = Elements(comps).FirstOrDefault(c => c.GetType().Name == "CompProperties_Refuelable");
                object explosive = Elements(comps).FirstOrDefault(c => c.GetType().Name == "CompProperties_Explosive");
                double value = Stat(thing, "MarketValue");
                double work = Math.Max(Stat(thing, "WorkToMake"), Stat(thing, "WorkToBuild"));
                object building = Member(thing, "building");
                object turretGun = Member(building, "turretGunDef");
                object turretVerb = Elements(Member(turretGun, "verbs")).FirstOrDefault();
                object weaponVerb = Elements(Member(thing, "verbs")).FirstOrDefault();
                object combatVerb = turretVerb ?? weaponVerb;
                HashSet<string> madeAt;
                producers.TryGetValue(thing.defName, out madeAt);
                defs.Add(new CatalogDef(thing.defName, category, Tier(Member(thing, "techLevel")), research,
                    Int(Member(size, "x"), 1), Int(Member(size, "z"), 1), Names(Member(thing, "stuffCategories")),
                    Int(Member(thing, "costStuffCount")), Costs(Member(thing, "costList")), stuffProvides,
                    Names(Member(thing, "weaponTags")), Names(Member(Member(thing, "apparel"), "tags")),
                    Names(Member(Member(thing, "apparel"), "layers")), Names(Member(Member(thing, "apparel"), "bodyPartGroups")),
                    Int(Member(power, "basePowerConsumption")), Names(Member(Member(fuel, "fuelFilter"), "thingDefs")),
                    Number(Member(fuel, "fuelConsumptionRate")), Bool(Member(thing, "hasInteractionCell")),
                    new GridPoint(Int(Member(offset, "x")), Int(Member(offset, "z"))),
                    Passability(Member(thing, "passability")), value,
                    Member(thing, "thingClass") is Type thingClass ? thingClass.Name : Member(thing, "thingClass")?.ToString() ?? string.Empty,
                    Name(Member(thing, "designationCategory")), Bool(Member(building, "isTrap")),
                    madeAt, raw, Math.Max(1, Int(Member(thing, "stackLimit"), 1)), work,
                    Name(turretGun), Number(Member(combatVerb, "minRange")), Number(Member(combatVerb, "range")),
                    Number(Member(fuel, "fuelCapacity")), Number(Member(fuel, "fuelMultiplier")),
                    Number(Member(fuel, "initialFuelPercent")), Number(Member(turretVerb, "consumeFuelPerShot")),
                    Bool(Member(fuel, "factorByDifficulty")), Number(Member(explosive, "explosiveRadius")),
                    Number(Member(building, "combatPower"))));
            }
            List<CatalogResearch> projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Select(project => new CatalogResearch(project.defName, Tier(Member(project, "techLevel")),
                    Names(Member(project, "prerequisites")).Concat(Names(Member(project, "hiddenPrerequisites")))))
                .ToList();
            return new TechCatalog("runtime:" + VersionControl.CurrentVersionStringWithRev, defs, projects);
        }

        private static bool Select(ThingDef thing, out CatalogCategory category)
        {
            category = CatalogCategory.Item;
            string kind = Member(thing, "category")?.ToString();
            if (kind == "Building" && Member(thing, "designationCategory") != null) { category = CatalogCategory.Building; return true; }
            if (kind != "Item") return false;
            if (Member(thing, "apparel") != null) { category = CatalogCategory.Apparel; return true; }
            if (Member(thing, "stuffProps") != null || Stock.Contains(thing.defName) || thing.defName.StartsWith("Shell_", StringComparison.Ordinal) ||
                Names(Member(thing, "thingCategories")).Contains("StoneChunks")) return true;
            if (Names(Member(thing, "weaponTags")).Any() || Names(Member(thing, "thingCategories")).Any(c => c.StartsWith("Weapons", StringComparison.Ordinal)))
            {
                if (Bool(Member(thing, "destroyOnDrop"))) return false;
                category = CatalogCategory.Weapon;
                return true;
            }
            return false;
        }

        private static object Member(object source, string name)
        {
            if (source == null) return null;
            Type type = source.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field.GetValue(source);
            PropertyInfo property = type.GetProperty(name, flags);
            return property == null ? null : property.GetValue(source, null);
        }
        private static IEnumerable<object> Elements(object source) => source is IEnumerable sequence && !(source is string)
            ? sequence.Cast<object>().Where(v => v != null) : Enumerable.Empty<object>();
        private static string Name(object source) => source == null ? null : Member(source, "defName")?.ToString() ?? source.ToString();
        private static IEnumerable<string> Names(object source) => Elements(source).Select(Name).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.Ordinal);
        private static void AddName(ISet<string> set, object value) { string name = Name(value); if (!string.IsNullOrEmpty(name)) set.Add(name); }
        private static void AddAll(Dictionary<string, HashSet<string>> values, string key, IEnumerable<string> additions)
        {
            HashSet<string> result;
            if (!values.TryGetValue(key, out result)) values[key] = result = new HashSet<string>(StringComparer.Ordinal);
            result.UnionWith(additions);
        }
        private static IEnumerable<KeyValuePair<string, int>> Costs(object source) => Elements(source)
            .Select(cost => new KeyValuePair<string, int>(Name(Member(cost, "thingDef")), Int(Member(cost, "count"))))
            .Where(cost => cost.Key != null && cost.Value > 0);
        private static double Stat(object thing, string name) => Elements(Member(thing, "statBases"))
            .Where(s => Name(Member(s, "stat")) == name).Select(s => Number(Member(s, "value"))).FirstOrDefault();
        private static TechTier Tier(object value) => Enum.TryParse(value?.ToString() ?? string.Empty, out TechTier tier) ? tier : TechTier.Undefined;
        private static CatalogPassability Passability(object value) => Enum.TryParse(value?.ToString() ?? string.Empty, out CatalogPassability passability)
            ? passability : CatalogPassability.Standable;
        private static int Int(object value, int fallback = 0) => value == null ? fallback : Convert.ToInt32(value);
        private static double Number(object value) => value == null ? 0 : Convert.ToDouble(value);
        private static bool Bool(object value) => value != null && Convert.ToBoolean(value);
    }
}
