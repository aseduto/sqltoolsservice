//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Extensibility;
using Microsoft.SqlTools.ServiceLayer.TaskServices;
using Microsoft.SqlTools.ServiceLayer.TaskServices.Contracts;
using Microsoft.SqlTools.ServiceLayer.Test.Common.RequestContextMocking;
using Microsoft.SqlTools.ServiceLayer.UnitTests.Utility;
using Moq;
using Xunit;

namespace Microsoft.SqlTools.ServiceLayer.UnitTests.TaskServices
{
    public class TaskServiceTests : ServiceTestBase
    {
        private TaskService service;
        private Mock<IServiceHost> serviceHostMock;
        private TaskMetadata taskMetaData = new TaskMetadata
        {
            ServerName = "server name",
            DatabaseName = "database name"
        };

        public TaskServiceTests()
        {
            serviceHostMock = new Mock<IServiceHost>();
            service = CreateService();
            service.InitializeService(serviceHostMock.Object);
        }

        [Fact]
        public void TaskListRequestErrorsIfParameterIsNull()
        {
            var efv = new EventFlowValidator<ListTasksResponse>()
                .AddSimpleErrorValidation((m, c) => Assert.Contains("ArgumentNullException", m))
                .Complete();

            service.HandleListTasksRequest(null, efv.Object);
            efv.Validate();
        }

        [Fact]
        public void NewTaskShouldSendNotification()
        {
            serviceHostMock.AddEventHandling(TaskCreatedNotification.Type, null);
            serviceHostMock.AddEventHandling(TaskStatusChangedNotification.Type, null);
            DatabaseOperationStub operation = new DatabaseOperationStub();
            SqlTask sqlTask = service.TaskManager.CreateTask(taskMetaData, operation.FunctionToRun);
            sqlTask.Run();
           
            serviceHostMock.Verify(x => x.SendEvent(TaskCreatedNotification.Type,
                It.Is<TaskInfo>(t => t.TaskId == sqlTask.TaskId.ToString() && t.ProviderName == "MSSQL")), Times.Once());
            operation.Stop();
            Thread.Sleep(2000);

            serviceHostMock.Verify(x => x.SendEvent(TaskStatusChangedNotification.Type,
                It.Is<TaskProgressInfo>(t => t.TaskId == sqlTask.TaskId.ToString())), Times.AtLeastOnce());
        }

        [Fact]
        public async Task CancelTaskShouldCancelTheOperationAndSendNotification()
        {
            serviceHostMock.AddEventHandling(TaskCreatedNotification.Type, null);
            serviceHostMock.AddEventHandling(TaskStatusChangedNotification.Type, null);
            DatabaseOperationStub operation = new DatabaseOperationStub();
            SqlTask sqlTask = service.TaskManager.CreateTask(taskMetaData, operation.FunctionToRun, operation.FunctionToCancel);
            Task taskToVerify = sqlTask.RunAsync().ContinueWith(task =>
            {
                serviceHostMock.Verify(x => x.SendEvent(TaskStatusChangedNotification.Type,
                           It.Is<TaskProgressInfo>(t => t.Status == SqlTaskStatus.Canceled)), Times.AtLeastOnce());
            });
            CancelTaskParams cancelParams = new CancelTaskParams
            {
                TaskId = sqlTask.TaskId.ToString()
            };

            RunAndVerify<bool>(
                test: (requestContext) => service.HandleCancelTaskRequest(cancelParams, requestContext),
                verify: ((result) =>
                {
                }));

            serviceHostMock.Verify(x => x.SendEvent(TaskCreatedNotification.Type,
                It.Is<TaskInfo>(t => t.TaskId == sqlTask.TaskId.ToString())), Times.Once());
            await taskToVerify;
        }


        [Fact]
        public void TaskListTaskShouldReturnAllTasks()
        {
            serviceHostMock.AddEventHandling(TaskCreatedNotification.Type, null);
            serviceHostMock.AddEventHandling(TaskStatusChangedNotification.Type, null);
            DatabaseOperationStub operation = new DatabaseOperationStub();
            SqlTask sqlTask = service.TaskManager.CreateTask(taskMetaData, operation.FunctionToRun);
            sqlTask.Run();
            ListTasksParams listParams = new ListTasksParams();

            RunAndVerify<ListTasksResponse>(
                test: (requestContext) => service.HandleListTasksRequest(listParams, requestContext),
                verify: ((result) =>
                {
                    Assert.True(result.Tasks.Any(x => x.TaskId == sqlTask.TaskId.ToString()));
                }));

            operation.Stop();
        }

        protected TaskService CreateService()
        {
            CreateServiceProviderWithMinServices();

            // Create the service using the service provider, which will initialize dependencies
            return ServiceProvider.GetService<TaskService>();
        }

        protected override RegisteredServiceProvider CreateServiceProviderWithMinServices()
        {
            TaskService service = new TaskService();
            service.TaskManager = new SqlTaskManager();
            return CreateProvider().RegisterSingleService(service);
        }
    }
}
