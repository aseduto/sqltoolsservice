//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.SqlTools.Dmp.Contracts;
using Microsoft.SqlTools.Dmp.Contracts.Hosting;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Channels;
using Microsoft.SqlTools.Dmp.Hosting.Extensibility;
using Microsoft.SqlTools.ServiceLayer.Utility;
using Microsoft.SqlTools.Dmp.Hosting.Utility;
using Microsoft.SqlTools.ServiceLayer.Admin;
using Microsoft.SqlTools.ServiceLayer.Connection;
using Microsoft.SqlTools.ServiceLayer.DisasterRecovery;
using Microsoft.SqlTools.ServiceLayer.EditData;
using Microsoft.SqlTools.ServiceLayer.FileBrowser;
using Microsoft.SqlTools.ServiceLayer.Metadata;
using Microsoft.SqlTools.ServiceLayer.Profiler;
using Microsoft.SqlTools.ServiceLayer.QueryExecution;
using Microsoft.SqlTools.ServiceLayer.Scripting;
using Microsoft.SqlTools.ServiceLayer.SqlContext;
using Microsoft.SqlTools.ServiceLayer.Workspace;

namespace Microsoft.SqlTools.ServiceLayer
{
    /// <summary>
    /// Main application class for SQL Tools API Service Host executable
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Main entry point into the SQL Tools API Service Host
        /// </summary>
        internal static void Main(string[] args)
        {
            try
            {
                // read command-line arguments
                ServiceLayerCommandOptions commandOptions = new ServiceLayerCommandOptions(args);
                if (commandOptions.ShouldExit)
                {
                    return;
                }

                string logFilePath = "sqltools";
                if (!string.IsNullOrWhiteSpace(commandOptions.LoggingDirectory))
                {
                    logFilePath = Path.Combine(commandOptions.LoggingDirectory, logFilePath);
                }

                // turn on Verbose logging during early development
                // TODO: Switch to Normal when preparing for public preview
                Logger.Initialize(logFilePath: logFilePath, minimumLogLevel: LogLevel.Verbose, isEnabled: commandOptions.EnableLogging);
                Logger.Write(LogLevel.Normal, "Starting SQL Tools Service Host");

                // Setup the service provider to have all our services
                string[] inclusionList = {"microsoftsqltoolsservicelayer.dll"};
                ExtensionServiceProvider serviceProvider = ExtensionServiceProvider.CreateFromAssembliesInDirectory(inclusionList);
                
                // Add all the old singleton services
                serviceProvider.RegisterSingleService(AdminService.Instance);
                serviceProvider.RegisterSingleService(ConnectionService.Instance);
                serviceProvider.RegisterSingleService(DisasterRecoveryService.Instance);
                serviceProvider.RegisterSingleService(EditDataService.Instance);
                serviceProvider.RegisterSingleService(FileBrowserService.Instance);
                serviceProvider.RegisterSingleService(MetadataService.Instance);
                serviceProvider.RegisterSingleService(ProfilerService.Instance);
                serviceProvider.RegisterSingleService(QueryExecutionService.Instance);
                serviceProvider.RegisterSingleService(ScriptingService.Instance);
                serviceProvider.RegisterSingleService(WorkspaceService<SqlToolsSettings>.Instance);

                // Create the service host
                ProviderDetails details = new ProviderDetails {ProviderProtocolVersion = "1.0"};
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
                ExtensibleServiceHost serviceHost = new ExtensibleServiceHost(serviceProvider, new StdioServerChannel(), details, capabilities);

                // Initialize the old singleton services (workspace service must go first)
                WorkspaceService<SqlToolsSettings>.Instance.InitializeService(serviceHost);
                
                AdminService.Instance.InitializeService(serviceHost);
                ConnectionService.Instance.InitializeService(serviceHost);
                DisasterRecoveryService.Instance.InitializeService(serviceHost);
                EditDataService.Instance.InitializeService(serviceHost);
                FileBrowserService.Instance.InitializeService(serviceHost);
                MetadataService.Instance.InitializeService(serviceHost);
                ProfilerService.Instance.InitializeService(serviceHost);
                QueryExecutionService.Instance.InitializeService(serviceHost);
                ScriptingService.Instance.InitializeService(serviceHost);
                
                // Start the service and wait for graceful exit
                serviceHost.Start();
                serviceHost.WaitForExit();
            }
            catch (Exception e)
            {
                Logger.Write(LogLevel.Error, string.Format("An unhandled exception occurred: {0}", e));
                Environment.Exit(1);
            }
        }
    }
}
