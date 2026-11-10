using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatGenerator.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DatGenerator;

/// <summary>
/// A generator to create JSON Converters for the brutal numeric types
/// </summary>
[Generator]
public class BrutalNumericsJsonGenerator : IIncrementalGenerator {
    private const string AssemblyName = "Brutal.Numerics";

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Create comparable version string so can always fast test that first
        var libraryInfo = context.MetadataReferencesProvider
            .Combine(context.CompilationProvider)
            .Select(static (tuple, _) => {
                    var (metaRef, compilation) = tuple;

                    // Find The Brutal Assembly library and box in a Record
                    if (compilation.GetAssemblyOrModuleSymbol(metaRef) is IAssemblySymbol { Name: AssemblyName } assemblySymbol) {
                        // Use a record in case we want to use more info later
                        return new LibraryMetadata(assemblySymbol.Identity.Version.ToString());
                    }

                    return null;
                }
            )
            .Where(static meta => meta != null)
            .Collect()
            .Select(static (libraries, _) => libraries.FirstOrDefault());

        // Now that we've extracted a version and gotten caching based off that, we can extract all our fields in a new
        // Incremental value provider
        var librarySchema = libraryInfo
            // We have to combine with the compilation provider again to get access to the Assembly
            .Combine(context.CompilationProvider)
            .Select(static (context, _) => {
                var (metaRef, compilation) = context;
                if (metaRef == null) return null;

                // Find the Brutal Numerics Assembly then Namespace containing all the types to JSONify
                var brutalAssembly = compilation.SourceModule.ReferencedAssemblySymbols
                    .FirstOrDefault(a => a.Name.Contains(AssemblyName));

                var numericsNamespace = brutalAssembly?.GlobalNamespace
                    .GetNamespaceMembers().FirstOrDefault(ns => ns.Name == "Brutal")
                    ?.GetNamespaceMembers().FirstOrDefault(ns => ns.Name == "Numerics");
                if (numericsNamespace == null) return null;

                // Find types that have members we're allowed to serialise
                var typeSchemata = numericsNamespace.GetTypeMembers()
                    .Where(typeSymbol => !typeSymbol.IsAbstract)
                    .Select(typeSymbol => {
                            // Get members with the XmlAttribute Attribute (Which helpfully marks them as serialisable)
                            var memberSchemata = typeSymbol.GetMembers()
                                .Where(m => m.GetAttributes()
                                    .Any(a => a.AttributeClass?.Name == "XmlAttributeAttribute")
                                )
                                .Select(m => {
                                        var type = m switch {
                                            IFieldSymbol fieldSymbol => fieldSymbol.Type.ToDisplayString(),
                                            IPropertySymbol propertySymbol => propertySymbol.Type.ToDisplayString(),
                                            _ => null
                                        };
                                        return type != null
                                            ? new MemberSchema(
                                                m.Name,
                                                type
                                            )
                                            : null;
                                    }
                                )
                                // Remove Nulls
                                .OfType<MemberSchema>()
                                .ToArray();
                            // If no members, then the class is not serialisable
                            return memberSchemata.Length == 0
                                ? null
                                : new TypeSchema(typeSymbol.Name,
                                    typeSymbol.ContainingNamespace.ToDisplayString(),
                                    new EquatableArray<MemberSchema>(memberSchemata)
                                );
                        }
                    )
                    // Remove Nulls
                    .OfType<TypeSchema>()
                    .ToArray();

                return new LibrarySchema(new EquatableArray<TypeSchema>(typeSchemata));
            });

        // Generate the source code.
        context.RegisterSourceOutput(librarySchema, GenerateCode);
    }

    /// <summary>
    /// Generate the classes for the JSON Converters
    /// </summary>
    /// <param name="productionContext"></param>
    /// <param name="librarySchema"></param>
    private static void GenerateCode(SourceProductionContext productionContext, LibrarySchema? librarySchema) {
        const string nameSpace = "DatEngine.Util.Json.Generated";

        // Quick check to make sure we've not made it this far without any successful context
        if (librarySchema is null) return;

        List<string> converters = [];

        // Convert every class we found into a JSON Converter
        foreach (var type in librarySchema.types) {
            var newClassName = $"{type.name}JsonConverter";

            StringBuilder readValues = new();
            StringBuilder writeValues = new();

            foreach (var member in type.members) {
                // There's a specific JSON method for each of these
                var getter = member.type switch {
                    "double" => "reader.GetDouble()",
                    "float" => "reader.GetSingle()",
                    "sbyte" => "reader.GetSByte()",
                    "byte" => "reader.GetByte()",
                    "short" => "reader.GetInt16()",
                    "ushort" => "reader.GetUInt16()",
                    "int" => "reader.GetInt32()",
                    "uint" => "reader.GetUInt32()",
                    "long" => "reader.GetInt64()",
                    "ulong" => "reader.GetUInt64()",
                    "bool" => "reader.GetBoolean()",
                    // We can fallback to see if JsonSerialiser knows what to do with this value
                    _ => $"JsonSerializer.Deserialize<{member.type}>(ref reader, options)"
                };

                // Turns out you have to read, then call the GetType method to interpret that read value
                readValues.AppendLine("             reader.Read();");
                readValues.AppendLine($"             {member.type} {member.name} = {getter};");

                // There's fewer methods required for interpreting these
                switch (member.type) {
                    case "double":
                    case "float":
                    case "sbyte":
                    case "byte":
                    case "short":
                    case "ushort":
                    case "int":
                    case "uint":
                    case "long":
                    case "ulong":
                        writeValues.AppendLine($"            writer.WriteNumberValue(value.{member.name});");
                        break;
                    case "bool":
                        writeValues.AppendLine($"            writer.WriteBooleanValue(value.{member.name});");
                        break;
                    default:
                        // We can also fall back here
                        writeValues.AppendLine($"            JsonSerializer.Serialize(writer, value.{member.name}, options);");
                        break;
                }
            }

            var code = $@"// <auto-generated/>
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using {type.nameSpace};

namespace {nameSpace} {{
    /// <summary>
    /// Generated class to support <see cref=""{type.name}""/> in Json serialisation and deserialisation
    /// </summary>
    public class {newClassName} : JsonConverter<{type.name}> {{
        public override {type.name} Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) {{
            // We don't need to parse the first array bracket, that's already been done for us
            if (reader.TokenType != JsonTokenType.StartArray) {{
                throw new JsonException(""Expected an array"");
            }}
            
{readValues}
            // We do need to parse the last array bracket though
            reader.Read();
            
            return new {type.name}({string.Join(", ", type.members.Select(m => m.name))});
        }}
        
        public override void Write(Utf8JsonWriter writer, {type.name} value, JsonSerializerOptions options) {{
            writer.WriteStartArray();
{writeValues}
            writer.WriteEndArray();
        }}
    }}
}}
";

            productionContext.AddSource($"{newClassName}.gen.cs", SourceText.From(code, Encoding.UTF8));
            converters.Add(newClassName);
        }

        // Build a class that'll get us all of these at once
        var accessorCode = $@"// <auto-generated/>
using System;
using System.Text.Json.Serialization;

namespace {nameSpace} {{
    public sealed class BrutalNumericsJson {{
        public static IList<JsonConverter> BrutalNumericConverters => [
{string.Join(",\n", converters.Select(jsonClass => $"            new {jsonClass}()"))}
        ];
    }}
}}
";

        productionContext.AddSource("BrutalNumericConverters.gen.cs", SourceText.From(accessorCode, Encoding.UTF8));
    }
}