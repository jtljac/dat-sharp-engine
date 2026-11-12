using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DatGenerator.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DatGenerator;

/// <summary>
/// A source generator that takes static classes annotated as a CVar and generates code for registering them to the
/// global CVar System
/// </summary>
[Generator]
public class CVarGenerator : IIncrementalGenerator {
    private const string Namespace = "DatEngine.Util";
    private const string AttributeName = "CVarAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Filter Fields annotated with the [CVar] attribute and extract relevent values.
        var provider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{Namespace}.{AttributeName}",
                static (s, _) => s is VariableDeclaratorSyntax,
                static (ctx, _) => GetFieldForSourceGen(ctx)
            )
            .Where(static t => t != null);

        // Generate the source code.
        context.RegisterSourceOutput(provider.Collect(),
            (ctx, t) => GenerateCode(t!, ctx)
        );
    }

    /// <summary>
    /// Check if the given symbol is a field with the CVar Attribute
    /// </summary>
    /// <param name="context">Syntax context, based on CreateSyntaxProvider predicate</param>
    /// <returns>The field symbol, or null if none found.</returns>
    private static Result<CVarSchema>? GetFieldForSourceGen(GeneratorAttributeSyntaxContext context) {
        var variableDeclarationSyntax = (VariableDeclaratorSyntax) context.TargetNode;


        var field = context.SemanticModel.GetDeclaredSymbol(variableDeclarationSyntax);
        if (field is null) {
            return null;
        }

        var attribute = field.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == $"{Namespace}.{AttributeName}"
        );
        if (attribute is null) {
            return null;
        }

        // We can't throw diagnostics here, so we've got to pass them through
        // We also want to collect as many as possible, so we won't exit early if we see any
        // If we've made it this far, we can collect enough values or defaults anyway
        List<DiagnosticInfo> diagnostics = [];

        // Must be static so we can edit it from the auto initialisation function
        if (!field.IsStatic) {
            diagnostics.Add(new DiagnosticInfo(
                    new DiagnosticDescriptor(
                        "CVAR001",
                        "CVar Definition Must be Static",
                        $"{field.Name} is required to be static for auto initialisation",
                        "DatEngine.Usage",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    field.Locations.FirstOrDefault()
            ));
        }

        // Must be public so we can edit it from the auto initialisation
        if (field.DeclaredAccessibility != Accessibility.Public) {
            diagnostics.Add(new DiagnosticInfo(
                    new DiagnosticDescriptor(
                        "CVAR002",
                        "CVar Definition Must be Public",
                        $"{field.Name} is required to be public for auto initialisation",
                        "DatEngine.Usage",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    field.Locations.FirstOrDefault()
            ));
        }

        if (context.TargetNode is VariableDeclaratorSyntax { Initializer: null }) {
            diagnostics.Add(new DiagnosticInfo(
                new DiagnosticDescriptor(
                    "CVAR005",
                    "CVar Must had a default value",
                    $"{field.Name} must be instantiated with a value for it's default",
                    "DatEngine.Usage",
                    DiagnosticSeverity.Error,
                    true
                ),
                field.Locations.FirstOrDefault()
            ));
        }

        // It'd be much easier to access these by name, rather than by index
        var attributeArgs = attribute.ConstructorArguments
            .Select((arg, index) => (index, arg))
            .ToDictionary(
                pair => attribute.AttributeConstructor!.Parameters[pair.index].Name,
                arg => arg.arg.Value
        );

        var cVarName = attributeArgs.TryGetValue("name", out var nameVal) ? (string) nameVal! : "";
        var cVarDescription = attributeArgs.TryGetValue("description", out var descriptionVal) ? (string) descriptionVal! : "";
        var cVarFlags = $"(CVarFlags) {(attributeArgs.TryGetValue("flags", out var flagValue) ? (int) flagValue! : 0)}";

        // Gotta have a name, can't store and retrieve it without a name
        if (cVarName.Length == 0) {
            diagnostics.Add(new DiagnosticInfo(
                new DiagnosticDescriptor(
                    "CVAR003",
                    "CVar Must have a name",
                    $"{field.Name}'s CVar Name is required to be set for console and lookups",
                    "DatEngine.Usage",
                    DiagnosticSeverity.Error,
                    true
                ),
                field.Locations.FirstOrDefault()
            ));
        }

        return new Result<CVarSchema>(
            new CVarSchema(field.ContainingType.ToDisplayString(),
                field.Name,
                cVarName,
                cVarDescription,
                cVarFlags,
                LocationInfo.Create(field.Locations.FirstOrDefault())
            ),
            diagnostics
        );
    }

    /// <summary>
    /// Generate code action.
    /// It will be executed on specific nodes (ClassDeclarationSyntax annotated with the [Report] attribute) changed by the user.
    /// </summary>
    /// <param name="fields">The fields that have CVars</param>
    /// <param name="context">Source generation context used to add source files.</param>
    private static void GenerateCode(ImmutableArray<Result<CVarSchema>> fields, SourceProductionContext context) {
        // Track seen names so we can report duplicates
        var seenFieldNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        StringBuilder initBuilder = new();
        foreach (var field in fields) {
            // Check problems, report problems
            if (field.diagnostics.Length > 0) {
                foreach (var diagnostic in field.diagnostics) {
                    context.ReportDiagnostic(diagnostic);
                }

                // If problems, can't build
                continue;
            }

            if (!seenFieldNames.Add(field.value.name)) {
                // We don't have the location context here, we'll just have to let it be free
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "CVAR004",
                        "CVar names must be unique",
                        $"{field.value.name} has been used more than once, CVar Names must be case insensitive unique so it doesn't collide during lookups",
                        "DatEngine.Usage",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    field.value.fieldLocation
                ));
                continue;
            }

            initBuilder.AppendLine($"            {field.value.classPath}.{field.value.fieldName} = cVarManager.Register(\"{field.value.name}\", {field.value.classPath}.{field.value.fieldName}.value, \"{field.value.description}\", {field.value.flags});");
        }

        string code = $@"// <auto-generated/>
using System;

namespace {Namespace} {{
    public static class CVarAutoInitialiser {{
        public static void InitialiseAll(CVarManager cVarManager) {{
{initBuilder}
        }}
    }}
}}
";

        context.AddSource("CVarAutoInit.gen.cs", SourceText.From(code, Encoding.UTF8));
    }
}

/// <summary>
/// Record representing a CVar and it's config
/// </summary>
/// <param name="classPath">The classpath of the variable storing the CVar</param>
/// <param name="fieldName">The name of the variable storing the CVar</param>
/// <param name="name">The given name of the CVar</param>
/// <param name="description">The given Description of the CVar</param>
/// <param name="flags">The given Flags of the CVar</param>
internal record CVarSchema(string classPath, string fieldName, string name, string description, string flags, LocationInfo? fieldLocation);