using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace CSharpScriptCompiler.Common;

public class ScriptAssemblyContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    public static (ScriptAssemblyContext, Assembly?, IList<string>) LoadAndCompile(
        string pluginPath,
        string sourceCode,
        IList<string> additionalAssemblies,
        OptimizationLevel optimizationLevel = OptimizationLevel.Release)
    {
        // Create the unloadable HostAssemblyLoadContext
        var scriptCompiler = new ScriptAssemblyContext(pluginPath);

        // Load the plugin assembly into the HostAssemblyLoadContext.
        // NOTE: the assemblyPath must be an absolute path.
        var (assembly, errors) = scriptCompiler.Compile(sourceCode, additionalAssemblies, optimizationLevel);

        return (scriptCompiler, assembly, errors);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public (Assembly?, IList<string>) Compile(string sourceCode, IList<string> additionalAssemblies, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
    {
        var (assembly, errors) = LoadFromSource(sourceCode, optimizationLevel, additionalAssemblies);

        // Return the assembly and instance
        return (assembly, errors);
    }

    public (Assembly?, IList<string>) LoadFromSource(string sourceCode, OptimizationLevel optimizationLevel, IList<string> additionalAssemblies)
    {
        var scriptCompiler = new ScriptCompiler();

        var (byteCode, errors) = scriptCompiler.CompileToByteCode(sourceCode, additionalAssemblies, optimizationLevel);

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
