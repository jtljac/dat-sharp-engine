using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;


namespace CVarGenerator;

// TODO: Rewrite to extract required values at the transform for caching
/// <summary>
/// A source generator that takes static classes annotated as a CVar and generates code for registering them to the
/// global CVar System
/// </summary>
[Generator]
public class CVarGenerator : IIncrementalGenerator {
    private const string Namespace = "DatEngine.Util";
    private const string AttributeName = "CVarAttribute";


    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Filter classes annotated with the [CVar] attribute.
        IncrementalValuesProvider<IFieldSymbol?> provider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{Namespace}.{AttributeName}",
                static (s, _) => s is FieldDeclarationSyntax,
                static (ctx, _) => GetFieldForSourceGen(ctx)
            )
            .Where(static t => t != null);

        // Generate the source code.
        context.RegisterSourceOutput(context.CompilationProvider.Combine(provider.Collect()),
            (ctx, t) => GenerateCode(t.Right!, ctx)
        );
    }

    /// <summary>
    /// Check if the given symbol is a field with the CVar Attribute
    /// </summary>
    /// <param name="context">Syntax context, based on CreateSyntaxProvider predicate</param>
    /// <returns>The field symbol, or null if none found.</returns>
    private static IFieldSymbol? GetFieldForSourceGen(GeneratorAttributeSyntaxContext context) {
        var fieldDeclarationSyntax = (FieldDeclarationSyntax) context.TargetNode;

        // Check all the fields on the line
        foreach (var variable in fieldDeclarationSyntax.Declaration.Variables) {
            if (context.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol) {
                // Skip non fields
                continue;
            }

            // Check if any attributes match our CVar attribute
            if (fieldSymbol.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == $"{Namespace}.{AttributeName}"
                )) {

                return fieldSymbol;
            }
        }

        return null;
    }

    /// <summary>
    /// Generate code action.
    /// It will be executed on specific nodes (ClassDeclarationSyntax annotated with the [Report] attribute) changed by the user.
    /// </summary>
    /// <param name="fields">The fields that </param>
    /// <param name="context">Source generation context used to add source files.</param>
    private void GenerateCode(ImmutableArray<IFieldSymbol> fields, SourceProductionContext context) {
        // Start of the file

        HashSet<string> seenFieldNames = [];
        StringBuilder initBuilder = new();
        foreach (var field in fields) {
            if (!field.IsStatic) {
                context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "CVAR001",
                            "CVar Definition Must be Static",
                            $"{field.Name} is required to be static for auto initialisation",
                            "DatEngine.Design",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        field.Locations.FirstOrDefault()
                ));

                continue;
            }
            if (field.DeclaredAccessibility != Accessibility.Public) {
                context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "CVAR002",
                            "CVar Definition Must be Public",
                            $"{field.Name} is required to be public for auto initialisation",
                            "DatEngine.Design",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        field.Locations.FirstOrDefault()
                ));

                continue;
            }

            var attribute = field.GetAttributes().FirstOrDefault(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == $"{Namespace}.{AttributeName}"
            );

            if (attribute == null) {
                throw new Exception("Failed to find attribute for field " + field.Name);
            }

            var attributeArgs = attribute.ConstructorArguments
                .Select((arg, index) => (index, arg))
                .ToDictionary(
                    pair => attribute.AttributeConstructor!.Parameters[pair.index].Name,
                    arg => arg.arg.Value
                );

            var classPath = field.ContainingType.ToDisplayString();
            var fieldName = field.Name;
            var cVarName = attributeArgs.TryGetValue("name", out var nameVal) ? (string) nameVal! : "";
            var cVarDescription = attributeArgs.TryGetValue("description", out var descriptionVal) ? (string) descriptionVal! : "";
            var cVarFlags = $"(CVarFlags) {(attributeArgs.TryGetValue("flags", out var flagValue) ? (int) flagValue! : 0)}";

            if (cVarName.Length == 0) {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "CVAR003",
                        "CVar Must have a name",
                        $"{field.Name}'s CVar Name is required to be set for console and lookups",
                        "DatEngine.Design",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    field.Locations.FirstOrDefault()
                ));

                continue;
            }

            if (!seenFieldNames.Add(cVarName)) {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "CVAR004",
                        "CVar names must be unique",
                        $"{field.Name}'s CVar name must be case insensitive unique to be unique as so it doesn't collide during lookups",
                        "DatEngine.Design",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    field.Locations.FirstOrDefault()
                ));
                continue;
            }

            initBuilder.AppendLine($"            {classPath}.{fieldName} = cVarManager.Register(\"{cVarName}\", {classPath}.{fieldName}.value, \"{cVarDescription}\", {cVarFlags});");
            // Add the source code to the compilation.
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