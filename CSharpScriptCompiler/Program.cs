using CSharpScriptCompiler.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace CSharpScriptCompiler;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddTransient<ScriptRunnerService>();
            })
            .Build();

        var scriptRunner = host.Services.GetRequiredService<ScriptRunnerService>();
        var stoppingTokenSource = new CancellationTokenSource();
        await scriptRunner.Execute(stoppingTokenSource.Token);
        stoppingTokenSource.Cancel();
    }

    class ScriptRunnerService(ILogger<ScriptRunnerService> logger)
    {
        private readonly ILogger<ScriptRunnerService> _logger = logger;

        public async Task Execute(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Executing script runner");

            var sourceCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyScriptNamespace;

public class MyScriptClass
{ 
    public async Task<string> Execute(string name, CancellationToken stoppingToken)
    {
        return await Task.Run(() => $""Hi there: '{name}'"", stoppingToken);
    }
} 
";

            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var executingAssemblyPath = Path.GetFullPath(currentAssemblyDirectory);

            await ScriptExecutor.RunAndUnload(
                executingAssemblyPath,
                sourceCode,
                additionalAssemblies: ["System.Private.CoreLib.dll"],
                executeScript: async (assembly, stoppingToken) =>
                {
                    // Get the plugin interface by calling the PluginClass.GetInterface method via reflection.
                    var scriptType = assembly.GetType("MyScriptNamespace.MyScriptClass") ?? throw new Exception("MyScriptNamespace.MyScriptClass");

                    var instance = Activator.CreateInstance(scriptType, true);

                    // Call script if not null an no errors
                    if (instance != null)
                    {
                        var execute = scriptType.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public) ?? throw new Exception("Execute");

                        // Now we can call methods of the plugin using the interface
                        var executor = (Task<string>?)execute.Invoke(instance, ["queen", stoppingToken]);
                        var message = await executor!;

                        Console.WriteLine(message);
                    }
                },
                () =>
                {
                    Console.WriteLine("Script assembly unloaded");
                },
                unloadMaxAttempts: 10, unloadDelayBetweenTries: 100, stoppingToken: stoppingToken);
        }
    }

}
