using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace CSharpScriptCompiler.Common;

public class ScriptCompiler(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    /// <summary>
    /// The full path to the currently executing .NET framework assemblies
    /// </summary>
    private readonly string _dotNetFrameworkAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

    /// <summary>
    /// The list of assembly references that are used by the script class
    /// </summary>
    private readonly IList<PortableExecutableReference> _assemblyReferences = [];

    public static (ScriptCompiler, Assembly?, IList<string>) LoadAndCompile(string pluginPath, string sourceCode, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
    {
        // Create the unloadable HostAssemblyLoadContext
        var scriptCompiler = new ScriptCompiler(pluginPath);

        // Load the plugin assembly into the HostAssemblyLoadContext.
        // NOTE: the assemblyPath must be an absolute path.
        var (assembly, errors) = scriptCompiler.Compile(sourceCode, optimizationLevel);

        return (scriptCompiler, assembly, errors);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public (Assembly?, IList<string>) Compile(string sourceCode, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
    {
        // Add the assembly references that we need
        AddAssemblies(
            "System.Private.CoreLib.dll",
            "System.Runtime.dll",
            "System.Console.dll",
            "netstandard.dll"
        );

        var (assembly, errors) = LoadFromSource(sourceCode, optimizationLevel);

        // Return the assembly and instance
        return (assembly, errors);
    }

    public (IList<byte>, IList<string>) CompileToByteCode(string sourceCode, OptimizationLevel optimizationLevel)
    {
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

    public (Assembly?, IList<string>) LoadFromSource(string sourceCode, OptimizationLevel optimizationLevel)
    {
        var (byteCode, errors) = CompileToByteCode(sourceCode, optimizationLevel);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        using var byteStream = new MemoryStream((byte[])byteCode);

        // Load assembly from COFF memory stream
        var assembly = LoadFromStream(byteStream);

        if (assembly != null)
        {
            // Hook into unloading event
            var currentContext = GetLoadContext(assembly);
            if (currentContext != null)
            {
                currentContext.Unloading += UnloadScript;
            }
        }

        // Return loaded assembly (and empty error list)
        return (assembly, []);
    }

    private static void UnloadScript(AssemblyLoadContext obj)
    {
        Console.WriteLine("Releasing script resources");
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

    protected override Assembly? Load(AssemblyName name)
    {
        Console.WriteLine(name);

        var assemblyPath = _resolver.ResolveAssemblyToPath(name);
        if (assemblyPath != null)
        {
            Console.WriteLine($"Loading assembly {assemblyPath} into the ScriptCompiler");
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }
}
