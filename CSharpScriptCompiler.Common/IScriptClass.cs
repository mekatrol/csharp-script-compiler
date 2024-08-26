namespace CSharpScriptCompiler.Common;

public interface IScriptClass
{
    Task<string> Execute(string name);
}
