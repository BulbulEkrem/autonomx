from google.protobuf.internal import containers as _containers
from google.protobuf.internal import enum_type_wrapper as _enum_type_wrapper
from google.protobuf import descriptor as _descriptor
from google.protobuf import message as _message
from collections.abc import Iterable as _Iterable, Mapping as _Mapping
from typing import ClassVar as _ClassVar, Optional as _Optional, Union as _Union

DESCRIPTOR: _descriptor.FileDescriptor

class ProjectStatus(int, metaclass=_enum_type_wrapper.EnumTypeWrapper):
    __slots__ = ()
    PROJECT_STATUS_UNSPECIFIED: _ClassVar[ProjectStatus]
    PROJECT_STATUS_CREATED: _ClassVar[ProjectStatus]
    PROJECT_STATUS_PLANNING: _ClassVar[ProjectStatus]
    PROJECT_STATUS_IN_PROGRESS: _ClassVar[ProjectStatus]
    PROJECT_STATUS_TESTING: _ClassVar[ProjectStatus]
    PROJECT_STATUS_REVIEWING: _ClassVar[ProjectStatus]
    PROJECT_STATUS_COMPLETED: _ClassVar[ProjectStatus]
    PROJECT_STATUS_FAILED: _ClassVar[ProjectStatus]
    PROJECT_STATUS_PAUSED: _ClassVar[ProjectStatus]

class TaskStatus(int, metaclass=_enum_type_wrapper.EnumTypeWrapper):
    __slots__ = ()
    TASK_STATUS_UNSPECIFIED: _ClassVar[TaskStatus]
    TASK_STATUS_READY: _ClassVar[TaskStatus]
    TASK_STATUS_IN_PROGRESS: _ClassVar[TaskStatus]
    TASK_STATUS_TESTING: _ClassVar[TaskStatus]
    TASK_STATUS_REVIEW: _ClassVar[TaskStatus]
    TASK_STATUS_DONE: _ClassVar[TaskStatus]
    TASK_STATUS_FAILED: _ClassVar[TaskStatus]
    TASK_STATUS_REVISION: _ClassVar[TaskStatus]

class TaskPriority(int, metaclass=_enum_type_wrapper.EnumTypeWrapper):
    __slots__ = ()
    TASK_PRIORITY_UNSPECIFIED: _ClassVar[TaskPriority]
    TASK_PRIORITY_MUST: _ClassVar[TaskPriority]
    TASK_PRIORITY_SHOULD: _ClassVar[TaskPriority]
    TASK_PRIORITY_COULD: _ClassVar[TaskPriority]

class AgentType(int, metaclass=_enum_type_wrapper.EnumTypeWrapper):
    __slots__ = ()
    AGENT_TYPE_UNSPECIFIED: _ClassVar[AgentType]
    AGENT_TYPE_PRODUCT_OWNER: _ClassVar[AgentType]
    AGENT_TYPE_PLANNER: _ClassVar[AgentType]
    AGENT_TYPE_ARCHITECT: _ClassVar[AgentType]
    AGENT_TYPE_MODEL_MANAGER: _ClassVar[AgentType]
    AGENT_TYPE_CODER: _ClassVar[AgentType]
    AGENT_TYPE_TESTER: _ClassVar[AgentType]
    AGENT_TYPE_REVIEWER: _ClassVar[AgentType]
PROJECT_STATUS_UNSPECIFIED: ProjectStatus
PROJECT_STATUS_CREATED: ProjectStatus
PROJECT_STATUS_PLANNING: ProjectStatus
PROJECT_STATUS_IN_PROGRESS: ProjectStatus
PROJECT_STATUS_TESTING: ProjectStatus
PROJECT_STATUS_REVIEWING: ProjectStatus
PROJECT_STATUS_COMPLETED: ProjectStatus
PROJECT_STATUS_FAILED: ProjectStatus
PROJECT_STATUS_PAUSED: ProjectStatus
TASK_STATUS_UNSPECIFIED: TaskStatus
TASK_STATUS_READY: TaskStatus
TASK_STATUS_IN_PROGRESS: TaskStatus
TASK_STATUS_TESTING: TaskStatus
TASK_STATUS_REVIEW: TaskStatus
TASK_STATUS_DONE: TaskStatus
TASK_STATUS_FAILED: TaskStatus
TASK_STATUS_REVISION: TaskStatus
TASK_PRIORITY_UNSPECIFIED: TaskPriority
TASK_PRIORITY_MUST: TaskPriority
TASK_PRIORITY_SHOULD: TaskPriority
TASK_PRIORITY_COULD: TaskPriority
AGENT_TYPE_UNSPECIFIED: AgentType
AGENT_TYPE_PRODUCT_OWNER: AgentType
AGENT_TYPE_PLANNER: AgentType
AGENT_TYPE_ARCHITECT: AgentType
AGENT_TYPE_MODEL_MANAGER: AgentType
AGENT_TYPE_CODER: AgentType
AGENT_TYPE_TESTER: AgentType
AGENT_TYPE_REVIEWER: AgentType

