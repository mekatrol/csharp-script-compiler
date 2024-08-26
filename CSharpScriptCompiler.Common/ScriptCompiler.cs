using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace CSharpScriptCompiler.Common;

public class ScriptCompiler
{
    /// <summary>
    /// The type that scripts must implement to be executed
    /// </summary>
    private static readonly Type ScriptClassType = typeof(IScriptClass);

    /// <summary>
    /// The full path to the currently executing .NET framework assemblies
    /// </summary>
    private readonly string _dotNetFrameworkAssemblyPath;

    /// <summary>
    /// The list of assembly references that are used by the script class
    /// </summary>
    private readonly IList<PortableExecutableReference> _assemblyReferences = [];

    public ScriptCompiler()
    {
        // Getting the assembly location of the 'object' type gets the executing framework assembly location
        // because the 'object' type is in the .NET framework
        _dotNetFrameworkAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
    }

    public (Assembly?, IScriptClass?, IList<string>) Compile(string sourceCode, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
    {
        // Add the assembly references that we need
        AddAssemblies(
            "System.Private.CoreLib.dll",
            "System.Runtime.dll",
            "System.Console.dll",
            "netstandard.dll",

            // We need the IScriptClass DLL
            "CSharpScriptCompiler.Common.dll"
        );

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

        Assembly? assembly = null;

        using (var inMemoryCode = new MemoryStream())
        {
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

                return (null, null, errorLines);
            }

            // Load raw compiled code (bytecode) into an assembly
            var bytecode = inMemoryCode.ToArray();
            assembly = Assembly.Load(bytecode);
        }

        // If we failed to load in memory code then there was some error, shouldn't normally
        // occur unles there is some sort of transient error (like low memory on the machine)
        if (assembly == null)
        {
            return (null, null, ["Failed to compile and load compiled assembly"]);
        }

        // Get all of the types that implment IScriptClass
        var types = assembly.GetTypes().Where(ScriptClassType.IsAssignableFrom).ToList();

        // There must be exactly one type (else it is an error)
        if (types == null || types.Count != 1)
        {
            return (null, null, [$"There must be exactly one class that implements the interface '{nameof(IScriptClass)}'"]);
        }

        // Instantiate the class
        var instance = (IScriptClass?)assembly.CreateInstance(types[0].FullName!);

        // Return the assembly and instance
        return (assembly, instance, []);
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

    public IList<string> AddAssemblies(params string[] assemblies)
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
