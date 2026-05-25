namespace AgentAI.Modules.AgentSteps.Dto;

public record CreateAgentStepRequest(
    int AgentRunId,
    string AgentType,
    string InputData,
    string Prompt
);


