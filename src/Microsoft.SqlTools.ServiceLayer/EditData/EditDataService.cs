//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.Dmp.Hosting.Utility;
using Microsoft.SqlTools.ServiceLayer.Connection;
using Microsoft.SqlTools.ServiceLayer.EditData.Contracts;
using Microsoft.SqlTools.ServiceLayer.QueryExecution;
using Microsoft.SqlTools.ServiceLayer.QueryExecution.Contracts.ExecuteRequests;
using ConnectionType = Microsoft.SqlTools.ServiceLayer.Connection.ConnectionType;

namespace Microsoft.SqlTools.ServiceLayer.EditData
{
    /// <summary>
    /// Service that handles edit data scenarios
    /// </summary>
    public class EditDataService
    {
        #region Singleton Instance Implementation

        private static readonly Lazy<EditDataService> LazyInstance = new Lazy<EditDataService>(() => new EditDataService());

        public static EditDataService Instance => LazyInstance.Value;

        private EditDataService()
        {
            queryExecutionService = QueryExecutionService.Instance;
            connectionService = ConnectionService.Instance;
            metadataFactory = new SmoEditMetadataFactory();
        }

        internal EditDataService(QueryExecutionService qes, ConnectionService cs, IEditMetadataFactory factory)
        {
            queryExecutionService = qes;
            connectionService = cs;
            metadataFactory = factory;
        }

        #endregion

        #region Member Variables 

        private readonly ConnectionService connectionService;

        private readonly IEditMetadataFactory metadataFactory;

        private readonly QueryExecutionService queryExecutionService;

        private readonly Lazy<ConcurrentDictionary<string, EditSession>> editSessions = new Lazy<ConcurrentDictionary<string, EditSession>>(
            () => new ConcurrentDictionary<string, EditSession>());

        #endregion

        #region Properties

        /// <summary>
        /// Dictionary mapping OwnerURIs to active sessions
        /// </summary>
        internal ConcurrentDictionary<string, EditSession> ActiveSessions => editSessions.Value;

        #endregion

        /// <summary>
        /// Initializes the edit data service with the service host
        /// </summary>
        /// <param name="serviceHost">The service host to register commands/events with</param>
        public void InitializeService(IServiceHost serviceHost)
        {
            // Register handlers for requests
            serviceHost.SetRequestHandler(EditCreateRowRequest.Type, HandleCreateRowRequest);
            serviceHost.SetRequestHandler(EditDeleteRowRequest.Type, HandleDeleteRowRequest);
            serviceHost.SetRequestHandler(EditDisposeRequest.Type, HandleDisposeRequest);
            serviceHost.SetRequestHandler(EditInitializeRequest.Type, HandleInitializeRequest);
            serviceHost.SetRequestHandler(EditRevertCellRequest.Type, HandleRevertCellRequest);
            serviceHost.SetRequestHandler(EditRevertRowRequest.Type, HandleRevertRowRequest);
            serviceHost.SetRequestHandler(EditUpdateCellRequest.Type, HandleUpdateCellRequest);
            
            serviceHost.SetAsyncRequestHandler(EditCommitRequest.Type, HandleCommitRequest);
            serviceHost.SetAsyncRequestHandler(EditSubsetRequest.Type, HandleSubsetRequest);
        }

        #region Request Handlers

        internal void HandleSessionRequest<TResult>(SessionOperationParams sessionParams,
            RequestContext<TResult> requestContext, Func<EditSession, TResult> sessionOperation)
        {
            try
            {
                EditSession editSession = GetActiveSessionOrThrow(sessionParams.OwnerUri);

                // Get the result from execution of the editSession operation
                TResult result = sessionOperation(editSession);
                requestContext.SendResult(result);
            }
            catch (Exception e)
            {
                requestContext.SendError(e.Message);
            }
        }

        internal void HandleCreateRowRequest(EditCreateRowParams createParams,
            RequestContext<EditCreateRowResult> requestContext)
        {
            HandleSessionRequest(createParams, requestContext, s => s.CreateRow());
        }

