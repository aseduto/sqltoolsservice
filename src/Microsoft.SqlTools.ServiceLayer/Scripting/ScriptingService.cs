//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.SqlTools.ServiceLayer.Connection;
using Microsoft.SqlTools.ServiceLayer.Scripting.Contracts;
using Microsoft.SqlTools.Dmp.Contracts;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.Dmp.Hosting.Utility;
using Microsoft.SqlTools.ServiceLayer.Utility.Extensions;

namespace Microsoft.SqlTools.ServiceLayer.Scripting
{
    /// <summary>
    /// Main class for Scripting Service functionality
    /// </summary>
    public sealed class ScriptingService : IDisposable
    {    
        private const int ScriptingOperationTimeout = 60000;

        private static readonly Lazy<ScriptingService> LazyInstance = new Lazy<ScriptingService>(() => new ScriptingService());

        public static ScriptingService Instance => LazyInstance.Value;

        private static ConnectionService connectionService = null;

        private readonly Lazy<ConcurrentDictionary<string, ScriptingOperation>> operations =
            new Lazy<ConcurrentDictionary<string, ScriptingOperation>>(() => new ConcurrentDictionary<string, ScriptingOperation>());

        private bool disposed;

        /// <summary>
        /// Internal for testing purposes only
        /// </summary>
        internal static ConnectionService ConnectionServiceInstance
        {
            get
            {
                if (connectionService == null)
                {
                    connectionService = ConnectionService.Instance;
                }
                return connectionService;
            }
            set
            {
                connectionService = value;
            }
        }

        /// <summary>
        /// The collection of active operations
        /// </summary>
        internal ConcurrentDictionary<string, ScriptingOperation> ActiveOperations => operations.Value;

        /// <summary>
        /// Initializes the Scripting Service instance
        /// </summary>
        /// <param name="serviceHost"></param>
        public void InitializeService(IServiceHost serviceHost)
        {
            serviceHost.SetRequestHandler(ScriptingRequest.Type, this.HandleScriptExecuteRequest);
            serviceHost.SetRequestHandler(ScriptingCancelRequest.Type, this.HandleScriptCancelRequest);
            serviceHost.SetRequestHandler(ScriptingListObjectsRequest.Type, this.HandleListObjectsRequest);

            // Register handler for shutdown event
            serviceHost.RegisterShutdownTask((shutdownParams, requestContext) =>
            {
                this.Dispose();
                return Task.FromResult(0);
            });
        }

        /// <summary>
        /// Handles request to execute start the list objects operation.
        /// </summary>
        private void HandleListObjectsRequest(ScriptingListObjectsParams parameters, RequestContext<ScriptingListObjectsResult> requestContext)
        {
            try
            {
                ScriptingListObjectsOperation operation = new ScriptingListObjectsOperation(parameters);
                operation.CompleteNotification += (sender, e) => requestContext.SendEvent(ScriptingListObjectsCompleteEvent.Type, e);

                RunTask(requestContext, operation);

                requestContext.SendResult(new ScriptingListObjectsResult { OperationId = operation.OperationId });
            }
            catch (Exception e)
            {
                requestContext.SendError(e);
            }
        }

