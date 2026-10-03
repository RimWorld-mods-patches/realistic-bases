using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RealisticBases.Planning;

namespace RealisticBases.Diagnostics
{
    // Disposable test data only. Export the resolved game catalog so a native
    // rejection can be replayed without guessing XML/runtime differences.
    internal static class P4PlannerFixtureExporter
    {
        internal static void Write(string path, TechCatalog catalog)
        {
            string source = "// Generated resolved runtime catalog: TEST DATA ONLY.\n" +
                "using System.Collections.Generic;\nusing RealisticBases.Planning;\n" +
                "internal static class RuntimeFlatCatalogFixture\n{\n" +
                "public static TechCatalog Build() => new TechCatalog(\"captured Core runtime\",\n" +
                "new CatalogDef[] {\n" + string.Join(",\n", catalog.Defs.Select(Constructor)) +
                "\n}, new CatalogResearch[] {\n" + string.Join(",\n", catalog.Research.Select(Constructor)) + "\n});\n}\n";
            System.IO.File.WriteAllText(path, source);
        }

        private static string Constructor(object value)
        {
            Type type = value.GetType();
            ConstructorInfo ctor = type.GetConstructors().Single();
            return "new " + type.Name + "(" + string.Join(", ", ctor.GetParameters().Select(p =>
            {
                string property = p.Name == "research" ? "ResearchPrerequisites" :
                    char.ToUpperInvariant(p.Name[0]) + p.Name.Substring(1);
                PropertyInfo info = type.GetProperty(property) ?? throw new InvalidOperationException("No fixture property " + property);
                return p.Name + ": " + Literal(info.GetValue(value, null));
            })) + ")";
        }

        private static string Literal(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is Enum) return value.GetType().Name + "." + value;
            if (value is GridPoint c) return "new GridPoint(" + c.X + ", " + c.Z + ")";
            if (value is IEnumerable<string> strings) return "new string[] {" + string.Join(",", strings.Select(x => Literal(x))) + "}";
            if (value is IEnumerable<KeyValuePair<string, int>> costs)
                return "new KeyValuePair<string,int>[] {" + string.Join(",", costs.Select(x =>
                    "new KeyValuePair<string,int>(" + Literal(x.Key) + "," + x.Value + ")")) + "}";
            if (value is double number) return number.ToString("R", CultureInfo.InvariantCulture) + "d";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
