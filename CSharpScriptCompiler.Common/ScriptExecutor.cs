using System.Reflection;

namespace CSharpScriptCompiler.Common;

public class ScriptExecutor
{
    public static async Task RunAndUnload(string pluginFullPath, string source, int unloadMaxAttempts, int unloadDelayBetweenTries)
    {
        var weakRef = await Run(pluginFullPath, source);

        if (weakRef != null)
        {
            await WaitForUnload(weakRef, unloadMaxAttempts, unloadDelayBetweenTries);
        }
    }

    public static (ScriptCompiler, Assembly?, IList<string>) Load(string pluginFullPath, string source)
    {
        var (scriptCompiler, assembly, errors) = ScriptCompiler.LoadAndCompile(pluginFullPath, source, Microsoft.CodeAnalysis.OptimizationLevel.Debug);
        return (scriptCompiler, assembly, errors);
    }

    public static async Task<WeakReference?> Run(string pluginFullPath, string source)
    {
        // Try and load compiler and assembly
        var (scriptCompiler, assembly, errors) = Load(pluginFullPath, source);

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

    public static async Task WaitForUnload(WeakReference weakRef, int maxAttempts, int delayBetweenTries)
    {
        for (var i = 0; weakRef.IsAlive && (i < maxAttempts); i++)
        {
            await Task.Delay(delayBetweenTries);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

    }
}