        /// <summary>
        /// Handles request to start the scripting operation
        /// </summary>
        public void HandleScriptExecuteRequest(ScriptingParams parameters, RequestContext<ScriptingResult> requestContext)
        {
            SmoScriptingOperation operation = null;

            try
            {
                // if a connection string wasn't provided as a parameter then
                // use the owner uri property to lookup its associated ConnectionInfo
                // and then build a connection string out of that
                ConnectionInfo connInfo = null;
                if (parameters.ConnectionString == null)
                {
                    ScriptingService.ConnectionServiceInstance.TryFindConnection(parameters.OwnerUri, out connInfo);
                    if (connInfo != null)
                    {
                        parameters.ConnectionString = ConnectionService.BuildConnectionString(connInfo.ConnectionDetails);
                    }
                    else
                    {
                        throw new Exception("Could not find ConnectionInfo");
                    }
                }

                if (!ShouldCreateScriptAsOperation(parameters))
                {
                    operation = new ScriptingScriptOperation(parameters);
                }
                else
                {
                    operation = new ScriptAsScriptingOperation(parameters);
                }

                operation.PlanNotification += (sender, e) => requestContext.SendEvent(ScriptingPlanNotificationEvent.Type, e);
                operation.ProgressNotification += (sender, e) => requestContext.SendEvent(ScriptingProgressNotificationEvent.Type, e);
                operation.CompleteNotification += (sender, e) => this.SendScriptingCompleteEvent(requestContext, ScriptingCompleteEvent.Type, e, operation, parameters.ScriptDestination);

                RunTask(requestContext, operation);

            }
            catch (Exception e)
            {
                requestContext.SendError(e);
            }
        }

        private bool ShouldCreateScriptAsOperation(ScriptingParams parameters)
        {
            // Scripting as operation should be used to script one object.
            // Scripting data and scripting to file is not supported by scripting as operation
            // To script Select, alter and execute use scripting as operation. The other operation doesn't support those types
            if( (parameters.ScriptingObjects != null && parameters.ScriptingObjects.Count == 1 && parameters.ScriptOptions != null 
                && parameters.ScriptOptions.TypeOfDataToScript == "SchemaOnly" && parameters.ScriptDestination == "ToEditor") || 
                parameters.Operation == ScriptingOperationType.Select || parameters.Operation == ScriptingOperationType.Execute || 
                parameters.Operation == ScriptingOperationType.Alter) 
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Handles request to cancel a script operation.
        /// </summary>
        public void HandleScriptCancelRequest(ScriptingCancelParams parameters, RequestContext<ScriptingCancelResult> requestContext)
        {
            try
            {
                ScriptingOperation operation = null;
                if (this.ActiveOperations.TryRemove(parameters.OperationId, out operation))
                {
                    operation.Cancel();
                }
                else
                {
                    Logger.Write(LogLevel.Normal, string.Format("Operation {0} was not found", operation.OperationId));
                }

                requestContext.SendResult(new ScriptingCancelResult());
            }
            catch (Exception e)
            {
                requestContext.SendError(e);
            }
        }

        private void SendScriptingCompleteEvent<TParams>(RequestContext<ScriptingResult> requestContext, EventType<TParams> eventType, TParams parameters, 
                                                               SmoScriptingOperation operation, string scriptDestination)
        {
            requestContext.SendEvent(eventType, parameters);
            switch (scriptDestination)
            {
                case "ToEditor":
                    requestContext.SendResult(new ScriptingResult { OperationId = operation.OperationId, Script = operation.ScriptText });
                    break;
                case "ToSingleFile":
                    requestContext.SendResult(new ScriptingResult { OperationId = operation.OperationId });
                    break;
                default:
                    requestContext.SendError(string.Format("Operation {0} failed", operation.ToString()));
                    break;
            }
        }

        /// <summary>
        /// Runs the async task that performs the scripting operation.
        /// </summary>
        private void RunTask<T>(RequestContext<T> context, ScriptingOperation operation)
        {
            ScriptingTask = Task.Run(() =>
            {
                try
                {
                    this.ActiveOperations[operation.OperationId] = operation;
                    operation.Execute();
                }
                catch (Exception e)
                {
                    context.SendError(e);
                }
                finally
                {
                    ScriptingOperation temp;
                    this.ActiveOperations.TryRemove(operation.OperationId, out temp);
                }
            }).ContinueWithOnFaulted(t => context.SendError(t.Exception));
        }

        internal Task ScriptingTask { get; set; }

        /// <summary>
        /// Disposes the scripting service and all active scripting operations.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                
                foreach (ScriptingScriptOperation operation in this.ActiveOperations.Values)
                {
                    operation.Dispose();
                }
            }
        }
    }
}
