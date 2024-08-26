using CSharpScriptCompiler.Common;

namespace CSharpScriptCompiler;

internal class Program
{
    static async Task Main()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using CSharpScriptCompiler.Common;

namespace MyScriptNamespace;

public class MyScriptClass : IScriptClass
{ 
    public async Task<string> Execute(string name)
    {
        return await Task.Run(() => $""Hi there: '{name}'"");
    }
} 
";

        var scriptCompiler = new ScriptCompiler();

        var (_, instance, errors) = scriptCompiler.Compile(source);

        // Call script if not null an no errors
        if (instance != null && errors.Count == 0)
        {
            var hello = await instance.Execute("Mary");
            Console.WriteLine(hello);
        }
        else
        {
            foreach (var error in errors)
            {
                Console.WriteLine(error);
            }
        }
    }
}
