using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace MermaidFromAssembly
{
    // Having the path to an assembly, get all the public types in that assembly
    // As type grouped by namespace, then get all the public and protected members
    // and generate a mermaid diagram from that. The diagram should also include the
    // inheritance hierarchy of the types, the interface implementation, and the members
    // as relationships if to a type of the same assembly. The types should be grouped
    // by namespace. The mermaid diagram should be saved to a file in the c:\temp folder
    // of the same name as the assembly but with extension .md
    internal class Program
    {
        static void Main(string[] args)
        {
            args = [@"D:\gh\abstractions\microsoft-identity-abstractions-for-dotnet\src\Microsoft.Identity.Abstractions\bin\Debug\net8.0\Microsoft.Identity.Abstractions.dll"];

            //args = [@"D:\gh\mise\MISE\tests\Experimental\MiseAuthN\Mise.Authentication.Tests\bin\Debug\net8.0\Mise.Authentication.dll" ];
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide the path to an assembly as an argument.");
                return;
            }

            string assemblyPath = args[0];
            try
            {
                // Load the assembly
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                // Get all public types grouped by namespace
                var typesByNamespace = assembly.GetExportedTypes()
                    .GroupBy(t => t.Namespace ?? "Global")
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Generate the mermaid diagram
                string mermaidDiagram = GenerateMermaidDiagram(assembly, typesByNamespace);

                // Save to file in c:\temp with assembly name and .md extension
                string fileName = Path.GetFileNameWithoutExtension(assemblyPath);
                string outputPath = Path.Combine(@"c:\temp", $"{fileName}.md");
                File.WriteAllText(outputPath, mermaidDiagram);

                Console.WriteLine($"Mermaid diagram saved to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing assembly: {ex.Message}");
            }
        }

        static string GenerateMermaidDiagram(Assembly assembly, Dictionary<string, List<Type>> typesByNamespace)
        {
            StringBuilder sb = new StringBuilder();

            // Start the mermaid class diagram
            sb.AppendLine("```mermaid");
            sb.AppendLine("classDiagram");

            HashSet<string> processedRelationships = new HashSet<string>();

            // Process each namespace
            foreach (var namespaceGroup in typesByNamespace)
            {
                string namespaceName = namespaceGroup.Key;
                List<Type> types = namespaceGroup.Value;

                // Add namespace as a comment for grouping
                sb.AppendLine($"    namespace {namespaceName}{{");

                // Process each type in the namespace
                foreach (Type type in types)
                {
                    // Define the class/interface
                    string typeDefinition = type.IsEnum ? "<<enum>>" : type.IsInterface ? "<<interface>>" : (type.IsAbstract ? "<<abstract>>" : "");
                    if (!string.IsNullOrEmpty(typeDefinition))
                    {
                        sb.AppendLine($"    class {SanitizeName(type)} {{ {typeDefinition}");
                    }
                    else
                    {
                        sb.AppendLine($"    class {SanitizeName(type)} {{");
                    }

                    // Add members to the class
                    AddMembers(sb, type, assembly);
                    sb.AppendLine("    }");
                }

                sb.AppendLine($"    }}");



                // Process each type in the namespace
                foreach (Type type in types)
                {
                    // Add inheritance relationship
                    if (type.BaseType != null && type.BaseType != typeof(object) &&
                        assembly.GetExportedTypes().Contains(type.BaseType))
                    {
                        string relationship = $"{SanitizeName(type.BaseType)} <|-- {SanitizeName(type)} : Inherits";
                        if (!processedRelationships.Contains(relationship))
                        {
                            sb.AppendLine($"    {relationship}");
                            processedRelationships.Add(relationship);
                        }
                    }

                    // Add interface implementations
                    foreach (var interfaceType in type.GetInterfaces())
                    {
                        if (assembly.GetExportedTypes().Contains(interfaceType))
                        {
                            string relationship = $"{SanitizeName(interfaceType)} <|.. {SanitizeName(type)} : Implements";
                            if (!processedRelationships.Contains(relationship))
                            {
                                sb.AppendLine($"    {relationship}");
                                processedRelationships.Add(relationship);
                            }
                        }
                    }
                }

                sb.AppendLine();
            }

            // Add member relationships between types
            AddTypeRelationships(sb, assembly, typesByNamespace, processedRelationships);

            // End the mermaid diagram
            sb.AppendLine("```");
            return sb.ToString();
        }

        static void AddMembers(StringBuilder sb, Type type, Assembly assembly)
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic |
                              BindingFlags.Instance | BindingFlags.Static |
                              BindingFlags.DeclaredOnly;

            // Get public and protected members
            var members = type.GetMembers(bindingFlags)
                .Where(m => m.DeclaringType == type &&
                       (m.IsPublic() || m.IsProtected()))
                .ToList();

            foreach (var member in members)
            {
                string accessModifier = member.IsPublic() ? "+" : "#";
                string memberType = GetMemberType(member);
                string memberName = member.Name;

                // For methods, include parameters
                if (member is MethodInfo method && !method.IsSpecialName)
                {
                    var parameters = method.GetParameters();
                    string paramList = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    string returnType = SanitizeName(method.ReturnType);
                    sb.AppendLine($"    {accessModifier}{returnType} {memberName}({paramList})");
                }
                // For properties
                else if (member is PropertyInfo property)
                {
                    string propertyType = SanitizeName(property.PropertyType);
                    sb.AppendLine($"    {accessModifier}{propertyType} {memberName}");
                }
                // For fields
                else if (member is FieldInfo field)
                {
                    string fieldType = SanitizeName(field.FieldType);
                    if (member.DeclaringType.IsEnum)
                    {
                        if (memberName != "value__")
                        {
                            sb.AppendLine($"    {memberName} = {field.GetValue(null)}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"    {accessModifier}{fieldType} {memberName}");
                    }
                }
                // Skip constructors, events, etc.
            }
        }

        static void AddTypeRelationships(StringBuilder sb, Assembly assembly,
                                      Dictionary<string, List<Type>> typesByNamespace,
                                      HashSet<string> processedRelationships)
        {
            var allTypes = assembly.GetExportedTypes().ToList();

            foreach (var type in allTypes)
            {
                // Check public and protected properties
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                          BindingFlags.Instance | BindingFlags.Static))
                {
                    if (property.DeclaringType != type || !(property.GetMethod?.IsPublic() == true ||
                                                           property.GetMethod?.IsProtected() == true))
                        continue;

                    AddRelationshipIfSameAssembly(sb, assembly, type, property.PropertyType,
                                               $"{SanitizeName(type)} --> {SanitizeName(property.PropertyType)} : Has",
                                               processedRelationships);
                }

                // Check fields
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.Instance | BindingFlags.Static))
                {
                    if (field.DeclaringType != type || !(field.IsPublic || field.IsFamily))
                        continue;

                    AddRelationshipIfSameAssembly(sb, assembly, type, field.FieldType,
                                              $"{SanitizeName(type)} --> {SanitizeName(field.FieldType)} : Has",
                                              processedRelationships);
                }
            }
        }

        static void AddRelationshipIfSameAssembly(StringBuilder sb, Assembly assembly,
                                              Type sourceType, Type targetType,
                                              string relationship,
                                              HashSet<string> processedRelationships)
        {
            // Handle collections
            if (targetType.IsGenericType &&
                (typeof(IEnumerable<>).IsAssignableFrom(targetType.GetGenericTypeDefinition()) ||
                 typeof(ICollection<>).IsAssignableFrom(targetType.GetGenericTypeDefinition()) ||
                 typeof(IList<>).IsAssignableFrom(targetType.GetGenericTypeDefinition())))
            {
                targetType = targetType.GetGenericArguments()[targetType.GetGenericArguments().Length-1];
                relationship = relationship.Replace("Has", "Has many");
            }

            // Check if target type is from the same assembly and is not primitive or from System namespace
            if (assembly.GetExportedTypes().Contains(targetType) &&
                !targetType.IsPrimitive &&
                !targetType.Namespace?.StartsWith("System") == true &&
                targetType != sourceType)
            {
                if (!processedRelationships.Contains(relationship))
                {
                    sb.AppendLine($"    {relationship}");
                    processedRelationships.Add(relationship);
                }
            }
        }

        static string GetMemberType(MemberInfo member)
        {
            return member.MemberType switch
            {
                MemberTypes.Constructor => "constructor",
                MemberTypes.Field => "field",
                MemberTypes.Method => "method",
                MemberTypes.Property => "property",
                MemberTypes.Event => "event",
                _ => member.MemberType.ToString().ToLower()
            };
        }

        static string SanitizeName(Type t)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (t.IsGenericType)
            {
                stringBuilder.Append(t.Name.Substring(0, t.Name.IndexOf('`')));
                stringBuilder.Append("<");
                stringBuilder.Append(string.Join(", ", t.GetGenericArguments().Select(SanitizeName)));
                stringBuilder.Append(">");
            }
            else
            {
                stringBuilder.Append(t.Name);
            }

            return stringBuilder.ToString();
        }
    }

    // Extension methods for checking access modifiers
    public static class ReflectionExtensions
    {
        public static bool IsPublic(this MemberInfo member)
        {
            return member switch
            {
                MethodInfo method => method.IsPublic,
                PropertyInfo property => (property.GetMethod?.IsPublic == true) || (property.SetMethod?.IsPublic == true),
                FieldInfo field => field.IsPublic,
                EventInfo eventInfo => eventInfo.AddMethod?.IsPublic == true,
                Type type => type.IsPublic,
                _ => false
            };
        }

        public static bool IsProtected(this MemberInfo member)
        {
            return member switch
            {
                MethodInfo method => method.IsFamily || method.IsFamilyOrAssembly,
                PropertyInfo property => (property.GetMethod?.IsFamily == true) || (property.GetMethod?.IsFamilyOrAssembly == true) ||
                                        (property.SetMethod?.IsFamily == true) || (property.SetMethod?.IsFamilyOrAssembly == true),
                FieldInfo field => field.IsFamily || field.IsFamilyOrAssembly,
                EventInfo eventInfo => eventInfo.AddMethod?.IsFamily == true || eventInfo.AddMethod?.IsFamilyOrAssembly == true,
                Type type => type.IsNestedFamily || type.IsNestedFamORAssem,
                _ => false
            };
        }
    }
}
