using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Stateless;

namespace AutoNomX.Application.StateMachine;

/// <summary>
/// State machine for pipeline execution lifecycle.
/// Uses the Stateless library with state persistence to PostgreSQL.
/// Supports re-hydration on application restart.
/// </summary>
public class PipelineStateMachine
{
    private readonly StateMachine<PipelineState, PipelineTrigger> _machine;
    private readonly PipelineRun _pipelineRun;
    private readonly IPipelineRunRepository _pipelineRunRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PipelineStateMachine> _logger;

    private PipelineState _state;
    private PipelineState _stateBeforePause;
    private int _testFailureCount;
    private int _revisionCount;

    public const int MaxTestRetries = 3;
    public const int MaxRevisions = 3;

    public PipelineState CurrentState => _state;
    public Guid PipelineRunId => _pipelineRun.Id;
    public Guid ProjectId => _pipelineRun.ProjectId;
    public int TestFailureCount => _testFailureCount;
    public int RevisionCount => _revisionCount;

    public PipelineStateMachine(
        PipelineRun pipelineRun,
        IPipelineRunRepository pipelineRunRepo,
        IUnitOfWork unitOfWork,
        ILogger<PipelineStateMachine> logger)
    {
        _pipelineRun = pipelineRun;
        _pipelineRunRepo = pipelineRunRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;

        // Re-hydrate state from DB
        _state = ParseState(pipelineRun.CurrentStep);
        _stateBeforePause = PipelineState.Idle;
        _testFailureCount = 0;
        _revisionCount = 0;

        _machine = new StateMachine<PipelineState, PipelineTrigger>(
            () => _state,
            s => _state = s);

        ConfigureTransitions();
    }

    private void ConfigureTransitions()
    {
        // Idle → Planning
        _machine.Configure(PipelineState.Idle)
            .Permit(PipelineTrigger.Start, PipelineState.Planning)
            .Permit(PipelineTrigger.Error, PipelineState.Failed);

        // Planning → Architecting
        _machine.Configure(PipelineState.Planning)
            .Permit(PipelineTrigger.PlanReady, PipelineState.Architecting)
            .Permit(PipelineTrigger.Pause, PipelineState.Paused)
            .Permit(PipelineTrigger.Error, PipelineState.Failed)
            .OnEntry(() => OnStateEntered(PipelineState.Planning));

        // Architecting → Coding
        _machine.Configure(PipelineState.Architecting)
            .Permit(PipelineTrigger.ArchitectureReady, PipelineState.Coding)
            .Permit(PipelineTrigger.Pause, PipelineState.Paused)
            .Permit(PipelineTrigger.Error, PipelineState.Failed)
            .OnEntry(() => OnStateEntered(PipelineState.Architecting));

        // Coding → Testing
        _machine.Configure(PipelineState.Coding)
            .Permit(PipelineTrigger.CodeReady, PipelineState.Testing)
            .Permit(PipelineTrigger.Pause, PipelineState.Paused)
            .Permit(PipelineTrigger.Error, PipelineState.Failed)
            .OnEntry(() => OnStateEntered(PipelineState.Coding));

        // Testing → Reviewing | Coding (retry)
        _machine.Configure(PipelineState.Testing)
            .Permit(PipelineTrigger.TestPassed, PipelineState.Reviewing)
            .PermitIf(PipelineTrigger.TestFailed, PipelineState.Coding, () => _testFailureCount < MaxTestRetries)
            .PermitIf(PipelineTrigger.TestFailed, PipelineState.Failed, () => _testFailureCount >= MaxTestRetries)
            .Permit(PipelineTrigger.Pause, PipelineState.Paused)
            .Permit(PipelineTrigger.Error, PipelineState.Failed)
            .OnEntry(() => OnStateEntered(PipelineState.Testing));

        // Reviewing → Architecting (approved, more tasks) | Coding (rejected) | Completed
        _machine.Configure(PipelineState.Reviewing)
            .Permit(PipelineTrigger.ReviewApproved, PipelineState.Architecting)
            .PermitIf(PipelineTrigger.ReviewRejected, PipelineState.Coding, () => _revisionCount < MaxRevisions)
            .PermitIf(PipelineTrigger.ReviewRejected, PipelineState.Failed, () => _revisionCount >= MaxRevisions)
            .Permit(PipelineTrigger.AllTasksCompleted, PipelineState.Completed)
            .Permit(PipelineTrigger.Pause, PipelineState.Paused)
            .Permit(PipelineTrigger.Error, PipelineState.Failed)
            .OnEntry(() => OnStateEntered(PipelineState.Reviewing));

        // Completed (terminal)
        _machine.Configure(PipelineState.Completed)
            .OnEntry(() => OnStateEntered(PipelineState.Completed));

        // Failed (terminal)
        _machine.Configure(PipelineState.Failed)
            .OnEntry(() => OnStateEntered(PipelineState.Failed));

        // Paused → Resume to previous state
        _machine.Configure(PipelineState.Paused)
            .PermitDynamic(PipelineTrigger.Resume, () => _stateBeforePause)
            .Permit(PipelineTrigger.Error, PipelineState.Failed)
            .OnEntry(() => OnStateEntered(PipelineState.Paused));

        // Global unhandled trigger handler
        _machine.OnUnhandledTrigger((state, trigger) =>
        {
            _logger.LogWarning(
                "Unhandled trigger {Trigger} in state {State} for pipeline {PipelineRunId}",
                trigger, state, _pipelineRun.Id);
        });
    }

