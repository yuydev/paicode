namespace VibeCodingDemo.Services.Tools;

public class FileSystemTool
{
    public string ReadFile(string path)
    {
        if (!File.Exists(path)) return $"[ERROR] 文件不存在: {path}";
        return File.ReadAllText(path);
    }

    public string WriteFile(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        return $"[OK] 已写入: {path}";
    }

    public string GetDirectoryTree(string root, int maxDepth = 4)
    {
        if (!Directory.Exists(root)) return $"[ERROR] 目录不存在: {root}";
        var sb = new System.Text.StringBuilder();
        BuildTree(root, sb, 0, maxDepth);
        return sb.ToString();
    }

    private static readonly HashSet<string> _skipDirs =
        [".git", "bin", "obj", ".vs", "node_modules", ".idea"];

    private void BuildTree(string path, System.Text.StringBuilder sb, int depth, int max)
    {
        if (depth > max) return;
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}📁 {Path.GetFileName(path)}/");

        foreach (var d in Directory.GetDirectories(path)
                     .Where(d => !_skipDirs.Contains(Path.GetFileName(d))))
            BuildTree(d, sb, depth + 1, max);

        foreach (var f in Directory.GetFiles(path)
                     .Where(f => f.EndsWith(".cs") || f.EndsWith(".csproj") || f.EndsWith(".json")))
            sb.AppendLine($"{indent}  📄 {Path.GetFileName(f)}");
    }

    public List<(string RelPath, string Content)> SampleCsFiles(string root, int max = 3)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin"))
            .Take(max)
            .Select(f => (Path.GetRelativePath(root, f), File.ReadAllText(f)))
            .ToList();
    }
}
