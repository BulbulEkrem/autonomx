import common_pb2 as _common_pb2
from google.protobuf.internal import containers as _containers
from google.protobuf import descriptor as _descriptor
from google.protobuf import message as _message
from collections.abc import Iterable as _Iterable, Mapping as _Mapping
from typing import ClassVar as _ClassVar, Optional as _Optional, Union as _Union

DESCRIPTOR: _descriptor.FileDescriptor

class GetProjectRequest(_message.Message):
    __slots__ = ("project_id",)
    PROJECT_ID_FIELD_NUMBER: _ClassVar[int]
    project_id: str
    def __init__(self, project_id: _Optional[str] = ...) -> None: ...

class GetProjectResponse(_message.Message):
    __slots__ = ("project_id", "name", "description", "status", "config", "tasks")
    PROJECT_ID_FIELD_NUMBER: _ClassVar[int]
    NAME_FIELD_NUMBER: _ClassVar[int]
    DESCRIPTION_FIELD_NUMBER: _ClassVar[int]
    STATUS_FIELD_NUMBER: _ClassVar[int]
    CONFIG_FIELD_NUMBER: _ClassVar[int]
    TASKS_FIELD_NUMBER: _ClassVar[int]
    project_id: str
    name: str
    description: str
    status: _common_pb2.ProjectStatus
    config: str
    tasks: _containers.RepeatedCompositeFieldContainer[_common_pb2.TaskInfo]
    def __init__(self, project_id: _Optional[str] = ..., name: _Optional[str] = ..., description: _Optional[str] = ..., status: _Optional[_Union[_common_pb2.ProjectStatus, str]] = ..., config: _Optional[str] = ..., tasks: _Optional[_Iterable[_Union[_common_pb2.TaskInfo, _Mapping]]] = ...) -> None: ...

class GetTaskBoardRequest(_message.Message):
    __slots__ = ("project_id", "filter_status")
    PROJECT_ID_FIELD_NUMBER: _ClassVar[int]
    FILTER_STATUS_FIELD_NUMBER: _ClassVar[int]
    project_id: str
    filter_status: _common_pb2.TaskStatus
    def __init__(self, project_id: _Optional[str] = ..., filter_status: _Optional[_Union[_common_pb2.TaskStatus, str]] = ...) -> None: ...

class GetTaskBoardResponse(_message.Message):
    __slots__ = ("tasks",)
    TASKS_FIELD_NUMBER: _ClassVar[int]
    tasks: _containers.RepeatedCompositeFieldContainer[_common_pb2.TaskInfo]
    def __init__(self, tasks: _Optional[_Iterable[_Union[_common_pb2.TaskInfo, _Mapping]]] = ...) -> None: ...

class UpdateTaskStatusRequest(_message.Message):
    __slots__ = ("task_id", "new_status", "assigned_worker", "files_touched", "result")
    TASK_ID_FIELD_NUMBER: _ClassVar[int]
    NEW_STATUS_FIELD_NUMBER: _ClassVar[int]
    ASSIGNED_WORKER_FIELD_NUMBER: _ClassVar[int]
    FILES_TOUCHED_FIELD_NUMBER: _ClassVar[int]
    RESULT_FIELD_NUMBER: _ClassVar[int]
    task_id: str
    new_status: _common_pb2.TaskStatus
    assigned_worker: str
    files_touched: _containers.RepeatedScalarFieldContainer[str]
    result: str
    def __init__(self, task_id: _Optional[str] = ..., new_status: _Optional[_Union[_common_pb2.TaskStatus, str]] = ..., assigned_worker: _Optional[str] = ..., files_touched: _Optional[_Iterable[str]] = ..., result: _Optional[str] = ...) -> None: ...

class UpdateTaskStatusResponse(_message.Message):
    __slots__ = ("success", "message")
    SUCCESS_FIELD_NUMBER: _ClassVar[int]
    MESSAGE_FIELD_NUMBER: _ClassVar[int]
    success: bool
    message: str
    def __init__(self, success: bool = ..., message: _Optional[str] = ...) -> None: ...

class FileLockRequest(_message.Message):
    __slots__ = ("task_id", "worker_id", "file_paths")
    TASK_ID_FIELD_NUMBER: _ClassVar[int]
    WORKER_ID_FIELD_NUMBER: _ClassVar[int]
    FILE_PATHS_FIELD_NUMBER: _ClassVar[int]
    task_id: str
    worker_id: str
    file_paths: _containers.RepeatedScalarFieldContainer[str]
    def __init__(self, task_id: _Optional[str] = ..., worker_id: _Optional[str] = ..., file_paths: _Optional[_Iterable[str]] = ...) -> None: ...

class FileLockResponse(_message.Message):
    __slots__ = ("success", "locked_by_others", "message")
    SUCCESS_FIELD_NUMBER: _ClassVar[int]
    LOCKED_BY_OTHERS_FIELD_NUMBER: _ClassVar[int]
    MESSAGE_FIELD_NUMBER: _ClassVar[int]
    success: bool
    locked_by_others: _containers.RepeatedScalarFieldContainer[str]
    message: str
    def __init__(self, success: bool = ..., locked_by_others: _Optional[_Iterable[str]] = ..., message: _Optional[str] = ...) -> None: ...
