#!/usr/bin/env python3
"""Generate the synthetic Core TechCatalog fixture for the pure P4A tests.

Reads RimWorld Core XML defs (read-only), resolves ParentName inheritance the
way Verse.XmlInheritance does (text overwrites, lists append, Inherit="False"
replaces, IsNull="True" removes), and emits a C# fixture. The fixture is test
data only; the pure planner never reads defs or files at runtime.

Usage:
  tools/extract-core-catalog.py [CORE_DEFS_DIR] [OUTPUT_CS]
"""
import copy
import glob
import os
import sys
import xml.etree.ElementTree as ET

DEFAULT_DEFS = os.path.expanduser(
    "~/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Data/Core/Defs")
DEFAULT_OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                           "Source", "RealisticBases.LayoutTests", "CoreCatalogFixture.cs")

# Stock items used by the settlement model beyond stuff (stuffProps) and chunks.
STOCK_ITEMS = {"ComponentIndustrial", "ComponentSpacer", "MedicineHerbal", "MedicineIndustrial",
               "MedicineUltratech", "Chemfuel", "ReinforcedBarrel"}
STOCK_PREFIXES = ("Shell_",)
# Items obtainable without local production (mined, harvested, butchered, salvaged).
# Raw items skip the producer gate and recipe-only research (that research gates
# local production, not possession). Minimum tiers for exotic raw items live in
# TechCatalog.RawMinimumTiers.
RAW_ITEMS = {"ComponentIndustrial", "ComponentSpacer", "MedicineHerbal"}
RAW_STUFF_CATEGORIES = {"Woody", "Leathery", "Fabric", "Metallic"}
TECH_LEVELS = ["Undefined", "Animal", "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech"]


def load(defs_dir):
    nodes = []
    for path in sorted(glob.glob(os.path.join(defs_dir, "**", "*.xml"), recursive=True)):
        root = ET.parse(path).getroot()
        nodes.extend(list(root))
    return nodes


def is_true(value):
    return value is not None and value.strip().lower() == "true"


def merge(parent, child):
    """Verse.XmlInheritance.RecursiveNodeCopyOverwriteElements, simplified."""
    if is_true(child.get("IsNull")):
        return None
    if child.get("Inherit", "").lower() == "false" or parent is None:
        return copy.deepcopy(child)
    children = list(child)
    if not children:
        result = copy.deepcopy(parent)
        for c in list(result):
            result.remove(c)
        result.text = child.text
        result.attrib.update(child.attrib)
        return result
    result = copy.deepcopy(parent)
    result.attrib.update(child.attrib)
    if all(c.tag == "li" for c in children):
        for c in children:
            result.append(copy.deepcopy(c))
        return result
    for c in children:
        existing = result.find(c.tag)
        if existing is None:
            result.append(copy.deepcopy(c))
            continue
        merged = merge(existing, c)
        index = list(result).index(existing)
        result.remove(existing)
        if merged is not None:
            result.insert(index, merged)
    return result


class Resolver:
    def __init__(self, nodes):
        self.named = {}
        for n in nodes:
            if n.get("Name"):
                self.named[(n.tag, n.get("Name"))] = n
        self.cache = {}

    def resolve(self, node):
        key = id(node)
        if key in self.cache:
            return self.cache[key]
        parent_name = node.get("ParentName")
        if parent_name:
            parent = self.named.get((node.tag, parent_name))
            if parent is None:
                raise SystemExit("missing parent %s for %s" % (parent_name, node.findtext("defName")))
            result = merge(self.resolve(parent), node)
        else:
            result = node
        self.cache[key] = result
        return result


def texts(node, path):
    return [li.text.strip() for li in node.findall(path) if li.text and li.text.strip()]


def text(node, path, default=None):
    value = node.findtext(path)
    return value.strip() if value is not None and value.strip() else default


def vec(value):
    parts = [p.strip() for p in value.strip().strip("()").split(",")]
    return [int(float(p)) for p in parts]


def number(value):
    f = float(value)
    return int(f) if f == int(f) else f


