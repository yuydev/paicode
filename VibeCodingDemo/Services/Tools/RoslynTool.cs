using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VibeCodingDemo.Services.Tools;

public class RoslynTool
{
    public CompileResult Compile(string code, string name = "Generated")
    {
        var tree = CSharpSyntaxTree.ParseText(code);

        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToArray();

        var comp = CSharpCompilation.Create(
            name, [tree], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = comp.Emit(ms);

        var errors = emit.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => new CompileError(
                d.Id,
                d.Location.GetLineSpan().StartLinePosition.Line + 1,
                d.GetMessage()))
            .ToList();

        return new CompileResult(emit.Success, errors);
    }
}

public record CompileResult(bool Success, List<CompileError> Errors);
public record CompileError(string Id, int Line, string Message);