        internal void HandleDeleteRowRequest(EditDeleteRowParams deleteParams,
            RequestContext<EditDeleteRowResult> requestContext)
        {
            HandleSessionRequest(deleteParams, requestContext, session =>
            {
                // Add the delete row to the edit cache
                session.DeleteRow(deleteParams.RowId);
                return new EditDeleteRowResult();
            });
        }

        internal void HandleDisposeRequest(EditDisposeParams disposeParams,
            RequestContext<EditDisposeResult> requestContext)
        {
            try
            {
                // Sanity check the owner URI
                Validate.IsNotNullOrWhitespaceString(nameof(disposeParams.OwnerUri), disposeParams.OwnerUri);

                // Attempt to remove the editSession
                EditSession editSession;
                if (!ActiveSessions.TryRemove(disposeParams.OwnerUri, out editSession))
                {
                    requestContext.SendError(SR.EditDataSessionNotFound);
                    return;
                }

                // Everything was successful, return success
                requestContext.SendResult(new EditDisposeResult());
            }
            catch (Exception e)
            {
                requestContext.SendError(e.Message);
            }
        }

        internal void HandleInitializeRequest(EditInitializeParams initParams,
            RequestContext<EditInitializeResult> requestContext)
        {
            Action<Exception> executionFailureHandler = e => SendSessionReadyEvent(requestContext, initParams.OwnerUri, false, e.Message);
            Action executionSuccessHandler = () => SendSessionReadyEvent(requestContext, initParams.OwnerUri, true, null);

            EditSession.Connector connector = () => connectionService.GetOrOpenConnection(initParams.OwnerUri, ConnectionType.Edit, alwaysPersistSecurity: true);
            EditSession.QueryRunner queryRunner = q => SessionInitializeQueryRunner(initParams.OwnerUri, requestContext, q);

            try
            {
                // Make sure we have info to process this request
                Validate.IsNotNullOrWhitespaceString(nameof(initParams.OwnerUri), initParams.OwnerUri);
                Validate.IsNotNullOrWhitespaceString(nameof(initParams.ObjectName), initParams.ObjectName);
                Validate.IsNotNullOrWhitespaceString(nameof(initParams.ObjectType), initParams.ObjectType);

                // Create a session and add it to the session list
                EditSession session = new EditSession(metadataFactory);
                if (!ActiveSessions.TryAdd(initParams.OwnerUri, session))
                {
                    throw new InvalidOperationException(SR.EditDataSessionAlreadyExists);
                }

                // Initialize the session
                session.Initialize(initParams, connector, queryRunner, executionSuccessHandler, executionFailureHandler);

                // Send the result
                requestContext.SendResult(new EditInitializeResult());
            }
            catch (Exception e)
            {
                requestContext.SendError(e.Message);
            }
        }

        internal void HandleRevertCellRequest(EditRevertCellParams revertParams,
            RequestContext<EditRevertCellResult> requestContext)
        {
            HandleSessionRequest(revertParams, requestContext,
                session => session.RevertCell(revertParams.RowId, revertParams.ColumnId));
        }

        internal void HandleRevertRowRequest(EditRevertRowParams revertParams,
            RequestContext<EditRevertRowResult> requestContext)
        {
            HandleSessionRequest(revertParams, requestContext, session =>
            {
                session.RevertRow(revertParams.RowId);
                return new EditRevertRowResult();
            });
        }

        internal async Task HandleSubsetRequest(EditSubsetParams subsetParams,
            RequestContext<EditSubsetResult> requestContext)
        {
            try
            {
                EditSession session = GetActiveSessionOrThrow(subsetParams.OwnerUri);

                EditRow[] rows = await session.GetRows(subsetParams.RowStartIndex, subsetParams.RowCount);
                EditSubsetResult result = new EditSubsetResult
                {
                    RowCount = rows.Length,
                    Subset = rows
                };

                requestContext.SendResult(result);
            }
            catch(Exception e)
            {
                requestContext.SendError(e.Message);
            }
        }

