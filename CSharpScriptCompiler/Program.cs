using CSharpScriptCompiler.Common;
using System.Reflection;

namespace CSharpScriptCompiler;

internal class Program
{
    static async Task Main()
    {
        var source = @"
using System;
using System.Threading.Tasks;

namespace MyScriptNamespace;

public class MyScriptClass
{ 
    public async Task<string> Execute(string name)
    {
        return await Task.Run(() => $""Hi there: '{name}'"");
    }
} 
";

        var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        var pluginFullPath = Path.GetFullPath(Path.Combine(currentAssemblyDirectory, $"..\\..\\..\\..\\Plugin\\bin\\Debug\\net8.0\\Plugin.dll"));

        var weakRef = await RunAndUnload(pluginFullPath, source);

        const int maxAttempts = 10;
        for (var i = 0; weakRef.IsAlive && (i < maxAttempts); i++)
        {
            await Task.Delay(100);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static async Task<WeakReference> RunAndUnload(string pluginFullPath, string source)
    {
        var (scriptCompiler, assembly, errors) = ScriptCompiler.LoadAndCompile(pluginFullPath, source, Microsoft.CodeAnalysis.OptimizationLevel.Debug);

        // Create a weak reference to the AssemblyLoadContext that will allow us to detect
        // when the unload completes.
        var alcWeakRef = new WeakReference(scriptCompiler);

        if (assembly == null || errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.WriteLine(error);
            }

            return alcWeakRef;
        }

        // Get the plugin interface by calling the PluginClass.GetInterface method via reflection.
        var scriptType = assembly.GetType("MyScriptNamespace.MyScriptClass") ?? throw new Exception("MyScriptNamespace.MyScriptClass");

        var instance = Activator.CreateInstance(scriptType, true);

        // Call script if not null an no errors
        if (instance != null)
        {
            var execute = scriptType.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public) ?? throw new Exception("Execute");

            // Now we can call methods of the plugin using the interface
            var executor = (Task<string>?)execute.Invoke(instance, ["queen"]);
            var message = (string?)await executor!;

            Console.WriteLine(message);

            scriptCompiler.Unload();
        }

        return alcWeakRef;
    }
}