class AgentConfig(_message.Message):
    __slots__ = ("agent_id", "agent_type", "model", "provider", "parameters")
    class ParametersEntry(_message.Message):
        __slots__ = ("key", "value")
        KEY_FIELD_NUMBER: _ClassVar[int]
        VALUE_FIELD_NUMBER: _ClassVar[int]
        key: str
        value: str
        def __init__(self, key: _Optional[str] = ..., value: _Optional[str] = ...) -> None: ...
    AGENT_ID_FIELD_NUMBER: _ClassVar[int]
    AGENT_TYPE_FIELD_NUMBER: _ClassVar[int]
    MODEL_FIELD_NUMBER: _ClassVar[int]
    PROVIDER_FIELD_NUMBER: _ClassVar[int]
    PARAMETERS_FIELD_NUMBER: _ClassVar[int]
    agent_id: str
    agent_type: AgentType
    model: str
    provider: str
    parameters: _containers.ScalarMap[str, str]
    def __init__(self, agent_id: _Optional[str] = ..., agent_type: _Optional[_Union[AgentType, str]] = ..., model: _Optional[str] = ..., provider: _Optional[str] = ..., parameters: _Optional[_Mapping[str, str]] = ...) -> None: ...

class TaskInfo(_message.Message):
    __slots__ = ("task_id", "project_id", "title", "description", "status", "priority", "dependencies", "files_touched", "assigned_worker")
    TASK_ID_FIELD_NUMBER: _ClassVar[int]
    PROJECT_ID_FIELD_NUMBER: _ClassVar[int]
    TITLE_FIELD_NUMBER: _ClassVar[int]
    DESCRIPTION_FIELD_NUMBER: _ClassVar[int]
    STATUS_FIELD_NUMBER: _ClassVar[int]
    PRIORITY_FIELD_NUMBER: _ClassVar[int]
    DEPENDENCIES_FIELD_NUMBER: _ClassVar[int]
    FILES_TOUCHED_FIELD_NUMBER: _ClassVar[int]
    ASSIGNED_WORKER_FIELD_NUMBER: _ClassVar[int]
    task_id: str
    project_id: str
    title: str
    description: str
    status: TaskStatus
    priority: TaskPriority
    dependencies: _containers.RepeatedScalarFieldContainer[str]
    files_touched: _containers.RepeatedScalarFieldContainer[str]
    assigned_worker: str
    def __init__(self, task_id: _Optional[str] = ..., project_id: _Optional[str] = ..., title: _Optional[str] = ..., description: _Optional[str] = ..., status: _Optional[_Union[TaskStatus, str]] = ..., priority: _Optional[_Union[TaskPriority, str]] = ..., dependencies: _Optional[_Iterable[str]] = ..., files_touched: _Optional[_Iterable[str]] = ..., assigned_worker: _Optional[str] = ...) -> None: ...

class LlmConfig(_message.Message):
    __slots__ = ("model", "provider", "temperature", "max_tokens")
    MODEL_FIELD_NUMBER: _ClassVar[int]
    PROVIDER_FIELD_NUMBER: _ClassVar[int]
    TEMPERATURE_FIELD_NUMBER: _ClassVar[int]
    MAX_TOKENS_FIELD_NUMBER: _ClassVar[int]
    model: str
    provider: str
    temperature: float
    max_tokens: int
    def __init__(self, model: _Optional[str] = ..., provider: _Optional[str] = ..., temperature: _Optional[float] = ..., max_tokens: _Optional[int] = ...) -> None: ...
