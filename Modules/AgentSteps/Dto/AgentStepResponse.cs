namespace AgentAI.Modules.AgentSteps.Dto;
public record AgentStepResponse(
    int Id,
    int AgentRunId,
    string AgentType,
    string InputData,
    string OutputData,
    string Status,
    string Prompt,
    DateTime CreatedAt
);