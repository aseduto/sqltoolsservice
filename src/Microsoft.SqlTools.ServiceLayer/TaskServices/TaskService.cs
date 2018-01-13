﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Extensibility;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.Dmp.Hosting.Utility;
using Microsoft.SqlTools.ServiceLayer.TaskServices.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.TaskServices
{
    public class TaskService: HostedService<TaskService>, IComposableService
    {
        private static readonly Lazy<TaskService> instance = new Lazy<TaskService>(() => new TaskService());
        private SqlTaskManager taskManager = null;
        private IServiceHost serviceHost;

        /// <summary>
        /// Gets the singleton instance object
        /// </summary>
        public static TaskService Instance => instance.Value;

        /// <summary>
        /// Task Manager Instance to use for testing
        /// </summary>
        internal SqlTaskManager TaskManager
        {
            get
            {
                if(taskManager == null)
                {
                    taskManager = SqlTaskManager.Instance;
                }
                return taskManager;
            }
            set
            {
                taskManager = value;
            }
        }

        /// <summary>
        /// Initializes the service instance
        /// </summary>
        public override void InitializeService(IServiceHost serviceHost)
        {
            this.serviceHost = serviceHost;
            Logger.Write(LogLevel.Verbose, "TaskService initialized");
            serviceHost.SetAsyncRequestHandler(ListTasksRequest.Type, HandleListTasksRequest);
            serviceHost.SetAsyncRequestHandler(CancelTaskRequest.Type, HandleCancelTaskRequest);
            TaskManager.TaskAdded += OnTaskAdded;
        }

        /// <summary>
        /// Handles a list tasks request
        /// </summary>
        internal async Task HandleListTasksRequest(
            ListTasksParams listTasksParams,
            RequestContext<ListTasksResponse> context)
        {
            Logger.Write(LogLevel.Verbose, "HandleListTasksRequest");

            Func<Task<ListTasksResponse>> getAllTasks = () =>
            {
                Validate.IsNotNull(nameof(listTasksParams), listTasksParams);
                return Task.Factory.StartNew(() =>
                {
                    ListTasksResponse response = new ListTasksResponse
                    {
                        Tasks = TaskManager.Tasks.Select(x => x.ToTaskInfo()).ToArray()
                    };

                    return response;
                });

            };

            await HandleRequestAsync(getAllTasks, context, "HandleListTasksRequest");
        }

        internal async Task HandleCancelTaskRequest(CancelTaskParams cancelTaskParams, RequestContext<bool> context)
        {
            Logger.Write(LogLevel.Verbose, "HandleCancelTaskRequest");
            Func<Task<bool>> cancelTask = () =>
            {
                Validate.IsNotNull(nameof(cancelTaskParams), cancelTaskParams);

                return Task.Factory.StartNew(() =>
                {
                    Guid taskId;
                    if (Guid.TryParse(cancelTaskParams.TaskId, out taskId))
                    {
                        TaskManager.CancelTask(taskId);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });

            };

            await HandleRequestAsync(cancelTask, context, "HandleCancelTaskRequest");
        }

        private void OnTaskAdded(object sender, TaskEventArgs<SqlTask> e)
        {
            SqlTask sqlTask = e.TaskData;
            if (sqlTask != null)
            {
                TaskInfo taskInfo = sqlTask.ToTaskInfo();
                sqlTask.ScriptAdded += OnTaskScriptAdded;
                sqlTask.MessageAdded += OnTaskMessageAdded;
                sqlTask.StatusChanged += OnTaskStatusChanged;
                serviceHost.SendEvent(TaskCreatedNotification.Type, taskInfo);
            }
        }

        private void OnTaskStatusChanged(object sender, TaskEventArgs<SqlTaskStatus> e)
        {
            SqlTask sqlTask = e.SqlTask;
            if (sqlTask != null)
            {
                TaskProgressInfo progressInfo = new TaskProgressInfo
                {
                    TaskId = sqlTask.TaskId.ToString(),
                    Status = e.TaskData
                };

                if (sqlTask.IsCompleted)
                {
                    progressInfo.Duration = sqlTask.Duration;
                }
                serviceHost.SendEvent(TaskStatusChangedNotification.Type, progressInfo);
            }
        }
        
        private void OnTaskScriptAdded(object sender, TaskEventArgs<TaskScript> e)
        {
            SqlTask sqlTask = e.SqlTask;
            if (sqlTask != null)
            {
                TaskProgressInfo progressInfo = new TaskProgressInfo
                {
                    TaskId = sqlTask.TaskId.ToString(),
                    Status = e.TaskData.Status,
                    Script = e.TaskData.Script,
                    Message = e.TaskData.ErrorMessage,
                };

                serviceHost.SendEvent(TaskStatusChangedNotification.Type, progressInfo);
            }
        }

        private void OnTaskMessageAdded(object sender, TaskEventArgs<TaskMessage> e)
        {
            SqlTask sqlTask = e.SqlTask;
            if (sqlTask != null)
            {
                TaskProgressInfo progressInfo = new TaskProgressInfo
                {
                    TaskId = sqlTask.TaskId.ToString(),
                    Message = e.TaskData.Description,
                    Status = sqlTask.TaskStatus
                };
                serviceHost.SendEvent(TaskStatusChangedNotification.Type, progressInfo);
            }
        }

        public void Dispose()
        {
            TaskManager.TaskAdded -= OnTaskAdded;
            TaskManager.Dispose();
        }
    }
}
