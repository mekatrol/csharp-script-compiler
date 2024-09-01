using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.CompilerServices;

namespace CSharpScriptCompiler.Common;

public class ScriptCompiler
{
    /// <summary>
    /// The full path to the currently executing .NET framework assemblies
    /// </summary>
    private readonly string _dotNetFrameworkAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

    /// <summary>
    /// The list of assembly references that are used by the script class
    /// </summary>
    private readonly IList<PortableExecutableReference> _assemblyReferences = [];

    [MethodImpl(MethodImplOptions.NoInlining)]
    public (IList<byte>, IList<string>) CompileToByteCode(string sourceCode, IList<string> additionalAssemblies, OptimizationLevel optimizationLevel)
    {
        // Add the assembly references that the caller needs
        AddAssemblies(additionalAssemblies);

        // Parse source code into a tree
        var tree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());

        // Make sure it compiles
        var compilation = CSharpCompilation.Create(Guid.NewGuid().ToString("D"))
            .WithOptions(
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: optimizationLevel)
                )
            .WithReferences(_assemblyReferences)
            .AddSyntaxTrees(tree);

        using var inMemoryCode = new MemoryStream();

        // Compile the code
        var compilationResult = compilation.Emit(inMemoryCode);

        // If there were errors then we return the error lines
        if (!compilationResult.Success)
        {
            IList<string> errorLines = [];

            foreach (var diagnostic in compilationResult.Diagnostics)
            {
                errorLines.Add(diagnostic.ToString());
            }

            return ([], errorLines);
        }

        var byteCode = inMemoryCode.ToArray();

        return (byteCode, []);
    }

    public bool AddAssembly(string assemblyDllFileName)
    {
        // Ignore empty entries
        if (string.IsNullOrWhiteSpace(assemblyDllFileName))
        {
            return false;
        }

        // Get full path, will convert relative to full paths
        var absolutePath = Path.GetFullPath(assemblyDllFileName);

        // Has it been added already, if so do nothing more (it is a duplicate)
        if (_assemblyReferences.Any(r => r.FilePath == absolutePath))
        {
            return true;
        }

        // If it does not exist as an absolute or relative file then we try as a .NET framework assembly
        if (!File.Exists(absolutePath))
        {
            // If the file does not exist then we can try and reference the current .NET framework folder.
            absolutePath = Path.Combine(_dotNetFrameworkAssemblyPath, Path.GetFileName(assemblyDllFileName));

            // Still doesn't exist then return false to indicate invalid assembly
            if (!File.Exists(absolutePath))
            {
                return false;
            }
        }

        try
        {
            // Create a reference to the DLL (not a snapshot, just a reference just in case the content changes before compiling)
            var reference = MetadataReference.CreateFromFile(absolutePath);
            _assemblyReferences.Add(reference);
            return true;
        }
        catch
        {
            // Failed to create DLL refernce then return failed
            return false;
        }
    }

    public IList<string> AddAssemblies(IList<string> assemblies)
    {
        IList<string> failedAssemblies = [];
        foreach (var assembly in assemblies)
        {
            if (!AddAssembly(assembly))
            {
                // Add to failed list
                failedAssemblies.Add(assembly);
            }
        }

        // Return any assemblies that have failed
        return failedAssemblies;
    }
}
