using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace CSharpScriptCompiler.Common;

public class ScriptAssemblyContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly Action _unload;

    // Make constructor private, must be instantiated through static method
    private ScriptAssemblyContext(string pluginPath, Action unload) : base(isCollectible: true)
    {
        _resolver = new(pluginPath);
        _unload = unload;
    }

    public static (ScriptAssemblyContext, Assembly?, IList<string>) LoadAndCompile(
        string pluginPath,
        string sourceCode,
        IList<string> additionalAssemblies,
        Action unload,
        OptimizationLevel optimizationLevel = OptimizationLevel.Release)
    {
        var context = new ScriptAssemblyContext(pluginPath, unload);

        var (assembly, errors) = context.LoadFromSource(sourceCode, additionalAssemblies, optimizationLevel);

        return (context, assembly, errors);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    public (Assembly?, IList<string>) LoadFromSource(
        string sourceCode,
        IList<string> additionalAssemblies,
        OptimizationLevel optimizationLevel)
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

    private void UnloadScript(AssemblyLoadContext obj)
    {
        _unload();
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
