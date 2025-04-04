using System.Reflection;
using System.Text;

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
            // args = [@"D:\gh\abstractions\microsoft-identity-abstractions-for-dotnet\src\Microsoft.Identity.Abstractions\bin\Debug\net8.0\Microsoft.Identity.Abstractions.dll"];

            //args = [@"D:\gh\mise\MISE\tests\Experimental\MiseAuthN\Mise.Authentication.Tests\bin\Debug\net8.0\Mise.Authentication.dll" ];
            if (args.Length == 0)
            {
                args = ["D:\\gh\\sal\\sal1\\test\\NewConfigTests\\bin\\Debug\\net8.0\\NewConfigTests.dll"];
                //Console.WriteLine("Please provide the path to an assembly as an argument.");
                //return;
            }

            string assemblyPath = args[0];
            string? categoryFilePath = args.Length > 1 ? args[1] : null;

            try
            {
                // Load the assembly
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                // Get all public types grouped by namespace
                var typesByNamespace = assembly.GetExportedTypes()
                    .GroupBy(t => t.Namespace ?? "Global")
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Read categories if a category file is provided
                Dictionary<string, List<string>> categories =
                    !string.IsNullOrEmpty(categoryFilePath) ?
                    ReadCategoriesFromFile(categoryFilePath) :
                    new Dictionary<string, List<string>>();

                // Generate the mermaid diagram
                string mermaidDiagram = GenerateMermaidDiagram(assembly, typesByNamespace, categories);

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

        static string GenerateMermaidDiagram(Assembly assembly,
                                          Dictionary<string, List<Type>> typesByNamespace,
                                          Dictionary<string, List<string>> categories)
        {
            StringBuilder sb = new StringBuilder();

            // Start the mermaid class diagram
            sb.AppendLine("```mermaid");
            sb.AppendLine("classDiagram");
            sb.AppendLine("direction LR");

            HashSet<string> processedRelationships = new HashSet<string>();

            // Process each namespace
            foreach (var namespaceGroup in typesByNamespace)
            {
                string namespaceName = namespaceGroup.Key;
                List<Type> types = namespaceGroup.Value;

                // Group types by category within this namespace
                Dictionary<string, List<Type>> typesByCategory = new Dictionary<string, List<Type>>();
                typesByCategory["Uncategorized"] = new List<Type>();

                // Initialize categories
                foreach (var category in categories.Keys)
                {
                    typesByCategory[category] = new List<Type>();
                }

                // Assign types to categories
                foreach (Type type in types)
                {
                    bool categorized = false;
                    foreach (var category in categories)
                    {
                        if (category.Value.Contains(type.Name))
                        {
                            typesByCategory[category.Key].Add(type);
                            categorized = true;
                            break;
                        }
                    }

                    if (!categorized)
                    {
                        typesByCategory["Uncategorized"].Add(type);
                    }
                }

                // Process each category
                foreach (var categoryGroup in typesByCategory)
                {
                    if (!categoryGroup.Value.Any())
                        continue;

                    if (categoryGroup.Key != "Uncategorized")
                    {
                        // Add subnamespace for category
                        sb.AppendLine($"        namespace {categoryGroup.Key} {{");
                    }
                    else
                    {
                        // Add namespace as a comment for grouping
                        sb.AppendLine($"    namespace {namespaceName} {{");
                    }


                    // Process each type in the category
                    foreach (Type type in categoryGroup.Value)
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

                    sb.AppendLine(" }");
                    sb.AppendLine();
                }

                // Process relationships for this namespace
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
                    string paramList = string.Join(", ", parameters.Select(p => $"{SanitizeName(p.ParameterType)} {p.Name}"));
                    string returnType = SanitizeName(method.ReturnType);
                    sb.AppendLine($"    {accessModifier}{returnType} {memberName}({paramList})");
                }
                // For properties
                else if (member is PropertyInfo property)
                {
                    string accessors = property.CanWrite && property.CanRead ? "&lt;&lt;rw&gt;&gt;" :
                                       property.CanRead ? "&lt;&lt;ro&gt;&gt;" :
                                       property.CanWrite ? "&lt;&lt;wo&gt;&gt;" : "";
                    string propertyType = SanitizeName(property.PropertyType);
                    sb.AppendLine($"    {accessors} {accessModifier}{propertyType} {memberName}");
                }
                // For fields
                else if (member is FieldInfo field)
                {
                    string fieldType = SanitizeName(field.FieldType);
                    if (member.DeclaringType != null && member.DeclaringType.IsEnum)
                    {
                        if (memberName != "value__")
                        {
                            sb.AppendLine($"    {memberName} = {field.GetRawConstantValue()}");
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
                                               $"{SanitizeName(type)} --> \"{property.Name}\" {SanitizeName(property.PropertyType)} : Has",
                                               processedRelationships);
                }

                // Check fields
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.Instance | BindingFlags.Static))
                {
                    if (field.DeclaringType != type || !(field.IsPublic || field.IsFamily))
                        continue;

                    AddRelationshipIfSameAssembly(sb, assembly, type, field.FieldType,
                                              $"{SanitizeName(type)} --> \"{field.Name}\" {SanitizeName(field.FieldType)} : Has",
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
                 typeof(IList<>).IsAssignableFrom(targetType.GetGenericTypeDefinition()) ||
                 typeof(IDictionary<,>).IsAssignableFrom(targetType.GetGenericTypeDefinition()) ||
                 typeof(System.Collections.IEnumerable).IsAssignableFrom(targetType.GetGenericTypeDefinition())
                 ))
            {
                var targetType2 = targetType.GetGenericArguments()[targetType.GetGenericArguments().Length - 1];
                relationship = relationship.Replace(SanitizeName(targetType), SanitizeName(targetType2));
                relationship = relationship.Replace("Has", "Has many");
                targetType = targetType2;
            }

            if (targetType.IsValueType)
            {
                relationship = relationship.Replace("-->", "*--");
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

        static string SanitizeName(Type? t)
        {
            if (t == null)
            {
                return string.Empty;
            }

            StringBuilder stringBuilder = new StringBuilder();
            if (t.IsGenericType)
            {
                stringBuilder.Append(t.Name.Substring(0, t.Name.IndexOf('`')));
                stringBuilder.Append("&lt;");
                stringBuilder.Append(string.Join(", ", t.GetGenericArguments().Select(SanitizeName)));
                stringBuilder.Append("&gt;");
            }
            else if (t.IsGenericTypeDefinition)
            {
                stringBuilder.Append(t.Name.Substring(0, t.Name.IndexOf('`')));
                stringBuilder.Append("&lt;");
                stringBuilder.Append(string.Join(", ", t.GetGenericArguments().Select(SanitizeName)));
                stringBuilder.Append("&gt;");
            }
            else if (t.IsArray)
            {
                stringBuilder.Append(SanitizeName(t.GetElementType()));
                stringBuilder.Append("[]");
            }
            else if (t.IsNested)
            {
                stringBuilder.Append(SanitizeName(t.DeclaringType));
                stringBuilder.Append(".");
                stringBuilder.Append(t.Name);
            }
            else if (t.FullName == "System.String")
            {
                stringBuilder.Append("string");
            }
            else if (t.FullName == "System.Boolean")
            {
                stringBuilder.Append("bool");
            }
            else
            {
                stringBuilder.Append(t.Name);
            }

            return stringBuilder.ToString();
        }

        // Read and parse the category file
        static Dictionary<string, List<string>> ReadCategoriesFromFile(string? categoryFilePath)
        {
            Dictionary<string, List<string>> categories = new Dictionary<string, List<string>>();

            if (categoryFilePath == null || !File.Exists(categoryFilePath))
                return categories;

            string currentCategory = "";

            foreach (var line in File.ReadAllLines(categoryFilePath))
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                if (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("*"))
                {
                    // This is a class entry
                    if (!string.IsNullOrEmpty(currentCategory))
                    {
                        string className = trimmedLine.Substring(1).Trim();
                        if (!categories[currentCategory].Contains(className))
                        {
                            categories[currentCategory].Add(className);
                        }
                    }
                }
                else
                {
                    // This is a category
                    currentCategory = trimmedLine;
                    if (!categories.ContainsKey(currentCategory))
                    {
                        categories[currentCategory] = new List<string>();
                    }
                }
            }

            return categories;
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
