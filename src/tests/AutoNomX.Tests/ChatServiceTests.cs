using AutoNomX.Application.Services;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AutoNomX.Tests;

public class ChatServiceTests
{
    private readonly IChatSessionRepository _sessionRepo = Substitute.For<IChatSessionRepository>();
    private readonly IChatMessageRepository _messageRepo = Substitute.For<IChatMessageRepository>();
    private readonly IChangeLogRepository _changeLogRepo = Substitute.For<IChangeLogRepository>();
    private readonly IAgentGateway _agentGateway = Substitute.For<IAgentGateway>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TaskBoardService _taskBoard;
    private readonly ILogger<ChatService> _logger = Substitute.For<ILogger<ChatService>>();
    private readonly ChatService _sut;
    private readonly Guid _projectId = Guid.NewGuid();

    public ChatServiceTests()
    {
        var workerRepo = Substitute.For<ICoderWorkerRepository>();
        var fileRepo = Substitute.For<IProjectFileRepository>();
        var mediator = Substitute.For<MediatR.IMediator>();
        var boardLogger = Substitute.For<ILogger<TaskBoardService>>();

        _taskBoard = new TaskBoardService(_taskRepo, workerRepo, fileRepo, _unitOfWork, mediator, boardLogger);
        _sut = new ChatService(_sessionRepo, _messageRepo, _changeLogRepo,
            _agentGateway, _taskRepo, _unitOfWork, _taskBoard, _logger);
    }

    [Fact]
    public async Task StartChatAsync_CreatesNewSession()
    {
        _sessionRepo.GetActiveByProjectIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns((ChatSession?)null);
        _taskRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<TaskItem>
            {
                new() { ProjectId = _projectId, Title = "Task 1" },
                new() { ProjectId = _projectId, Title = "Task 2" },
            });

        var session = await _sut.StartChatAsync(_projectId);

        Assert.Equal(_projectId, session.ProjectId);
        Assert.True(session.IsActive);
        Assert.Equal(2, session.OriginalStoryCount);
        await _sessionRepo.Received(1).AddAsync(Arg.Any<ChatSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartChatAsync_ReusesExistingActiveSession()
    {
        var existing = new ChatSession
        {
            ProjectId = _projectId,
            IsActive = true,
            OriginalStoryCount = 3,
            CurrentStoryCount = 3,
        };
        _sessionRepo.GetActiveByProjectIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var session = await _sut.StartChatAsync(_projectId);

        Assert.Equal(existing.Id, session.Id);
        await _sessionRepo.DidNotReceive().AddAsync(Arg.Any<ChatSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_CallsPOAgent()
    {
        var session = CreateSession();
        _sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetBySessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessage>());
        _agentGateway.RunAgentAsync(AgentType.ProductOwner, Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionResult(
                ExecutionId: Guid.NewGuid().ToString(),
                Success: true,
                ResultJson: "{\"message\": \"Understood. I will analyze your request.\"}"));

        var response = await _sut.SendMessageAsync(session.Id, "Add a login page");

        Assert.Contains("Understood", response.Message);
        Assert.Null(response.ChangeType);
        await _messageRepo.Received(2).AddAsync(Arg.Any<ChatMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_DetectsChangeProposal()
    {
        var session = CreateSession();
        _sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetBySessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessage>());
        _agentGateway.RunAgentAsync(AgentType.ProductOwner, Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionResult(
                ExecutionId: Guid.NewGuid().ToString(),
                Success: true,
                ResultJson: "{\"message\": \"Adding new story.\", \"change_type\": \"AddStory\", \"change_details\": \"{}\"}"));

        var response = await _sut.SendMessageAsync(session.Id, "Add OAuth support");

        Assert.Equal("AddStory", response.ChangeType);
        Assert.NotNull(response.ChangeDetails);
    }

    [Fact]
    public async Task SendMessageAsync_ScopeCreepWarning()
    {
        var session = CreateSession();
        session.OriginalStoryCount = 3;
        session.CurrentStoryCount = 4; // Already 33% over

        _sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetBySessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessage>());
        _agentGateway.RunAgentAsync(AgentType.ProductOwner, Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionResult(
                ExecutionId: Guid.NewGuid().ToString(),
                Success: true,
                ResultJson: "{\"message\": \"Adding one more.\", \"change_type\": \"AddStory\"}"));

        var response = await _sut.SendMessageAsync(session.Id, "Add yet another feature");

        Assert.Contains("Scope Creep Warning", response.Message);
    }

    [Fact]
    public async Task EndChatAsync_DeactivatesSession()
    {
        var session = CreateSession();
        _sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        await _sut.EndChatAsync(session.Id);

        Assert.False(session.IsActive);
        await _sessionRepo.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetChatHistoryAsync_ReturnsSortedMessages()
    {
        var session = CreateSession();
        _sessionRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<ChatSession> { session });

        var msg1 = new ChatMessage { SessionId = session.Id, Role = "user", Content = "Hi", CreatedAt = DateTime.UtcNow.AddMinutes(-2) };
        var msg2 = new ChatMessage { SessionId = session.Id, Role = "assistant", Content = "Hello!", CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        _messageRepo.GetBySessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessage> { msg1, msg2 });

        var history = await _sut.GetChatHistoryAsync(_projectId);

        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("assistant", history[1].Role);
    }

    [Fact]
    public async Task RejectChangeAsync_MarksNotApproved()
    {
        var session = CreateSession();
        var changeMsg = new ChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = "Proposing change",
            ChangeType = "AddStory",
        };

        _messageRepo.GetBySessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessage> { changeMsg });

        await _sut.RejectChangeAsync(session.Id, changeMsg.Id);

        Assert.False(changeMsg.IsApproved);
    }

    private ChatSession CreateSession() => new()
    {
        ProjectId = _projectId,
        IsActive = true,
        OriginalStoryCount = 5,
        CurrentStoryCount = 5,
    };
}
