# MermaidFromAssembly

A .NET tool that generates Mermaid class diagrams from .NET assemblies, helping visualize the structure and relationships of types within an assembly.

## Features

- Generates class diagrams in Mermaid syntax from .NET assemblies
- Groups types by namespace and optional categories
- Shows class inheritance hierarchies and interface implementations
- Displays the public API (public and protected members)
- Visualizes relationships between types (associations, collections)
- Supports generic types and collections
- Handles access modifiers and obsolete members

## Usage

```bash
MermaidFromAssembly.exe <assembly-path> [category-file-path] [options]
```

### Arguments

- `assembly-path`: Path to the .NET assembly (.dll) file to analyze
- `category-file-path` (optional): Path to a file that defines categories for organizing types

### Options

- `--keep-obsolete`: Include obsolete types and members in the diagram (by default, they are excluded)
- `--one-diagram-per-namespace`: Generate separate diagrams for each namespace instead of one large diagram

## Category File Format

The category file allows you to group types into logical categories within their namespaces. Here's the format:

```
Category1
- Type1
- Type2

Category2
* Type3
* Type4
```

- Category names are written as plain text
- Type names are listed under categories with either `-` or `*` prefix
- Empty lines between categories are optional

## Output

The tool generates a Markdown file (.md) containing the Mermaid diagram:
- Output location: `c:\temp`
- Filename: Same as the assembly name with `.md` extension
- Format: Mermaid class diagram syntax in a code block

## Examples

Command to analyze an assembly:
```bash
MermaidFromAssembly.exe "Path\To\Your\Assembly.dll"
```

With categories and options:
```bash
MermaidFromAssembly.exe "Path\To\Your\Assembly.dll" "categories.txt" --keep-obsolete --one-diagram-per-namespace
```

Example output diagram structure:
```mermaid
classDiagram
direction LR
    namespace YourNamespace {
        class Class1 {
            +string Property1
            #void Method1(int param)
        }
        class Class2 {
            <<interface>>
        }
        Class1 --|> Class2 : Implements
    }
```

## Diagram Features

The generated diagrams include:
- Classes, interfaces, and enums with their members
- Access modifiers (+: public, #: protected)
- Type relationships:
  - Inheritance (solid arrows)
  - Interface implementation (dashed arrows)
  - Associations (standard arrows)
  - Collections (with "many" multiplicity)
- Generic type parameters
- Property access indicators (read-only, write-only, read-write)
- Obsolete members (if included)
