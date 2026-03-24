from google.protobuf.internal import containers as _containers
from google.protobuf.internal import enum_type_wrapper as _enum_type_wrapper
from google.protobuf import descriptor as _descriptor
from google.protobuf import message as _message
from collections.abc import Mapping as _Mapping
from typing import ClassVar as _ClassVar, Optional as _Optional, Union as _Union

DESCRIPTOR: _descriptor.FileDescriptor

class ExecutionEnvironment(int, metaclass=_enum_type_wrapper.EnumTypeWrapper):
    __slots__ = ()
    EXECUTION_ENVIRONMENT_UNSPECIFIED: _ClassVar[ExecutionEnvironment]
    EXECUTION_ENVIRONMENT_DOCKER: _ClassVar[ExecutionEnvironment]
    EXECUTION_ENVIRONMENT_HOST: _ClassVar[ExecutionEnvironment]
    EXECUTION_ENVIRONMENT_SANDBOX: _ClassVar[ExecutionEnvironment]

class OutputType(int, metaclass=_enum_type_wrapper.EnumTypeWrapper):
    __slots__ = ()
    OUTPUT_TYPE_UNSPECIFIED: _ClassVar[OutputType]
    OUTPUT_TYPE_STDOUT: _ClassVar[OutputType]
    OUTPUT_TYPE_STDERR: _ClassVar[OutputType]
    OUTPUT_TYPE_EXIT: _ClassVar[OutputType]
EXECUTION_ENVIRONMENT_UNSPECIFIED: ExecutionEnvironment
EXECUTION_ENVIRONMENT_DOCKER: ExecutionEnvironment
EXECUTION_ENVIRONMENT_HOST: ExecutionEnvironment
EXECUTION_ENVIRONMENT_SANDBOX: ExecutionEnvironment
OUTPUT_TYPE_UNSPECIFIED: OutputType
OUTPUT_TYPE_STDOUT: OutputType
OUTPUT_TYPE_STDERR: OutputType
OUTPUT_TYPE_EXIT: OutputType

class ExecuteCodeRequest(_message.Message):
    __slots__ = ("execution_id", "project_id", "command", "working_directory", "environment", "timeout_seconds", "env_vars")
    class EnvVarsEntry(_message.Message):
        __slots__ = ("key", "value")
        KEY_FIELD_NUMBER: _ClassVar[int]
        VALUE_FIELD_NUMBER: _ClassVar[int]
        key: str
        value: str
        def __init__(self, key: _Optional[str] = ..., value: _Optional[str] = ...) -> None: ...
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    PROJECT_ID_FIELD_NUMBER: _ClassVar[int]
    COMMAND_FIELD_NUMBER: _ClassVar[int]
    WORKING_DIRECTORY_FIELD_NUMBER: _ClassVar[int]
    ENVIRONMENT_FIELD_NUMBER: _ClassVar[int]
    TIMEOUT_SECONDS_FIELD_NUMBER: _ClassVar[int]
    ENV_VARS_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    project_id: str
    command: str
    working_directory: str
    environment: ExecutionEnvironment
    timeout_seconds: int
    env_vars: _containers.ScalarMap[str, str]
    def __init__(self, execution_id: _Optional[str] = ..., project_id: _Optional[str] = ..., command: _Optional[str] = ..., working_directory: _Optional[str] = ..., environment: _Optional[_Union[ExecutionEnvironment, str]] = ..., timeout_seconds: _Optional[int] = ..., env_vars: _Optional[_Mapping[str, str]] = ...) -> None: ...

class ExecuteCodeResponse(_message.Message):
    __slots__ = ("execution_id", "exit_code", "stdout", "stderr", "duration_seconds", "timed_out")
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    EXIT_CODE_FIELD_NUMBER: _ClassVar[int]
    STDOUT_FIELD_NUMBER: _ClassVar[int]
    STDERR_FIELD_NUMBER: _ClassVar[int]
    DURATION_SECONDS_FIELD_NUMBER: _ClassVar[int]
    TIMED_OUT_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    exit_code: int
    stdout: str
    stderr: str
    duration_seconds: float
    timed_out: bool
    def __init__(self, execution_id: _Optional[str] = ..., exit_code: _Optional[int] = ..., stdout: _Optional[str] = ..., stderr: _Optional[str] = ..., duration_seconds: _Optional[float] = ..., timed_out: bool = ...) -> None: ...

class ExecuteCodeEvent(_message.Message):
    __slots__ = ("execution_id", "output_type", "data", "timestamp")
    EXECUTION_ID_FIELD_NUMBER: _ClassVar[int]
    OUTPUT_TYPE_FIELD_NUMBER: _ClassVar[int]
    DATA_FIELD_NUMBER: _ClassVar[int]
    TIMESTAMP_FIELD_NUMBER: _ClassVar[int]
    execution_id: str
    output_type: OutputType
    data: str
    timestamp: str
    def __init__(self, execution_id: _Optional[str] = ..., output_type: _Optional[_Union[OutputType, str]] = ..., data: _Optional[str] = ..., timestamp: _Optional[str] = ...) -> None: ...
