using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using VibeCodingDemo.Services.Tools;

namespace VibeCodingDemo.Services;

public class VibeCodingOrchestrator(Kernel kernel)
{
    private readonly FileSystemTool _fs = new();
    private readonly RoslynTool _roslyn = new();

    public async IAsyncEnumerable<AgentMessage> ExecuteAsync(
        string intent,
        string projectPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return Msg(AgentRole.Planner, "🗺️  分析项目结构...");

        var tree = _fs.GetDirectoryTree(projectPath);
        var sample = _fs.SampleCsFiles(projectPath);
        var ctx = sample.Any()
            ? string.Join("\n\n", sample.Select(f => $"// {f.RelPath}\n{Truncate(f.Content, 800)}"))
            : "// 这是一个新项目，暂无现有代码";

        yield return Msg(AgentRole.Planner, $"项目结构:\n```\n{tree}\n```");

        var planText = await GenerateLLMTextAsync(
            AgentRole.Planner,
            $"""
             你是 C# 项目规划专家，必须用中文回答。

             用户意图: {intent}

             当前项目结构:
             {tree}

             现有代码参考:
             {ctx}

             请输出详细执行计划，格式如下:
             ## 执行计划
             **目标**: [一句话]

             **步骤**:
             1. 创建 [文件路径] — [说明]
             2. ...

             **注意事项**: [命名空间、依赖关系等]
             """,
            cancellationToken);

        yield return Msg(AgentRole.Planner, planText);
        yield return Msg(AgentRole.Planner, "✅ 规划完成", MsgType.Success);

        yield return Msg(AgentRole.Coder, "💻  开始生成代码...");

        var codeText = await GenerateLLMTextAsync(
            AgentRole.Coder,
            $"""
             你是 C# 12 代码生成专家，必须用中文注释。

             用户意图: {intent}
             执行计划:
             {planText}

             现有代码参考:
             {ctx}

             要求:
             - 每个文件必须严格按以下格式输出（不得省略标记）
               <<<FILE:相对路径>>>
               // 完整代码
               <<<END>>>
             - 一次可输出多个文件
             - 只输出文件块，不输出额外解释
             """,
            cancellationToken);

        var blocks = ParseFileBlocks(codeText);
        if (blocks.Count == 0)
        {
            yield return Msg(AgentRole.Coder, "⚠️ 未识别到可写入的文件块", MsgType.Warning);
        }

        foreach (var (relPath, content) in blocks)
        {
            var fullPath = Path.GetFullPath(Path.Combine(projectPath, relPath));
            if (!IsWithinProjectRoot(projectPath, fullPath))
            {
                yield return Msg(AgentRole.Executor, $"❌ 非法路径: {relPath}", MsgType.Error);
                continue;
            }

            _fs.WriteFile(fullPath, content);
            yield return Msg(AgentRole.Coder, content, MsgType.Code, relPath);
            yield return Msg(AgentRole.Executor, $"✅ 已写入 {relPath}", MsgType.Success, relPath);
        }

        foreach (var (relPath, content) in blocks.Where(b => b.RelPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var compile = _roslyn.Compile(content, Path.GetFileNameWithoutExtension(relPath));
            if (!compile.Success)
            {
                var msg = string.Join('\n', compile.Errors.Select(e => $"{e.Id} Line {e.Line}: {e.Message}"));
                yield return Msg(AgentRole.Reviewer, $"{relPath} 编译检查失败:\n{msg}", MsgType.Warning, relPath);
            }
        }

        yield return Msg(AgentRole.Reviewer, "✅ 审核阶段完成", MsgType.Success);
        yield return Msg(AgentRole.Executor, "🎉 任务执行完成", MsgType.Success);
    }

    private async Task<string> GenerateLLMTextAsync(AgentRole role, string prompt, CancellationToken cancellationToken)
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage($"你是 {role} 代理，请仅输出任务相关内容。");
        history.AddUserMessage(prompt);

        var sb = new StringBuilder();
        await foreach (var item in chat.GetStreamingChatMessageContentsAsync(history, cancellationToken: cancellationToken))
        {
            sb.Append(item.Content);
        }

        return sb.ToString().Trim();
    }

    private static List<(string RelPath, string Content)> ParseFileBlocks(string text)
    {
        var list = new List<(string RelPath, string Content)>();
        var regex = new Regex(@"<<<FILE:(.+?)>>>\s*([\s\S]*?)<<<END>>>", RegexOptions.Multiline);

        foreach (Match m in regex.Matches(text))
        {
            var path = m.Groups[1].Value.Trim();
            var content = m.Groups[2].Value.Trim();
            if (!string.IsNullOrWhiteSpace(path))
            {
                list.Add((path, content));
            }
        }

        return list;
    }

    private static AgentMessage Msg(AgentRole role, string content, MsgType type = MsgType.Info, string? filePath = null)
        => new(role, content, type, filePath);

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "\n// ...truncated...";

    private static bool IsWithinProjectRoot(string projectPath, string fullPath)
    {
        var root = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(fullPath);
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return candidate.StartsWith(rootWithSeparator, comparison) || string.Equals(candidate, root, comparison);
    }
}
