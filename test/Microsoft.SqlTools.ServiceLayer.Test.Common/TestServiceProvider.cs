//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.SqlTools.Dmp.Contracts.Hosting;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Channels;
using Microsoft.SqlTools.Dmp.Hosting.Extensibility;
using Microsoft.SqlTools.ServiceLayer.Admin;
using Microsoft.SqlTools.ServiceLayer.Connection;
using Microsoft.SqlTools.ServiceLayer.Connection.Contracts;
using Microsoft.SqlTools.ServiceLayer.DisasterRecovery;
using Microsoft.SqlTools.ServiceLayer.EditData;
using Microsoft.SqlTools.ServiceLayer.FileBrowser;
using Microsoft.SqlTools.ServiceLayer.Metadata;
using Microsoft.SqlTools.ServiceLayer.ObjectExplorer;
using Microsoft.SqlTools.ServiceLayer.Profiler;
using Microsoft.SqlTools.ServiceLayer.QueryExecution;
using Microsoft.SqlTools.ServiceLayer.Scripting;
using Microsoft.SqlTools.ServiceLayer.SqlContext;
using Microsoft.SqlTools.ServiceLayer.Workspace;
using Xunit;

namespace Microsoft.SqlTools.ServiceLayer.Test.Common
{
    /// <summary>
    /// Class to provide SQL tools service classes
    /// </summary>
    public class TestServiceProvider
    {
        private TestServiceProvider()
        {
            InitializeTestServices();
        }

        private static object _lockObject = new object();
        private static TestServiceProvider _instance = new TestServiceProvider();

        private static IMultiServiceProvider serviceProvider;

        public static TestServiceProvider Instance
        {
            get
            {
                return _instance;
            }
        }

        public ConnectionService ConnectionService
        {
            get
            {
                return ConnectionService.Instance;
            }
        }

        public ObjectExplorerService ObjectExplorerService
        {
            get
            {
                return serviceProvider.GetService<ObjectExplorerService>();
            }
        }

        public TestConnectionProfileService ConnectionProfileService
        {
            get
            {
                return TestConnectionProfileService.Instance;
            }
        }

        public WorkspaceService<SqlToolsSettings> WorkspaceService
        {
            get
            {
                return WorkspaceService<SqlToolsSettings>.Instance;
            }
        }

        /// <summary>
        /// Runs a query by calling the services directly (not using the test driver) 
        /// </summary>
        public void RunQuery(TestServerType serverType, string databaseName, string queryText, bool throwOnError = false)
        {
            RunQueryAsync(serverType, databaseName, queryText, throwOnError).Wait();

        }

