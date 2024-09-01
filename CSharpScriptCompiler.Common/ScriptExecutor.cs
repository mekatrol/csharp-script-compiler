using System.Reflection;

namespace CSharpScriptCompiler.Common;

public class ScriptExecutor
{
    public static async Task RunAndUnload(
        string executingAssemblyPath,
        string sourceCode,
        IList<string> additionalAssemblies,
        Func<Assembly, CancellationToken, Task> executeScript,
        Action unload,
        int unloadMaxAttempts,
        int unloadDelayBetweenTries,
        CancellationToken stoppingToken = default)
    {
        var weakRef = await Run(executingAssemblyPath, sourceCode, additionalAssemblies, executeScript, unload, stoppingToken);

        if (weakRef != null)
        {
            await WaitForUnload(weakRef, unloadMaxAttempts, unloadDelayBetweenTries, stoppingToken);
        }
    }

    public static (ScriptAssemblyContext, Assembly?, IList<string>) Load(
        string executingAssemblyPath,
        string sourceCode,
        IList<string> additionalAssemblies,
        Action unload)
    {
        var (scriptCompiler, assembly, errors) = ScriptAssemblyContext.LoadAndCompile(
            executingAssemblyPath,
            sourceCode,
            additionalAssemblies,
            unload,
            Microsoft.CodeAnalysis.OptimizationLevel.Debug);
        return (scriptCompiler, assembly, errors);
    }

    public static async Task<WeakReference?> Run(
        string executingAssemblyPath,
        string sourceCode,
        IList<string> additionalAssemmblies,
        Func<Assembly, CancellationToken, Task> executeScript,
        Action unload,
        CancellationToken stoppingToken = default)
    {
        // Try and load compiler and assembly
        var (scriptCompiler, assembly, errors) = Load(executingAssemblyPath, sourceCode, additionalAssemmblies, unload);

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

        await executeScript(assembly, stoppingToken);

        scriptCompiler.Unload();

        return alcWeakRef;
    }

    public static async Task WaitForUnload(
        WeakReference weakRef,
        int maxAttempts,
        int delayBetweenTries,
        CancellationToken stoppingToken)
    {
        for (var i = 0; weakRef.IsAlive && (i < maxAttempts); i++)
        {
            await Task.Delay(delayBetweenTries, stoppingToken);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

    }
}
