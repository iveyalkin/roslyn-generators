using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IV.UnityBinder
{
    [Generator]
    class ComponentBinderGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: (ctx, _) => GetClassWithRequireComponentInfo(ctx))
                .Where(m => m is not null);

            context.RegisterSourceOutput(classDeclarations,
                (spc, classInfo) => GenerateComponentBinding(spc, classInfo!));
        }

        private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        {
            if (node is not ClassDeclarationSyntax classDeclaration)
                return false;

            return classDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Select(attr => attr.Name.ToString())
                .Any(attr => attr == "RequireComponent");
        }

        private ClassInfo GetClassWithRequireComponentInfo(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            if (!HasMonoBehaviourBase(classDeclaration, semanticModel))
                return null;

            var components = GetRequireComponentAttributes(classDeclaration, semanticModel).ToList();
            if (!components.Any())
                return null;

            // look for OnAwake method
            // could be improved by looking for a specific interface like IAwakable
            // that should contains the OnAwake method 
            var hasOnAwake = classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.Text == "OnAwake");

            return new ClassInfo(
                classDeclaration.Identifier.Text,
                GetNamespace(classDeclaration),
                components,
                hasOnAwake
            );
        }

        private string GetNamespace(ClassDeclarationSyntax classDeclaration)
        {
            var parent = classDeclaration.Parent;
            while (parent != null)
            {
                if (parent is NamespaceDeclarationSyntax ns)
                    return ns.Name.ToString();

                parent = parent.Parent;
            }

            return null;
        }

        private bool HasMonoBehaviourBase(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            var baseType = classSymbol?.BaseType;

            while (baseType != null)
            {
                if (baseType.Name == "MonoBehaviour" && baseType.ContainingNamespace.ToString() == "UnityEngine")
                    return true;

                baseType = baseType.BaseType;
            }
            return false;
        }

        private IEnumerable<ITypeSymbol> GetRequireComponentAttributes(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
        {
            var attributes = classDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Where(a => semanticModel.GetTypeInfo(a).Type?.Name == "RequireComponent");

            foreach (var attribute in attributes)
            {
                if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf)
                {
                    var typeSymbol = semanticModel.GetTypeInfo(typeOf.Type).ConvertedType;
                    if (typeSymbol != null)
                    {
                        yield return typeSymbol;
                    }
                }
            }
        }

        private void GenerateComponentBinding(SourceProductionContext context, ClassInfo classInfo)
        {
            var fieldDeclarations = new StringBuilder();
            var componentBindings = new StringBuilder();

            foreach (var component in classInfo.Components)
            {
                var typeName = component.ToDisplayString();
                var fieldName = $"{char.ToLower(component.Name[0])}{component.Name.Substring(1)}";
                fieldDeclarations.AppendLine($"protected {typeName} {fieldName};");
                componentBindings.AppendLine($"{fieldName} = GetComponent<{typeName}>();");
            }

            var hasNamespace = !string.IsNullOrEmpty(classInfo.Namespace);
            var namespaceDeclaration = hasNamespace ? $"namespace {classInfo.Namespace}" : "";

            var source = $@"// auto-generated

using UnityEngine;

{namespaceDeclaration}
{(hasNamespace ? "{" : "")}
public partial class {classInfo.ClassName}
{{
    {fieldDeclarations}

    protected virtual void Awake()
    {{
        InjectComponents();
        {(classInfo.HasOnAwake ? "OnAwake();" : string.Empty)}
    }}

    private void InjectComponents()
    {{
{componentBindings}
    }}
}}
{(hasNamespace ? "}" : "")}";

            context.AddSource($"{classInfo.ClassName}.g.cs", source);
        }

        private record ClassInfo(string ClassName, string Namespace, List<ITypeSymbol> Components, bool HasOnAwake);
    }
}