    /// <summary>Fire a trigger to transition the state machine.</summary>
    public async Task FireAsync(PipelineTrigger trigger)
    {
        var previousState = _state;

        _logger.LogInformation(
            "Pipeline {Id}: firing {Trigger} in state {State}",
            _pipelineRun.Id, trigger, _state);

        // Track retries
        if (trigger == PipelineTrigger.TestFailed)
            _testFailureCount++;
        if (trigger == PipelineTrigger.ReviewRejected)
            _revisionCount++;

        // Save state before pause for resume
        if (trigger == PipelineTrigger.Pause)
            _stateBeforePause = _state;

        _machine.Fire(trigger);

        _logger.LogInformation(
            "Pipeline {Id}: {PreviousState} → {NewState} (trigger={Trigger})",
            _pipelineRun.Id, previousState, _state, trigger);

        await PersistStateAsync();
    }

    /// <summary>Check if a trigger can be fired in the current state.</summary>
    public bool CanFire(PipelineTrigger trigger) => _machine.CanFire(trigger);

    /// <summary>Get all triggers available in the current state.</summary>
    public IEnumerable<PipelineTrigger> GetPermittedTriggers() => _machine.PermittedTriggers;

    private void OnStateEntered(PipelineState state)
    {
        _pipelineRun.CurrentStep = state.ToString();
        _pipelineRun.CurrentIteration++;

        if (state == PipelineState.Completed)
        {
            _pipelineRun.Status = PipelineStatus.Completed;
            _pipelineRun.CompletedAt = DateTime.UtcNow;
        }
        else if (state == PipelineState.Failed)
        {
            _pipelineRun.Status = PipelineStatus.Failed;
            _pipelineRun.CompletedAt = DateTime.UtcNow;
        }
        else if (state == PipelineState.Paused)
        {
            _pipelineRun.Status = PipelineStatus.Paused;
        }
        else
        {
            _pipelineRun.Status = PipelineStatus.Running;
        }
    }

    private async Task PersistStateAsync()
    {
        _pipelineRun.UpdatedAt = DateTime.UtcNow;
        await _pipelineRunRepo.UpdateAsync(_pipelineRun);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>Create a new state machine for a fresh pipeline run.</summary>
    public static PipelineStateMachine Create(
        Guid projectId,
        IPipelineRunRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<PipelineStateMachine> logger)
    {
        var run = new PipelineRun
        {
            ProjectId = projectId,
            Status = PipelineStatus.Pending,
            CurrentStep = PipelineState.Idle.ToString(),
            CurrentIteration = 0,
            StartedAt = DateTime.UtcNow,
        };

        return new PipelineStateMachine(run, repo, unitOfWork, logger);
    }

    /// <summary>Re-hydrate a state machine from an existing pipeline run (restart recovery).</summary>
    public static PipelineStateMachine Rehydrate(
        PipelineRun existingRun,
        IPipelineRunRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<PipelineStateMachine> logger)
    {
        return new PipelineStateMachine(existingRun, repo, unitOfWork, logger);
    }

    /// <summary>Get the underlying PipelineRun entity.</summary>
    public PipelineRun GetPipelineRun() => _pipelineRun;

    private static PipelineState ParseState(string step)
    {
        return Enum.TryParse<PipelineState>(step, ignoreCase: true, out var state)
            ? state
            : PipelineState.Idle;
    }
}
