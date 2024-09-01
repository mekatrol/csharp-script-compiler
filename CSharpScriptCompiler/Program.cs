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

        await ScriptExecutor.RunAndUnload(pluginFullPath, source, unloadMaxAttempts: 10, unloadDelayBetweenTries: 100);
    }
}
