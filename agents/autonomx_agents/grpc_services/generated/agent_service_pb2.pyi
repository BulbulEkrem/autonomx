import common_pb2 as _common_pb2
from google.protobuf.internal import containers as _containers
from google.protobuf.internal import enum_type_wrapper as _enum_type_wrapper
from google.protobuf import descriptor as _descriptor
from google.protobuf import message as _message
from collections.abc import Mapping as _Mapping
from typing import ClassVar as _ClassVar, Optional as _Optional, Union as _Union

DESCRIPTOR: _descriptor.FileDescriptor

class StreamEventType(int, metaclass=_enum_type_wrapper.EnumTypeWrapper):
    __slots__ = ()
    STREAM_EVENT_TYPE_UNSPECIFIED: _ClassVar[StreamEventType]
    STREAM_EVENT_TYPE_LOG: _ClassVar[StreamEventType]
    STREAM_EVENT_TYPE_PROGRESS: _ClassVar[StreamEventType]
    STREAM_EVENT_TYPE_OUTPUT: _ClassVar[StreamEventType]
    STREAM_EVENT_TYPE_ERROR: _ClassVar[StreamEventType]
    STREAM_EVENT_TYPE_COMPLETED: _ClassVar[StreamEventType]
STREAM_EVENT_TYPE_UNSPECIFIED: StreamEventType
STREAM_EVENT_TYPE_LOG: StreamEventType
STREAM_EVENT_TYPE_PROGRESS: StreamEventType
STREAM_EVENT_TYPE_OUTPUT: StreamEventType
STREAM_EVENT_TYPE_ERROR: StreamEventType
STREAM_EVENT_TYPE_COMPLETED: StreamEventType

class ExecuteAgentRequest(_message.Message):
    __slots__ = ("execution_id", "project_id", "agent_type", "config", "task", "context", "metadata")
    class MetadataEntry(_message.Message):
        __slots__ = ("key", "value")
        KEY_FIELD_NUMBER: _ClassVar[int]
        VALUE_FIELD_NUMBER: _ClassVar[int]
        key: str
        value: str
        def __init__(self, key: _Optional[str] = ..., value: _Optional[str] = ...) -> None: ...
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    PROJECT_ID_FIELD_NUMBER: _ClassVar[int]
    AGENT_TYPE_FIELD_NUMBER: _ClassVar[int]
    CONFIG_FIELD_NUMBER: _ClassVar[int]
    TASK_FIELD_NUMBER: _ClassVar[int]
    CONTEXT_FIELD_NUMBER: _ClassVar[int]
    METADATA_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    project_id: str
    agent_type: _common_pb2.AgentType
    config: _common_pb2.AgentConfig
    task: _common_pb2.TaskInfo
    context: str
    metadata: _containers.ScalarMap[str, str]
    def __init__(self, execution_id: _Optional[str] = ..., project_id: _Optional[str] = ..., agent_type: _Optional[_Union[_common_pb2.AgentType, str]] = ..., config: _Optional[_Union[_common_pb2.AgentConfig, _Mapping]] = ..., task: _Optional[_Union[_common_pb2.TaskInfo, _Mapping]] = ..., context: _Optional[str] = ..., metadata: _Optional[_Mapping[str, str]] = ...) -> None: ...

class ExecuteAgentResponse(_message.Message):
    __slots__ = ("execution_id", "success", "result", "error", "metrics")
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    SUCCESS_FIELD_NUMBER: _ClassVar[int]
    RESULT_FIELD_NUMBER: _ClassVar[int]
    ERROR_FIELD_NUMBER: _ClassVar[int]
    METRICS_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    success: bool
    result: str
    error: str
    metrics: AgentMetrics
    def __init__(self, execution_id: _Optional[str] = ..., success: bool = ..., result: _Optional[str] = ..., error: _Optional[str] = ..., metrics: _Optional[_Union[AgentMetrics, _Mapping]] = ...) -> None: ...

class AgentStreamEvent(_message.Message):
    __slots__ = ("execution_id", "event_type", "data", "timestamp")
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    EVENT_TYPE_FIELD_NUMBER: _ClassVar[int]
    DATA_FIELD_NUMBER: _ClassVar[int]
    TIMESTAMP_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    event_type: StreamEventType
    data: str
    timestamp: str
    def __init__(self, execution_id: _Optional[str] = ..., event_type: _Optional[_Union[StreamEventType, str]] = ..., data: _Optional[str] = ..., timestamp: _Optional[str] = ...) -> None: ...

class GetAgentStatusRequest(_message.Message):
    __slots__ = ("execution_id",)
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    def __init__(self, execution_id: _Optional[str] = ...) -> None: ...

class GetAgentStatusResponse(_message.Message):
    __slots__ = ("execution_id", "agent_type", "status", "progress")
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    AGENT_TYPE_FIELD_NUMBER: _ClassVar[int]
    STATUS_FIELD_NUMBER: _ClassVar[int]
    PROGRESS_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    agent_type: _common_pb2.AgentType
    status: str
    progress: float
    def __init__(self, execution_id: _Optional[str] = ..., agent_type: _Optional[_Union[_common_pb2.AgentType, str]] = ..., status: _Optional[str] = ..., progress: _Optional[float] = ...) -> None: ...

class CancelAgentRequest(_message.Message):
    __slots__ = ("execution_id", "reason")
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    REASON_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    reason: str
    def __init__(self, execution_id: _Optional[str] = ..., reason: _Optional[str] = ...) -> None: ...

class CancelAgentResponse(_message.Message):
    __slots__ = ("success", "message")
    SUCCESS_FIELD_NUMBER: _ClassVar[int]
    MESSAGE_FIELD_NUMBER: _ClassVar[int]
    success: bool
    message: str
    def __init__(self, success: bool = ..., message: _Optional[str] = ...) -> None: ...

class AgentMetrics(_message.Message):
    __slots__ = ("total_tokens", "prompt_tokens", "completion_tokens", "duration_seconds", "iterations", "model_used")
    TOTAL_TOKENS_FIELD_NUMBER: _ClassVar[int]
    PROMPT_TOKENS_FIELD_NUMBER: _ClassVar[int]
    COMPLETION_TOKENS_FIELD_NUMBER: _ClassVar[int]
    DURATION_SECONDS_FIELD_NUMBER: _ClassVar[int]
    ITERATIONS_FIELD_NUMBER: _ClassVar[int]
    MODEL_USED_FIELD_NUMBER: _ClassVar[int]
    total_tokens: int
    prompt_tokens: int
    completion_tokens: int
    duration_seconds: float
    iterations: int
    model_used: str
    def __init__(self, total_tokens: _Optional[int] = ..., prompt_tokens: _Optional[int] = ..., completion_tokens: _Optional[int] = ..., duration_seconds: _Optional[float] = ..., iterations: _Optional[int] = ..., model_used: _Optional[str] = ...) -> None: ...