def main():
    defs_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DEFS
    out_path = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_OUT
    nodes = load(defs_dir)
    resolver = Resolver(nodes)
    concrete = [resolver.resolve(n) for n in nodes if n.tag in ("ThingDef", "RecipeDef", "ResearchProjectDef")
                and not is_true(n.get("Abstract")) and n.findtext("defName")]

    things = {d.findtext("defName").strip(): d for d in concrete if d.tag == "ThingDef"}
    recipes = [d for d in concrete if d.tag == "RecipeDef"]
    research = sorted((d for d in concrete if d.tag == "ResearchProjectDef"), key=lambda d: d.findtext("defName").strip())

    # Producers: recipeMaker users, RecipeDef products with recipeUsers, and benches listing recipes.
    producers = {}
    recipe_by_name = {r.findtext("defName").strip(): r for r in recipes}
    recipe_users = {name: set(texts(r, "recipeUsers/li")) for name, r in recipe_by_name.items()}
    for name, thing in things.items():
        for rec in texts(thing, "recipes/li"):
            recipe_users.setdefault(rec, set()).add(name)
    # Research every producing RecipeDef requires (items without a recipeMaker only).
    recipe_research = {}
    for name, r in recipe_by_name.items():
        products = r.find("products")
        if products is None:
            continue
        needed = set(texts(r, "researchPrerequisites/li"))
        if text(r, "researchPrerequisite"):
            needed.add(text(r, "researchPrerequisite"))
        for p in products:
            producers.setdefault(p.tag, set()).update(recipe_users.get(name, set()))
            recipe_research[p.tag] = needed if p.tag not in recipe_research else recipe_research[p.tag] & needed
    for name, thing in things.items():
        if thing.find("recipeMaker") is not None:
            producers.setdefault(name, set()).update(texts(thing, "recipeMaker/recipeUsers/li"))

    selected = []
    for name, d in sorted(things.items()):
        category = text(d, "category")
        stuff_provides = texts(d, "stuffProps/categories/li")
        thing_categories = texts(d, "thingCategories/li")
        if category == "Building" and text(d, "designationCategory"):
            kind = "Building"
        elif category == "Item" and d.find("apparel") is not None:
            kind = "Apparel"
        elif category == "Item" and (stuff_provides or name in STOCK_ITEMS or name.startswith(STOCK_PREFIXES)
                                     or "StoneChunks" in thing_categories):
            kind = "Item"
        elif category == "Item" and (texts(d, "weaponTags/li") or any(c.startswith("Weapons") for c in thing_categories)):
            # Turret-top guns exist only as part of their turret building, which carries the research gate.
            if is_true(text(d, "destroyOnDrop")):
                continue
            kind = "Weapon"
        else:
            continue
        selected.append((name, kind, d, stuff_provides, thing_categories))

    lines = []
    for name, kind, d, stuff_provides, thing_categories in selected:
        raw = name in RAW_ITEMS or "StoneChunks" in thing_categories or \
            (kind == "Item" and any(c in RAW_STUFF_CATEGORIES for c in stuff_provides))
        args = ['"%s"' % name, "CatalogCategory." + kind]
        tech = text(d, "techLevel", "Undefined")
        if tech not in TECH_LEVELS:
            raise SystemExit("unknown techLevel %s on %s" % (tech, name))
        if tech != "Undefined":
            args.append("techLevel: TechTier." + tech)
        req = []
        for r in texts(d, "researchPrerequisites/li") + texts(d, "recipeMaker/researchPrerequisites/li"):
            if r not in req:
                req.append(r)
        single = text(d, "recipeMaker/researchPrerequisite")
        if single and single not in req:
            req.append(single)

        def strs(values):
            return "new[] { " + ", ".join('"%s"' % v for v in values) + " }"

        if d.find("recipeMaker") is None and not raw:
            for r in sorted(recipe_research.get(name, set())):
                if r not in req:
                    req.append(r)
        if req:
            args.append("research: " + strs(req))
        size = text(d, "size")
        if size:
            sx, sz = vec(size)[:2]
            if (sx, sz) != (1, 1):
                args.append("sizeX: %d, sizeZ: %d" % (sx, sz))
        if texts(d, "stuffCategories/li"):
            args.append("stuffCategories: " + strs(texts(d, "stuffCategories/li")))
        if text(d, "costStuffCount"):
            args.append("costStuffCount: %d" % int(text(d, "costStuffCount")))
        cost = d.find("costList")
        if cost is not None and len(cost):
            args.append("costList: new[] { " + ", ".join('Cost("%s", %d)' % (c.tag, int(c.text)) for c in cost) + " }")
        if stuff_provides:
            args.append("stuffProvides: " + strs(stuff_provides))
        if texts(d, "weaponTags/li"):
            args.append("weaponTags: " + strs(texts(d, "weaponTags/li")))
        if texts(d, "apparel/tags/li"):
            args.append("apparelTags: " + strs(texts(d, "apparel/tags/li")))
        if texts(d, "apparel/layers/li"):
            args.append("apparelLayers: " + strs(texts(d, "apparel/layers/li")))
        if texts(d, "apparel/bodyPartGroups/li"):
            args.append("apparelBodyParts: " + strs(texts(d, "apparel/bodyPartGroups/li")))
        power = None
        fuel = []
        fuel_rate = None
        fuel_comp = None
        explosive_comp = None
        for comp in d.findall("comps/li"):
            cls = comp.get("Class", "")
            if comp.find("basePowerConsumption") is not None:
                power = number(comp.findtext("basePowerConsumption"))
            if cls == "CompProperties_Refuelable":
                fuel_comp = comp
                fuel = texts(comp, "fuelFilter/thingDefs/li")
                if comp.findtext("fuelConsumptionRate"):
                    fuel_rate = number(comp.findtext("fuelConsumptionRate"))
            if cls == "CompProperties_Explosive":
                explosive_comp = comp
        if power:
            args.append("basePowerConsumption: %d" % int(round(power)))
        if fuel:
            args.append("fuel: " + strs(fuel))
        if fuel_rate:
            args.append("fuelPerDay: %r" % float(fuel_rate))
        facilities = sorted({name for comp in d.findall("comps/li") for name in texts(comp, "linkableFacilities/li")})
        if facilities:
            args.append("linkableFacilities: " + strs(facilities))
        for comp in d.findall("comps/li"):
            if comp.get("Class") == "CompProperties_Facility":
                # Core 1.6 CompProperties_Facility default, verified against
                # version-matched implementation source (XML often omits it).
                args.append("facilityMaxDistance: %r" % float(number(text(comp, "maxDistance") or "8")))
        if is_true(text(d, "building/isSittable")):
            args.append("isSittable: true")
        turret_gun = text(d, "building/turretGunDef")
        if turret_gun:
            gun = things.get(turret_gun)
            verb = gun.find("verbs/li") if gun is not None else None
            args.append('turretGunDefName: "%s"' % turret_gun)
            for prop, path, source in [
                    ("minimumRange", "minRange", verb), ("maximumRange", "range", verb),
                    ("fuelCapacity", "fuelCapacity", fuel_comp), ("fuelMultiplier", "fuelMultiplier", fuel_comp),
                    ("initialFuelPercent", "initialFuelPercent", fuel_comp),
                    ("fuelPerShot", "consumeFuelPerShot", verb),
                    ("explosionRadius", "explosiveRadius", explosive_comp),
                    ("combatPower", "building/combatPower", d)]:
                value = text(source, path) if source is not None else None
                if value:
                    args.append("%s: %r" % (prop, float(value)))
            if fuel_comp is not None and is_true(text(fuel_comp, "factorByDifficulty")):
                args.append("fuelFactorByDifficulty: true")
        elif kind == "Weapon":
            verb = d.find("verbs/li")
            for prop, path in (("minimumRange", "minRange"), ("maximumRange", "range")):
                value = text(verb, path) if verb is not None else None
                if value:
                    args.append("%s: %r" % (prop, float(value)))
        if is_true(text(d, "hasInteractionCell")):
            args.append("hasInteractionCell: true")
            offset = text(d, "interactionCellOffset")
            if offset:
                x, _, z = vec(offset)
                args.append("interactionOffset: new GridPoint(%d, %d)" % (x, z))
        passability = text(d, "passability", "Standable")
        if passability != "Standable":
            args.append("passability: CatalogPassability." + passability)
        market = text(d, "statBases/MarketValue")
        if market:
            args.append("marketValue: %r" % float(market))
        thing_class = text(d, "thingClass")
        if thing_class:
            args.append('thingClass: "%s"' % thing_class)
        designation = text(d, "designationCategory")
        if designation:
            args.append('designationCategory: "%s"' % designation)
        if is_true(text(d, "building/isTrap")):
            args.append("isTrap: true")
        produced = sorted(producers.get(name, set()))
        if produced:
            args.append("producedAt: " + strs(produced))
        if raw:
            args.append("raw: true")
        work = max(float(text(d, "statBases/WorkToMake", "0")), float(text(d, "statBases/WorkToBuild", "0")))
        if work > 0:
            args.append("workToMake: %r" % work)
        stack = text(d, "stackLimit")
        if stack and int(stack) > 1:
            args.append("stackLimit: %d" % int(stack))
        lines.append("            new CatalogDef(" + ", ".join(args) + "),")

    research_lines = []
    for r in research:
        name = r.findtext("defName").strip()
        prereq = []
        for p in texts(r, "prerequisites/li") + texts(r, "hiddenPrerequisites/li"):
            if p not in prereq:
                prereq.append(p)
        tech = text(r, "techLevel", "Undefined")
        research_lines.append('            new CatalogResearch("%s", TechTier.%s%s),' % (
            name, tech, (", new[] { " + ", ".join('"%s"' % p for p in prereq) + " }") if prereq else ""))

    header = """// <auto-generated>
// TEST DATA ONLY, not a runtime dependency. Generated by tools/extract-core-catalog.py
// from RimWorld Core defs, source version RimWorld 1.6.4871 (rev595). Do not edit by
// hand; rerun the script. The pure planner never reads defs or files at runtime.
// Defs: %d catalog entries, %d research projects.
// </auto-generated>
using System.Collections.Generic;
using RealisticBases.Planning;

internal static class CoreCatalogFixture
{
    public const string Source = "RimWorld 1.6.4871 Core (synthetic test fixture)";

    private static KeyValuePair<string, int> Cost(string def, int count) => new KeyValuePair<string, int>(def, count);

    public static TechCatalog Build() => new TechCatalog(Source, Defs(), Research());

    private static CatalogDef[] Defs() => new[]
    {
""" % (len(lines), len(research_lines))
    body = header + "\n".join(lines) + "\n    };\n\n    private static CatalogResearch[] Research() => new[]\n    {\n" + \
        "\n".join(research_lines) + "\n    };\n}\n"
    with open(out_path, "w", newline="\n") as f:
        f.write(body)
    print("wrote %s: %d defs, %d research" % (out_path, len(lines), len(research_lines)))


if __name__ == "__main__":
    main()
