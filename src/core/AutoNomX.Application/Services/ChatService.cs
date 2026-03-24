using System.Text.Json;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Application.Services;

/// <summary>
/// Product Owner chat service.
/// Handles interactive sessions between users and PO agent,
/// including change proposals, approvals, and scope creep detection.
/// </summary>
public class ChatService(
    IChatSessionRepository sessionRepo,
    IChatMessageRepository messageRepo,
    IChangeLogRepository changeLogRepo,
    IAgentGateway agentGateway,
    ITaskRepository taskRepo,
    IUnitOfWork unitOfWork,
    TaskBoardService taskBoard,
    ILogger<ChatService> logger)
{
    private const double ScopeCreepThreshold = 0.30; // 30%

    /// <summary>Start a new chat session with PO for a project.</summary>
    public async Task<ChatSession> StartChatAsync(Guid projectId, CancellationToken ct = default)
    {
        // Check for existing active session
        var existing = await sessionRepo.GetActiveByProjectIdAsync(projectId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Reusing active chat session {Id} for project {ProjectId}",
                existing.Id, projectId);
            return existing;
        }

        // Count current stories for scope tracking
        var tasks = await taskRepo.GetByProjectIdAsync(projectId, ct);
        var storyCount = tasks.Count;

        var session = new ChatSession
        {
            ProjectId = projectId,
            IsActive = true,
            OriginalStoryCount = storyCount,
            CurrentStoryCount = storyCount,
        };

        await sessionRepo.AddAsync(session, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Chat session started: {Id} for project {ProjectId}", session.Id, projectId);
        return session;
    }

    /// <summary>Send a message and get PO response.</summary>
    public async Task<ChatResponse> SendMessageAsync(
        Guid sessionId,
        string userMessage,
        CancellationToken ct = default)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Chat session {sessionId} not found");

        // Save user message
        var userMsg = new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = userMessage,
        };
        await messageRepo.AddAsync(userMsg, ct);

        // Build context with history
        var history = await messageRepo.GetBySessionIdAsync(sessionId, ct);
        var contextJson = BuildContextJson(session, history, userMessage);

        // Call PO agent
        var result = await agentGateway.RunAgentAsync(
            AgentType.ProductOwner,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: session.ProjectId.ToString(),
                ContextJson: contextJson),
            ct);

        // Parse PO response
        var poResponse = ParsePoResponse(result);

        // Check scope creep
        if (poResponse.ChangeType is not null)
            poResponse = CheckScopeCreep(session, poResponse);

        // Save assistant message
        var assistantMsg = new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = poResponse.Message,
            ChangeType = poResponse.ChangeType,
        };
        await messageRepo.AddAsync(assistantMsg, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return poResponse;
    }

    /// <summary>Approve a proposed change.</summary>
    public async Task<ApplyChangeResult> ApproveChangeAsync(
        Guid sessionId,
        Guid messageId,
        CancellationToken ct = default)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Chat session {sessionId} not found");

        var messages = await messageRepo.GetBySessionIdAsync(sessionId, ct);
        var changeMsg = messages.FirstOrDefault(m => m.Id == messageId && m.ChangeType is not null);
        if (changeMsg is null)
            return new ApplyChangeResult(false, "No change proposal found for this message");

        changeMsg.IsApproved = true;

        // Apply the change
        var result = await ApplyChangeAsync(session.ProjectId, changeMsg, ct);

        // Log the change
        var changeLog = new ChangeLog
        {
            ProjectId = session.ProjectId,
            ChangeType = Enum.TryParse<ChangeType>(changeMsg.ChangeType, true, out var parsed)
                ? parsed : ChangeType.ChangeScope,
            UserMessage = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "",
            AgentResponse = changeMsg.Content,
            IsApproved = true,
        };
        await changeLogRepo.AddAsync(changeLog, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return result;
    }

    /// <summary>Reject a proposed change.</summary>
    public async Task RejectChangeAsync(
        Guid sessionId,
        Guid messageId,
        CancellationToken ct = default)
    {
        var messages = await messageRepo.GetBySessionIdAsync(sessionId, ct);
        var changeMsg = messages.FirstOrDefault(m => m.Id == messageId);
        if (changeMsg is not null)
            changeMsg.IsApproved = false;

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Get full chat history for a project.</summary>
    public async Task<IReadOnlyList<ChatMessage>> GetChatHistoryAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var sessions = await sessionRepo.GetByProjectIdAsync(projectId, ct);
        var allMessages = new List<ChatMessage>();

        foreach (var session in sessions)
        {
            var msgs = await messageRepo.GetBySessionIdAsync(session.Id, ct);
            allMessages.AddRange(msgs);
        }

        return allMessages.OrderBy(m => m.CreatedAt).ToList();
    }

    /// <summary>End an active chat session.</summary>
    public async Task EndChatAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId, ct);
        if (session is null) return;

        session.IsActive = false;
        await sessionRepo.UpdateAsync(session, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ── Private helpers ─────────────────────────────────────────

    private static string BuildContextJson(
        ChatSession session,
        IReadOnlyList<ChatMessage> history,
        string userMessage)
    {
        var context = new
        {
            mode = "chat",
            session_id = session.Id.ToString(),
            project_id = session.ProjectId.ToString(),
            original_story_count = session.OriginalStoryCount,
            current_story_count = session.CurrentStoryCount,
            history = history.Select(m => new { m.Role, m.Content }).ToList(),
            user_message = userMessage,
        };

        return JsonSerializer.Serialize(context);
    }

    private static ChatResponse ParsePoResponse(AgentExecutionResult result)
    {
        if (!result.Success)
            return new ChatResponse(result.Error ?? "PO agent failed", null, null);

        try
        {
            using var doc = JsonDocument.Parse(result.ResultJson);
            var root = doc.RootElement;

            var message = root.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? result.ResultJson
                : result.ResultJson;

            var changeType = root.TryGetProperty("change_type", out var ct)
                ? ct.GetString()
                : null;

            var changeDetails = root.TryGetProperty("change_details", out var cd)
                ? cd.GetRawText()
                : null;

            return new ChatResponse(message, changeType, changeDetails);
        }
        catch
        {
            return new ChatResponse(result.ResultJson, null, null);
        }
    }

    private ChatResponse CheckScopeCreep(ChatSession session, ChatResponse response)
    {
        if (session.OriginalStoryCount == 0) return response;

        var changeRatio = (double)(session.CurrentStoryCount - session.OriginalStoryCount)
            / session.OriginalStoryCount;

        if (changeRatio >= ScopeCreepThreshold)
        {
            var warning = "\n\n**Scope Creep Warning**: The cumulative changes have reached " +
                $"{changeRatio:P0} of the original scope ({session.OriginalStoryCount} stories). " +
                "Consider splitting this into phases to maintain focus and delivery quality.";

            return response with { Message = response.Message + warning };
        }

        return response;
    }

    private async Task<ApplyChangeResult> ApplyChangeAsync(
        Guid projectId,
        ChatMessage changeMsg,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ChangeType>(changeMsg.ChangeType, true, out var changeType))
            return new ApplyChangeResult(false, $"Unknown change type: {changeMsg.ChangeType}");

        try
        {
            switch (changeType)
            {
                case ChangeType.AddStory:
                    return await ApplyAddStoryAsync(projectId, changeMsg.Content, ct);
                case ChangeType.ModifyStory:
                    return await ApplyModifyStoryAsync(projectId, changeMsg.Content, ct);
                case ChangeType.RemoveStory:
                    return await ApplyRemoveStoryAsync(projectId, changeMsg.Content, ct);
                case ChangeType.ChangePriority:
                    return await ApplyChangePriorityAsync(projectId, changeMsg.Content, ct);
                case ChangeType.PauseProject:
                    return new ApplyChangeResult(true, "Project pause requested");
                default:
                    return new ApplyChangeResult(false, $"Unhandled change type: {changeType}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply change: {Type}", changeType);
            return new ApplyChangeResult(false, ex.Message);
        }
    }

    private async Task<ApplyChangeResult> ApplyAddStoryAsync(
        Guid projectId, string content, CancellationToken ct)
    {
        // Send to planner for task creation
        var result = await agentGateway.RunAgentAsync(
            AgentType.Planner,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: projectId.ToString(),
                ContextJson: JsonSerializer.Serialize(new { action = "add_story", content })),
            ct);

        if (!result.Success)
            return new ApplyChangeResult(false, result.Error ?? "Planner failed");

        // Parse new tasks and add to board
        var newTasks = ParseTasksFromPlannerResult(projectId, result.ResultJson);
        if (newTasks.Count > 0)
        {
            await taskBoard.InitializeBoardAsync(projectId, newTasks, ct);

            // Update session story count
            var sessions = await sessionRepo.GetByProjectIdAsync(projectId, ct);
            var active = sessions.FirstOrDefault(s => s.IsActive);
            if (active is not null)
            {
                active.CurrentStoryCount += newTasks.Count;
                await sessionRepo.UpdateAsync(active, ct);
            }
        }

        return new ApplyChangeResult(true, $"{newTasks.Count} new task(s) added to the board");
    }

    private async Task<ApplyChangeResult> ApplyModifyStoryAsync(
        Guid projectId, string content, CancellationToken ct)
    {
        // Parse which task to modify from PO response
        try
        {
            using var doc = JsonDocument.Parse(content);
            // PO response should contain task_id and updates
            if (doc.RootElement.TryGetProperty("task_id", out var taskIdProp))
            {
                var taskId = Guid.Parse(taskIdProp.GetString()!);
                var task = await taskRepo.GetByIdAsync(taskId, ct);
                if (task is null)
                    return new ApplyChangeResult(false, "Task not found");

                if (doc.RootElement.TryGetProperty("new_description", out var desc))
                    task.Description = desc.GetString();

                await taskRepo.UpdateAsync(task, ct);
                return new ApplyChangeResult(true, $"Task '{task.Title}' updated");
            }
        }
        catch { /* not parseable as json, treat as general modification */ }

        return new ApplyChangeResult(true, "Story modification noted");
    }

    private async Task<ApplyChangeResult> ApplyRemoveStoryAsync(
        Guid projectId, string content, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("task_id", out var taskIdProp))
            {
                var taskId = Guid.Parse(taskIdProp.GetString()!);
                var task = await taskRepo.GetByIdAsync(taskId, ct);
                if (task is null)
                    return new ApplyChangeResult(false, "Task not found");

                if (task.Status == TaskItemStatus.InProgress)
                    return new ApplyChangeResult(false,
                        $"Task '{task.Title}' is in progress — waiting for completion before removal");

                task.Status = TaskItemStatus.Failed; // Mark as cancelled
                await taskRepo.UpdateAsync(task, ct);

                // Update session story count
                var sessions = await sessionRepo.GetByProjectIdAsync(projectId, ct);
                var active = sessions.FirstOrDefault(s => s.IsActive);
                if (active is not null)
                {
                    active.CurrentStoryCount--;
                    await sessionRepo.UpdateAsync(active, ct);
                }

                return new ApplyChangeResult(true, $"Task '{task.Title}' removed");
            }
        }
        catch { /* not parseable */ }

        return new ApplyChangeResult(false, "Could not identify task to remove");
    }

    private async Task<ApplyChangeResult> ApplyChangePriorityAsync(
        Guid projectId, string content, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("task_id", out var taskIdProp) &&
                doc.RootElement.TryGetProperty("new_priority", out var priorityProp))
            {
                var taskId = Guid.Parse(taskIdProp.GetString()!);
                var task = await taskRepo.GetByIdAsync(taskId, ct);
                if (task is null)
                    return new ApplyChangeResult(false, "Task not found");

                if (Enum.TryParse<TaskItemPriority>(priorityProp.GetString(), true, out var newPriority))
                {
                    task.Priority = newPriority;
                    await taskRepo.UpdateAsync(task, ct);
                    return new ApplyChangeResult(true, $"Task '{task.Title}' priority → {newPriority}");
                }
            }
        }
        catch { /* not parseable */ }

        return new ApplyChangeResult(false, "Could not parse priority change");
    }

    private static List<TaskItem> ParseTasksFromPlannerResult(Guid projectId, string resultJson)
    {
        var tasks = new List<TaskItem>();
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            if (doc.RootElement.TryGetProperty("tasks", out var tasksArray))
            {
                foreach (var taskEl in tasksArray.EnumerateArray())
                {
                    var title = taskEl.TryGetProperty("title", out var t) ? t.GetString() ?? "Untitled" : "Untitled";
                    var desc = taskEl.TryGetProperty("description", out var d) ? d.GetString() : null;
                    var priorityStr = taskEl.TryGetProperty("priority", out var p) ? p.GetString() : "Should";
                    Enum.TryParse<TaskItemPriority>(priorityStr, true, out var priority);

                    tasks.Add(new TaskItem
                    {
                        ProjectId = projectId,
                        Title = title,
                        Description = desc,
                        Priority = priority,
                    });
                }
            }
        }
        catch { /* invalid json */ }

        return tasks;
    }
}

public record ChatResponse(string Message, string? ChangeType, string? ChangeDetails);

public record ApplyChangeResult(bool Success, string Message);