        internal void HandleUpdateCellRequest(EditUpdateCellParams updateParams,
            RequestContext<EditUpdateCellResult> requestContext)
        {
            HandleSessionRequest(updateParams, requestContext,
                session => session.UpdateCell(updateParams.RowId, updateParams.ColumnId, updateParams.NewValue));
        }

        internal async Task HandleCommitRequest(EditCommitParams commitParams,
            RequestContext<EditCommitResult> requestContext)
        {
            // Setup a callback for if the edits have been successfully written to the db
            Action successHandler = () => requestContext.SendResult(new EditCommitResult());

            // Setup a callback for if the edits failed to be written to db
            Action<Exception> failureHandler = e => requestContext.SendError(e.Message);

            try
            {
                // Get the editSession
                EditSession editSession = GetActiveSessionOrThrow(commitParams.OwnerUri);

                // Get a connection for doing the committing
                DbConnection conn = await connectionService.GetOrOpenConnection(commitParams.OwnerUri,
                    ConnectionType.Edit);
                editSession.CommitEdits(conn, successHandler, failureHandler);
            }
            catch (Exception e)
            {
                failureHandler(e);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Returns the session with the given owner URI or throws if it can't be found
        /// </summary>
        /// <exception cref="Exception">If the edit session doesn't exist</exception>
        /// <param name="ownerUri">Owner URI for the edit session</param>
        /// <returns>The edit session that corresponds to the owner URI</returns>
        private EditSession GetActiveSessionOrThrow(string ownerUri)
        {
            // Sanity check the owner URI is provided
            Validate.IsNotNullOrWhitespaceString(nameof(ownerUri), ownerUri);

            // Attempt to get the editSession, throw if unable
            EditSession editSession;
            if (!ActiveSessions.TryGetValue(ownerUri, out editSession))
            {
                throw new Exception(SR.EditDataSessionNotFound);
            }

            return editSession;
        }

        private async Task<EditSession.EditSessionQueryExecutionState> SessionInitializeQueryRunner(string ownerUri,
            IEventSender eventSender, string query)
        {
            // Open a task completion source, effectively creating a synchronous block
            TaskCompletionSource<EditSession.EditSessionQueryExecutionState> taskCompletion =
                new TaskCompletionSource<EditSession.EditSessionQueryExecutionState>();

            // Setup callback for successful query creation
            // NOTE: We do not want to set the task completion source, since we will continue executing the query after
            Func<Query, Task<bool>> queryCreateSuccessCallback = q => Task.FromResult(true);

            // Setup callback for failed query creation
            Func<string, Task> queryCreateFailureCallback = m =>
            {
                taskCompletion.SetResult(new EditSession.EditSessionQueryExecutionState(null, m));
                return Task.FromResult(0);
            };

            // Setup callback for successful query execution
            Query.QueryAsyncEventHandler queryCompleteSuccessCallback = q =>
            {
                taskCompletion.SetResult(new EditSession.EditSessionQueryExecutionState(q));
                return Task.FromResult(0);
            };

            // Setup callback for failed query execution
            Query.QueryAsyncErrorEventHandler queryCompleteFailureCallback = (q, e) =>
            {
                taskCompletion.SetResult(new EditSession.EditSessionQueryExecutionState(null));
                return Task.FromResult(0);
            };

            // Execute the query
            ExecuteStringParams executeParams = new ExecuteStringParams
            {
                Query = query,
                OwnerUri = ownerUri
            };
            await queryExecutionService.InterServiceExecuteQuery(executeParams, null, eventSender,
                queryCreateSuccessCallback, queryCreateFailureCallback,
                queryCompleteSuccessCallback, queryCompleteFailureCallback);

            // Wait for the completion source to complete, this will wait until the query has
            // completed and sent all its events.
            return await taskCompletion.Task;
        }

        private static void SendSessionReadyEvent(IEventSender eventSender, string ownerUri, bool success,
            string message)
        {
            var sessionReadyParams = new EditSessionReadyParams
            {
                OwnerUri = ownerUri,
                Message = message,
                Success = success
            };

            eventSender.SendEvent(EditSessionReadyEvent.Type, sessionReadyParams);
        }

        #endregion

    }
}
