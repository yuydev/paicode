namespace VibeCodingDemo.Services;

public record AgentMessage(
    AgentRole Role,
    string Content,
    MsgType Type = MsgType.Info,
    string? FilePath = null
);

public enum AgentRole { System, Planner, Coder, Reviewer, Executor }
public enum MsgType { Info, Success, Warning, Error, Code }
