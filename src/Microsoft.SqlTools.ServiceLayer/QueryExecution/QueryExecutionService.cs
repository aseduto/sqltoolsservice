﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Extensibility;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.Dmp.Hosting.Utility;
using Microsoft.SqlTools.ServiceLayer.Capabilities;
using Microsoft.SqlTools.ServiceLayer.Capabilities.Contracts;
using Microsoft.SqlTools.ServiceLayer.Connection;
using Microsoft.SqlTools.ServiceLayer.Connection.Contracts;
using Microsoft.SqlTools.ServiceLayer.QueryExecution.Contracts;
using Microsoft.SqlTools.ServiceLayer.QueryExecution.Contracts.ExecuteRequests;
using Microsoft.SqlTools.ServiceLayer.QueryExecution.DataStorage;
using Microsoft.SqlTools.ServiceLayer.SqlContext;
using Microsoft.SqlTools.ServiceLayer.Workspace;
using Microsoft.SqlTools.ServiceLayer.Workspace.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.QueryExecution
{
    /// <summary>
    /// Service for executing queries
    /// </summary>
    public sealed class QueryExecutionService : IDisposable
    {
        #region Singleton Instance Implementation

        private static readonly Lazy<QueryExecutionService> LazyInstance = new Lazy<QueryExecutionService>(() => new QueryExecutionService());

        /// <summary>
        /// Singleton instance of the query execution service
        /// </summary>
        public static QueryExecutionService Instance => LazyInstance.Value;

        private QueryExecutionService()
        {
            ConnectionService = ConnectionService.Instance;
            WorkspaceService = WorkspaceService<SqlToolsSettings>.Instance;
            Settings = new SqlToolsSettings();
        }

        internal QueryExecutionService(ConnectionService connService, WorkspaceService<SqlToolsSettings> workspaceService)
        {
            ConnectionService = connService;
            WorkspaceService = workspaceService;
            Settings = new SqlToolsSettings();
        }

        #endregion

        #region Properties

        /// <summary>
        /// File factory to be used to create a buffer file for results.
        /// </summary>
        /// <remarks>
        /// Made internal here to allow for overriding in unit testing
        /// </remarks>
        internal IFileStreamFactory BufferFileStreamFactory;

        /// <summary>
        /// File factory to be used to create a buffer file for results
        /// </summary>
        private IFileStreamFactory BufferFileFactory
        {
            get
            {
                if (BufferFileStreamFactory == null)
                {
                    BufferFileStreamFactory = new ServiceBufferFileStreamFactory
                    {
                        ExecutionSettings = Settings.QueryExecutionSettings
                    };
                }
                return BufferFileStreamFactory;
            }
        }

        /// <summary>
        /// File factory to be used to create CSV files from result sets. Set to internal in order
        /// to allow overriding in unit testing
        /// </summary>
        internal IFileStreamFactory CsvFileFactory { get; set; }

        /// <summary>
        /// File factory to be used to create Excel files from result sets. Set to internal in order
        /// to allow overriding in unit testing
        /// </summary>
        internal IFileStreamFactory ExcelFileFactory { get; set; }

        /// <summary>
        /// File factory to be used to create JSON files from result sets. Set to internal in order
        /// to allow overriding in unit testing
        /// </summary>
        internal IFileStreamFactory JsonFileFactory { get; set; }

        /// <summary>
        /// The collection of active queries
        /// </summary>
        internal ConcurrentDictionary<string, Query> ActiveQueries => queries.Value;

        /// <summary>
        /// Instance of the connection service, used to get the connection info for a given owner URI
        /// </summary>
        private ConnectionService ConnectionService { get; }

        private WorkspaceService<SqlToolsSettings> WorkspaceService { get; }

        /// <summary>
        /// Internal storage of active queries, lazily constructed as a threadsafe dictionary
        /// </summary>
        private readonly Lazy<ConcurrentDictionary<string, Query>> queries =
            new Lazy<ConcurrentDictionary<string, Query>>(() => new ConcurrentDictionary<string, Query>());

        /// <summary>
        /// Settings that will be used to execute queries. Internal for unit testing
        /// </summary>
        internal SqlToolsSettings Settings { get; set; }

        /// <summary>
        /// Holds a map from the simple execute unique GUID and the underlying task that is being ran
        /// </summary>
        private readonly Lazy<ConcurrentDictionary<string, Task>> simpleExecuteRequests = 
            new Lazy<ConcurrentDictionary<string, Task>>(() => new ConcurrentDictionary<string, Task>());

        /// <summary>
        /// Holds a map from the simple execute unique GUID and the underlying task that is being ran
        /// </summary>
        internal ConcurrentDictionary<string, Task> ActiveSimpleExecuteRequests => simpleExecuteRequests.Value;

        #endregion

        /// <summary>
        /// Initializes the service with the service host, registers request handlers and shutdown
        /// event handler.
        /// </summary>
        /// <param name="serviceHost">The service host instance to register with</param>
        public void InitializeService(IServiceHost serviceHost, IMultiServiceProvider serviceProvider)
        {
            // Register handlers for requests
            serviceHost.SetRequestHandler(ExecuteDocumentSelectionRequest.Type, HandleExecuteRequest);
            serviceHost.SetRequestHandler(ExecuteDocumentStatementRequest.Type, HandleExecuteRequest);
            serviceHost.SetRequestHandler(ExecuteStringRequest.Type, HandleExecuteRequest);
            serviceHost.SetAsyncRequestHandler(SubsetRequest.Type, HandleResultSubsetRequest);
            serviceHost.SetRequestHandler(QueryDisposeRequest.Type, HandleDisposeRequest);
            serviceHost.SetRequestHandler(QueryCancelRequest.Type, HandleCancelRequest);
            serviceHost.SetRequestHandler(SaveResultsAsCsvRequest.Type, HandleSaveResultsAsCsvRequest);
            serviceHost.SetRequestHandler(SaveResultsAsExcelRequest.Type, HandleSaveResultsAsExcelRequest);
            serviceHost.SetRequestHandler(SaveResultsAsJsonRequest.Type, HandleSaveResultsAsJsonRequest);
            serviceHost.SetAsyncRequestHandler(QueryExecutionPlanRequest.Type, HandleExecutionPlanRequest);
            serviceHost.SetRequestHandler(SimpleExecuteRequest.Type, HandleSimpleExecuteRequest);

            // Register handler for shutdown event
            serviceHost.RegisterShutdownTask((shutdownParams, requestContext) =>
            {
                Dispose();
                return Task.FromResult(0);
            });

            // Register a handler for when the configuration changes
            WorkspaceService.RegisterConfigChangeCallback(UpdateSettings);
            
            // Tell the capabilities service that this will handle serialization
            CapabilitiesService capabilitiesService = serviceProvider.GetService<CapabilitiesService>();
            capabilitiesService?.FeaturesMetadata.Add(new FeatureMetadataProvider
            {
                FeatureName = "serializationService",
                Enabled = true,
                OptionsMetadata = new ServiceOption[0]
            });
        }

        #region Request Handlers

        /// <summary>
        /// Handles request to execute a selection of a document in the workspace service
        /// </summary>
        internal void HandleExecuteRequest(ExecuteRequestParamsBase executeParams,
            RequestContext<ExecuteRequestResult> requestContext)
        {
            // Setup actions to perform upon successful start and on failure to start
            Func<Query, bool> queryCreateSuccessAction = q => {
                requestContext.SendResult(new ExecuteRequestResult());
                return true;
            };
            Action<string> queryCreateFailureAction = message => requestContext.SendError(message);

            // Use the internal handler to launch the query
            InterServiceExecuteQuery(executeParams, null, requestContext, queryCreateSuccessAction, queryCreateFailureAction, null, null);
        }

        /// <summary>
        /// Handles a request to execute a string and return the result
        /// </summary>
        internal void HandleSimpleExecuteRequest(SimpleExecuteParams executeParams,
            RequestContext<SimpleExecuteResult> requestContext)
        {
            try
            {
                string randomUri = Guid.NewGuid().ToString();
                ExecuteStringParams executeStringParams = new ExecuteStringParams
                {
                    Query = executeParams.QueryString,
                    // generate guid as the owner uri to make sure every query is unique
                    OwnerUri = randomUri
                };

                // get connection
                ConnectionInfo connInfo;
                if (!ConnectionService.TryFindConnection(executeParams.OwnerUri, out connInfo))
                {
                    requestContext.SendError(SR.QueryServiceQueryInvalidOwnerUri);
                    return;
                }
                
                ConnectParams connectParams = new ConnectParams
                {
                    OwnerUri = randomUri,
                    Connection = connInfo.ConnectionDetails,
                    Type = ConnectionType.Default
                };
                
                Task workTask = Task.Run(async () => {
                    await ConnectionService.Connect(connectParams);

                    ConnectionInfo newConn;
                    ConnectionService.TryFindConnection(randomUri, out newConn);

                    Action<string> queryCreateFailureAction = message => requestContext.SendError(message);

                    ResultOnlyContext<SimpleExecuteResult> newContext = new ResultOnlyContext<SimpleExecuteResult>(requestContext);

                    // handle sending event back when the query completes
                    Query.QueryEventHandler queryComplete = async query =>
                    {
                        try
                        {
                            // check to make sure any results were recieved
                            if (query.Batches.Length == 0 
                                || query.Batches[0].ResultSets.Count == 0) 
                            {
                                requestContext.SendError(SR.QueryServiceResultSetHasNoResults);
                                return;
                            } 

                            long rowCount = query.Batches[0].ResultSets[0].RowCount;
                            // check to make sure there is a safe amount of rows to load into memory
                            if (rowCount > int.MaxValue) 
                            {
                                requestContext.SendError(SR.QueryServiceResultSetTooLarge);
                                return;
                            }
                            
                            SimpleExecuteResult result = new SimpleExecuteResult
                            {
                                RowCount = rowCount,
                                ColumnInfo = query.Batches[0].ResultSets[0].Columns,
                                Rows = new DbCellValue[0][] 
                            };

                            if (rowCount > 0)
                            {
                                SubsetParams subsetRequestParams = new SubsetParams
                                {
                                    OwnerUri = randomUri,
                                    BatchIndex = 0,
                                    ResultSetIndex = 0,
                                    RowsStartIndex = 0,
                                    RowsCount = Convert.ToInt32(rowCount)
                                };
                                // get the data to send back
                                ResultSetSubset subset = await InterServiceResultSubset(subsetRequestParams);
                                result.Rows = subset.Rows;
                            }
                            requestContext.SendResult(result);
                        } 
                        finally 
                        {
                            Query removedQuery;
                            Task removedTask;
                            // remove the active query since we are done with it
                            ActiveQueries.TryRemove(randomUri, out removedQuery);
                            ActiveSimpleExecuteRequests.TryRemove(randomUri, out removedTask);
                            ConnectionService.Disconnect(new DisconnectParams(){
                                OwnerUri = randomUri,
                                Type = null
                            });
                        }
                    };

                    // handle sending error back when query fails
                    Query.QueryErrorEventHandler queryFail = (q, e) =>
                    {
                        requestContext.SendError(e);
                    };

                    InterServiceExecuteQuery(executeStringParams, newConn, newContext, null, queryCreateFailureAction, queryComplete, queryFail);
                });

                ActiveSimpleExecuteRequests.TryAdd(randomUri, workTask);
            }
            catch(Exception ex) 
            {
                requestContext.SendError(ex.ToString());
            }
        }

        /// <summary>
        /// Handles a request to get a subset of the results of this query
        /// </summary>
        internal async Task HandleResultSubsetRequest(SubsetParams subsetParams,
            RequestContext<SubsetResult> requestContext)
        {
            try
            {
                ResultSetSubset subset = await InterServiceResultSubset(subsetParams);
                var result = new SubsetResult
                {
                    ResultSubset = subset
                };
                requestContext.SendResult(result);
            }
            catch (Exception e)
            {
                // This was unexpected, so send back as error
                requestContext.SendError(e.Message);
            }
        }

         /// <summary>
        /// Handles a request to get an execution plan
        /// </summary>
        internal async Task HandleExecutionPlanRequest(QueryExecutionPlanParams planParams,
            RequestContext<QueryExecutionPlanResult> requestContext)
        {
            try
            {
                // Attempt to load the query
                Query query;
                if (!ActiveQueries.TryGetValue(planParams.OwnerUri, out query))
                {
                    requestContext.SendError(SR.QueryServiceRequestsNoQuery);
                    return;
                }

                // Retrieve the requested execution plan and return it
                var result = new QueryExecutionPlanResult
                {
                    ExecutionPlan = await query.GetExecutionPlan(planParams.BatchIndex, planParams.ResultSetIndex)
                };
                requestContext.SendResult(result);
            }
            catch (Exception e)
            {
                // This was unexpected, so send back as error
                requestContext.SendError(e.Message);
            }
        }

        /// <summary>
        /// Handles a request to dispose of this query
        /// </summary>
        internal void HandleDisposeRequest(QueryDisposeParams disposeParams,
            RequestContext<QueryDisposeResult> requestContext)
        {
            // Setup action for success and failure
            Action successAction = () => requestContext.SendResult(new QueryDisposeResult());
            Action<string> failureAction = message => requestContext.SendError(message);

            // Use the inter-service dispose functionality
            InterServiceDisposeQuery(disposeParams.OwnerUri, successAction, failureAction);
        }

        /// <summary>
        /// Handles a request to cancel this query if it is in progress
        /// </summary>
        internal void HandleCancelRequest(QueryCancelParams cancelParams,
            RequestContext<QueryCancelResult> requestContext)
        {
            try
            {
                // Attempt to find the query for the owner uri
                Query result;
                if (!ActiveQueries.TryGetValue(cancelParams.OwnerUri, out result))
                {
                    requestContext.SendResult(new QueryCancelResult
                    {
                        Messages = SR.QueryServiceRequestsNoQuery
                    });
                    return;
                }

                // Cancel the query and send a success message
                result.Cancel();
                requestContext.SendResult(new QueryCancelResult());
            }
            catch (InvalidOperationException e)
            {
                // If this exception occurred, we most likely were trying to cancel a completed query
                requestContext.SendResult(new QueryCancelResult
                {
                    Messages = e.Message
                });
            }
            catch (Exception e)
            {
                requestContext.SendError(e.Message);
            }
        }

        /// <summary>
        /// Process request to save a resultSet to a file in CSV format
        /// </summary>
        internal void HandleSaveResultsAsCsvRequest(SaveResultsAsCsvRequestParams saveParams,
            RequestContext<SaveResultRequestResult> requestContext)
        {
            // Use the default CSV file factory if we haven't overridden it
            IFileStreamFactory csvFactory = CsvFileFactory ?? new SaveAsCsvFileStreamFactory
            {
                SaveRequestParams = saveParams,
                QueryExecutionSettings = Settings.QueryExecutionSettings
            };
            SaveResultsHelper(saveParams, requestContext, csvFactory);
        }

        /// <summary>
        /// Process request to save a resultSet to a file in Excel format
        /// </summary>
        internal void HandleSaveResultsAsExcelRequest(SaveResultsAsExcelRequestParams saveParams,
            RequestContext<SaveResultRequestResult> requestContext)
        {
            // Use the default Excel file factory if we haven't overridden it
            IFileStreamFactory excelFactory = ExcelFileFactory ?? new SaveAsExcelFileStreamFactory
            {
                SaveRequestParams = saveParams,
                QueryExecutionSettings = Settings.QueryExecutionSettings
            };
            SaveResultsHelper(saveParams, requestContext, excelFactory);
        }

        /// <summary>
        /// Process request to save a resultSet to a file in JSON format
        /// </summary>
        internal void HandleSaveResultsAsJsonRequest(SaveResultsAsJsonRequestParams saveParams,
            RequestContext<SaveResultRequestResult> requestContext)
        {
            // Use the default JSON file factory if we haven't overridden it
            IFileStreamFactory jsonFactory = JsonFileFactory ?? new SaveAsJsonFileStreamFactory
            {
                SaveRequestParams = saveParams,
                QueryExecutionSettings = Settings.QueryExecutionSettings
            };
            SaveResultsHelper(saveParams, requestContext, jsonFactory);
        }

        #endregion

        #region Inter-Service API Handlers

        /// <summary>
        /// Query execution meant to be called from another service. Utilizes callbacks to allow
        /// custom actions to be taken upon creation of query and failure to create query.
        /// </summary>
        /// <param name="executeParams">Parameters for execution</param>
        /// <param name="connInfo">Connection Info to use; will try and get the connection from owneruri if not provided</param>
        /// <param name="queryEventSender">Event sender that will send progressive events during execution of the query</param>
        /// <param name="queryCreateSuccessFunc">
        /// Callback for when query has been created successfully. If result is <c>true</c>, query
        /// will be executed asynchronously. If result is <c>false</c>, query will be disposed. May
        /// be <c>null</c>
        /// </param>
        /// <param name="queryCreateFailFunc">
        /// Callback for when query failed to be created successfully. Error message is provided.
        /// May be <c>null</c>.
        /// </param>
        /// <param name="querySuccessFunc">
        /// Callback to call when query has completed execution successfully. May be <c>null</c>.
        /// </param>
        /// <param name="queryFailureFunc">
        /// Callback to call when query has completed execution with errors. May be <c>null</c>.
        /// </param>
        public void InterServiceExecuteQuery(ExecuteRequestParamsBase executeParams, 
            ConnectionInfo connInfo,
            IEventSender queryEventSender,
            Func<Query, bool> queryCreateSuccessFunc,
            Action<string> queryCreateFailFunc,
            Query.QueryEventHandler querySuccessFunc, 
            Query.QueryErrorEventHandler queryFailureFunc)
        {
            Validate.IsNotNull(nameof(executeParams), executeParams);
            Validate.IsNotNull(nameof(queryEventSender), queryEventSender);
            
            Query newQuery;
            try
            {
                // Get a new active query
                newQuery = CreateQuery(executeParams, connInfo);
                if (queryCreateSuccessFunc != null && !queryCreateSuccessFunc(newQuery))
                {
                    // The callback doesn't want us to continue, for some reason
                    // It's ok if we leave the query behind in the active query list, the next call
                    // to execute will replace it.
                    newQuery.Dispose();
                    return;
                }
            }
            catch (Exception e)
            {
                // Call the failure callback if it was provided
                queryCreateFailFunc?.Invoke(e.Message);
                return;
            }

            // Execute the query asynchronously
            ExecuteAndCompleteQuery(executeParams.OwnerUri, newQuery, queryEventSender, querySuccessFunc, queryFailureFunc);
        }

        /// <summary>
        /// Query disposal meant to be called from another service. Utilizes callbacks to allow
        /// custom actions to be performed on success or failure.
        /// </summary>
        /// <param name="ownerUri">The identifier of the query to be disposed</param>
        /// <param name="successAction">Action to perform on success</param>
        /// <param name="failureAction">Action to perform on failure</param>
        public void InterServiceDisposeQuery(string ownerUri, Action successAction,
            Action<string> failureAction)
        {
            Validate.IsNotNull(nameof(successAction), successAction);
            Validate.IsNotNull(nameof(failureAction), failureAction);

            try
            {
                // Attempt to remove the query for the owner uri
                Query result;
                if (!ActiveQueries.TryRemove(ownerUri, out result))
                {
                    failureAction(SR.QueryServiceRequestsNoQuery);
                    return;
                }

                // Cleanup the query
                result.Dispose();

                // Success
                successAction();
            }
            catch (Exception e)
            {
                failureAction(e.Message);
            }
        }

        /// <summary>
        /// Retrieves the requested subset of rows from the requested result set. Intended to be
        /// called by another service.
        /// </summary>
        /// <param name="subsetParams">Parameters for the subset to retrieve</param>
        /// <returns>The requested subset</returns>
        /// <exception cref="ArgumentOutOfRangeException">The requested query does not exist</exception>
        public async Task<ResultSetSubset> InterServiceResultSubset(SubsetParams subsetParams)
        {
            Validate.IsNotNullOrEmptyString(nameof(subsetParams.OwnerUri), subsetParams.OwnerUri);

            // Attempt to load the query
            Query query;
            if (!ActiveQueries.TryGetValue(subsetParams.OwnerUri, out query))
            {
                throw new ArgumentOutOfRangeException(SR.QueryServiceRequestsNoQuery);
            }

            // Retrieve the requested subset and return it
            return await query.GetSubset(subsetParams.BatchIndex, subsetParams.ResultSetIndex,
                subsetParams.RowsStartIndex, subsetParams.RowsCount);
        }

        #endregion

        #region Private Helpers

        private Query CreateQuery(ExecuteRequestParamsBase executeParams, ConnectionInfo connInfo)
        {
            // Attempt to get the connection for the editor
            ConnectionInfo connectionInfo;
            if (connInfo != null) {
                connectionInfo = connInfo;
            } else if (!ConnectionService.TryFindConnection(executeParams.OwnerUri, out connectionInfo))
            {
                throw new ArgumentOutOfRangeException(nameof(executeParams.OwnerUri), SR.QueryServiceQueryInvalidOwnerUri);
            }

            // Attempt to clean out any old query on the owner URI
            Query oldQuery;
            if (ActiveQueries.TryGetValue(executeParams.OwnerUri, out oldQuery) && oldQuery.HasExecuted)
            {
                oldQuery.Dispose();
                ActiveQueries.TryRemove(executeParams.OwnerUri, out oldQuery);
            }

            // Retrieve the current settings for executing the query with
            QueryExecutionSettings settings = Settings.QueryExecutionSettings;

            // Apply execution parameter settings 
            settings.ExecutionPlanOptions = executeParams.ExecutionPlanOptions;

            // If we can't add the query now, it's assumed the query is in progress
            Query newQuery = new Query(GetSqlText(executeParams), connectionInfo, settings, BufferFileFactory);
            if (!ActiveQueries.TryAdd(executeParams.OwnerUri, newQuery))
            {
                newQuery.Dispose();
                throw new InvalidOperationException(SR.QueryServiceQueryInProgress);
            }

            return newQuery;
        }

        private static void ExecuteAndCompleteQuery(string ownerUri, Query query,
            IEventSender eventSender,
            Query.QueryEventHandler querySuccessCallback,
            Query.QueryErrorEventHandler queryFailureCallback)
        {
            // Setup the callback to send the complete event
            Query.QueryEventHandler completeCallback = q =>
            {
                // Send back the results
                QueryCompleteParams eventParams = new QueryCompleteParams
                {
                    OwnerUri = ownerUri,
                    BatchSummaries = q.BatchSummaries
                };

                eventSender.SendEvent(QueryCompleteEvent.Type, eventParams);
            };

            // Setup the callback to send the complete event
            Query.QueryErrorEventHandler failureCallback = (q, e) =>
            {
                // Send back the results
                QueryCompleteParams eventParams = new QueryCompleteParams
                {
                    OwnerUri = ownerUri,
                    BatchSummaries = q.BatchSummaries
                };

                eventSender.SendEvent(QueryCompleteEvent.Type, eventParams);
            };
            query.QueryCompleted += completeCallback;
            query.QueryFailed += failureCallback;

            // Add the callbacks that were provided by the caller
            // If they're null, that's no problem
            query.QueryCompleted += querySuccessCallback;
            query.QueryFailed += queryFailureCallback;

            // Setup the batch callbacks
            Batch.BatchEventHandler batchStartCallback = b =>
            {
                BatchEventParams eventParams = new BatchEventParams
                {
                    BatchSummary = b.Summary,
                    OwnerUri = ownerUri
                };

                eventSender.SendEvent(BatchStartEvent.Type, eventParams);
            };
            query.BatchStarted += batchStartCallback;

            Batch.BatchEventHandler batchCompleteCallback = b =>
            {
                BatchEventParams eventParams = new BatchEventParams
                {
                    BatchSummary = b.Summary,
                    OwnerUri = ownerUri
                };

                eventSender.SendEvent(BatchCompleteEvent.Type, eventParams);
            };
            query.BatchCompleted += batchCompleteCallback;

            Batch.BatchMessageHandler batchMessageCallback = m =>
            {
                MessageParams eventParams = new MessageParams
                {
                    Message = m,
                    OwnerUri = ownerUri
                };
                eventSender.SendEvent(MessageEvent.Type, eventParams);
            };
            query.BatchMessageSent += batchMessageCallback;

            // Setup the ResultSet completion callback
            ResultSet.ResultSetEventHandler resultCallback = r =>
            {
                ResultSetEventParams eventParams = new ResultSetEventParams
                {
                    ResultSetSummary = r.Summary,
                    OwnerUri = ownerUri
                };
                eventSender.SendEvent(ResultSetCompleteEvent.Type, eventParams);
            };
            query.ResultSetCompleted += resultCallback;

            // Launch this as an asynchronous task
            query.Execute();
        }

        private void SaveResultsHelper(SaveResultsRequestParams saveParams,
            RequestContext<SaveResultRequestResult> requestContext, IFileStreamFactory fileFactory)
        {
            // retrieve query for OwnerUri
            Query query;
            if (!ActiveQueries.TryGetValue(saveParams.OwnerUri, out query))
            {
                requestContext.SendError(SR.QueryServiceQueryInvalidOwnerUri);
                return;
            }

            //Setup the callback for completion of the save task
            ResultSet.SaveAsEventHandler successHandler = parameters =>
            {
                requestContext.SendResult(new SaveResultRequestResult());
            };
            ResultSet.SaveAsFailureEventHandler errorHandler = (parameters, reason) =>
            {
                string message = SR.QueryServiceSaveAsFail(Path.GetFileName(parameters.FilePath), reason);
                requestContext.SendError(message);
            };

            try
            {
                // Launch the task
                query.SaveAs(saveParams, fileFactory, successHandler, errorHandler);
            }
            catch (Exception e)
            {
                errorHandler(saveParams, e.Message);
            }
        }

        // Internal for testing purposes
        internal string GetSqlText(ExecuteRequestParamsBase request)
        {
            // If it is a document selection, we'll retrieve the text from the document
            ExecuteDocumentSelectionParams docRequest = request as ExecuteDocumentSelectionParams;
            if (docRequest != null)
            {
                return GetSqlTextFromSelectionData(docRequest.OwnerUri, docRequest.QuerySelection);
            }

             // If it is a document statement, we'll retrieve the text from the document
            ExecuteDocumentStatementParams stmtRequest = request as ExecuteDocumentStatementParams;
            if (stmtRequest != null)
            {
                return GetSqlStatementAtPosition(stmtRequest.OwnerUri, stmtRequest.Line, stmtRequest.Column);
            }

            // If it is an ExecuteStringParams, return the text as is
            ExecuteStringParams stringRequest = request as ExecuteStringParams;
            if (stringRequest != null)
            {
                return stringRequest.Query;
            }

            // Note, this shouldn't be possible due to inheritance rules
            throw new InvalidCastException("Invalid request type");
        }

        /// <summary>
        /// Return portion of document corresponding to the selection range
        /// </summary>
        internal string GetSqlTextFromSelectionData(string ownerUri, SelectionData selection)
        {
            // Get the document from the parameters
            ScriptFile queryFile = WorkspaceService.Workspace.GetFile(ownerUri);
            if (queryFile == null)
            {
                return string.Empty;
            }
            // If a selection was not provided, use the entire document
            if (selection == null)
            {
                return queryFile.Contents;
            }

            // A selection was provided, so get the lines in the selected range
            string[] queryTextArray = queryFile.GetLinesInRange(
                new BufferRange(
                    new BufferPosition(
                        selection.StartLine + 1,
                        selection.StartColumn + 1
                    ),
                    new BufferPosition(
                        selection.EndLine + 1,
                        selection.EndColumn + 1
                    )
                )
            );
            return string.Join(Environment.NewLine, queryTextArray);
        }

        /// <summary>
        /// Return portion of document corresponding to the statement at the line and column
        /// </summary>
        internal string GetSqlStatementAtPosition(string ownerUri, int line, int column)
        {
            // Get the document from the parameters
            ScriptFile queryFile = WorkspaceService.Workspace.GetFile(ownerUri);
            if (queryFile == null)
            {
                return string.Empty;
            }

            return LanguageServices.LanguageService.Instance.ParseStatementAtPosition(
                queryFile.Contents, line, column);
        }

        /// Internal for testing purposes
        internal Task UpdateSettings(SqlToolsSettings newSettings, SqlToolsSettings oldSettings, EventContext eventContext)
        {
            Settings.QueryExecutionSettings.Update(newSettings.QueryExecutionSettings);
            return Task.FromResult(0);
        }

        #endregion

        #region IDisposable Implementation

        private bool disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (var query in ActiveQueries)
                {
                    if (!query.Value.HasExecuted)
                    {
                        try
                        {
                            query.Value.Cancel();
                        }
                        catch (Exception e)
                        {
                            // We don't particularly care if we fail to cancel during shutdown
                            string message = string.Format("Failed to cancel query {0} during query service disposal: {1}", query.Key, e);
                            Logger.Write(LogLevel.Warning, message);
                        }
                    }
                    query.Value.Dispose();
                }
                ActiveQueries.Clear();
            }

            disposed = true;
        }

        ~QueryExecutionService()
        {
            Dispose(false);
        }

        #endregion
    }
}