        /// <summary>
        /// Runs a query by calling the services directly (not using the test driver) 
        /// </summary>
        public async Task RunQueryAsync(TestServerType serverType, string databaseName, string queryText, bool throwOnError = false)
        {
            string uri = "";
            using (SelfCleaningTempFile queryTempFile = new SelfCleaningTempFile())
            {
                uri = queryTempFile.FilePath;

                ConnectionInfo connInfo = InitLiveConnectionInfo(serverType, databaseName, uri);
                Query query = new Query(queryText, connInfo, new QueryExecutionSettings(), MemoryFileSystem.GetFileStreamFactory());
                query.Execute();
                await query.ExecutionTask;

                if (throwOnError)
                {
                    IEnumerable<Batch> errorBatches = query.Batches.Where(b => b.HasError);
                    if (errorBatches.Count() > 0)
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                "The query encountered and error. The batches with errors: {0}",
                                string.Join(Environment.NewLine, errorBatches.Select(b => b.BatchText))));
                    }
                }
                DisconnectConnection(uri);
            }

        }

        public static async Task<T> CalculateRunTime<T>(Func<Task<T>> testToRun, bool printResult, [CallerMemberName] string testName = "")
        {
            TestTimer timer = new TestTimer() { PrintResult = printResult };
            T result = await testToRun();
            timer.EndAndPrint(testName);

            return result;
        }

        private ConnectionInfo InitLiveConnectionInfo(TestServerType serverType, string databaseName, string scriptFilePath)
        {
            ConnectParams connectParams = ConnectionProfileService.GetConnectionParameters(serverType, databaseName);

            string ownerUri = scriptFilePath;
            var connectionService = ConnectionService.Instance;
            var connectionResult = connectionService.Connect(new ConnectParams()
                {
                    OwnerUri = ownerUri,
                    Connection = connectParams.Connection
                });

            connectionResult.Wait();

            ConnectionInfo connInfo = null;
            connectionService.TryFindConnection(ownerUri, out connInfo);
            Assert.NotNull(connInfo);
            return connInfo;
        }

        private void DisconnectConnection(string uri)
        {
            ConnectionService.Instance.Disconnect(new DisconnectParams
            {
                OwnerUri = uri,
                Type = ConnectionType.Default
            });
        }

        private static bool hasInitServices = false;

        private static void InitializeTestServices()
        {
            if (TestServiceProvider.hasInitServices)
            {
                return;
            }

            lock (_lockObject)
            {
                if (TestServiceProvider.hasInitServices)
                {
                    return;
                }
                TestServiceProvider.hasInitServices = true;

                const string hostName = "SQL Tools Test Service Host";
                const string hostProfileId = "SQLToolsTestService";
                Version hostVersion = new Version(1, 0);

                // set up the host details and profile paths 
                var hostDetails = new HostDetails(hostName, hostProfileId, hostVersion);

                // Grab the instance of the service host
                // TODO: We should consider rethinking this entire setup
                string directory = Path.GetDirectoryName(typeof(TestServiceProvider).Assembly.Location);
                string[] inclusionList = {"microsoftsqltoolsservicelayer.dll"};
                ExtensionServiceProvider provider = ExtensionServiceProvider.CreateFromAssembliesInDirectory(directory, inclusionList);
                provider.RegisterSingleService(AdminService.Instance);
                provider.RegisterSingleService(ConnectionService.Instance);
                provider.RegisterSingleService(DisasterRecoveryService.Instance);
                provider.RegisterSingleService(EditDataService.Instance);
                provider.RegisterSingleService(FileBrowserService.Instance);
                provider.RegisterSingleService(MetadataService.Instance);
                provider.RegisterSingleService(ProfilerService.Instance);
                provider.RegisterSingleService(QueryExecutionService.Instance);
                provider.RegisterSingleService(ScriptingService.Instance);
                provider.RegisterSingleService(WorkspaceService<SqlToolsSettings>.Instance);

                // Create the service host
                LanguageServiceCapabilities capabilities = new LanguageServiceCapabilities
                {
                    TextDocumentSync = TextDocumentSyncKind.Incremental,
                    DefinitionProvider = true,
                    ReferencesProvider = false,
                    DocumentFormattingProvider = true,
                    DocumentRangeFormattingProvider = true,
                    DocumentHighlightProvider = true,
                    HoverProvider = true,
                    CompletionProvider = new CompletionOptions
                    {
                        ResolveProvider = true,
                        TriggerCharacters = new[] { ".", "-", ":", "\\", "[", "\"" }
                    },
                    SignatureHelpProvider = new SignatureHelpOptions
                    {
                        TriggerCharacters = new[] {" ", ","}
                    }
                };
                ExtensibleServiceHost serviceHost = new ExtensibleServiceHost(provider, new StdioServerChannel(), 
                    SqlToolsServiceProviderDetails.ProviderDetails, capabilities);

                // Initialize the old singleton services (workspace service must go first)
                WorkspaceService<SqlToolsSettings>.Instance.InitializeService(serviceHost);
                
                AdminService.Instance.InitializeService(serviceHost, provider);
                ConnectionService.Instance.InitializeService(serviceHost, provider);
                DisasterRecoveryService.Instance.InitializeService(serviceHost, provider);
                EditDataService.Instance.InitializeService(serviceHost);
                FileBrowserService.Instance.InitializeService(serviceHost);
                MetadataService.Instance.InitializeService(serviceHost);
                ProfilerService.Instance.InitializeService(serviceHost);
                QueryExecutionService.Instance.InitializeService(serviceHost, provider);
                ScriptingService.Instance.InitializeService(serviceHost);

                serviceProvider = provider;
            }
        }

    }
}
