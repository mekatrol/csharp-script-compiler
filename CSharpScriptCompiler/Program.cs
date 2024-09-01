using CSharpScriptCompiler.Common;
using System.Reflection;

namespace CSharpScriptCompiler;

internal class Program
{
    static async Task Main()
    {
        var sourceCode = @"
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
        var executingAssemblyPath = Path.GetFullPath(currentAssemblyDirectory);

        await ScriptExecutor.RunAndUnload(
            executingAssemblyPath,
            sourceCode,
            additionalAssemblies: ["System.Private.CoreLib.dll"],
            executeScript: async (assembly) =>
            {
                // Get the plugin interface by calling the PluginClass.GetInterface method via reflection.
                var scriptType = assembly.GetType("MyScriptNamespace.MyScriptClass") ?? throw new Exception("MyScriptNamespace.MyScriptClass");

                var instance = Activator.CreateInstance(scriptType, true);

                // Call script if not null an no errors
                if (instance != null)
                {
                    var execute = scriptType.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public) ?? throw new Exception("Execute");

                    // Now we can call methods of the plugin using the interface
                    var executor = (Task<string>?)execute.Invoke(instance, ["queen"]);
                    var message = await executor!;

                    Console.WriteLine(message);
                }
            },
            unloadMaxAttempts: 10,
            unloadDelayBetweenTries: 100);
    }
}
