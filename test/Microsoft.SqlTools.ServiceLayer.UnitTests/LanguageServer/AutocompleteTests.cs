//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Runtime.InteropServices;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.Connection.Contracts;
using Microsoft.SqlTools.ServiceLayer.LanguageServices;
using Microsoft.SqlTools.ServiceLayer.LanguageServices.Contracts;
using Microsoft.SqlTools.ServiceLayer.Test.Common.RequestContextMocking;
using Microsoft.SqlTools.ServiceLayer.Workspace.Contracts;
using Moq;
using Xunit;
using GlobalCommon = Microsoft.SqlTools.ServiceLayer.Test.Common;

namespace Microsoft.SqlTools.ServiceLayer.UnitTests.LanguageServer
{
    /// <summary>
    /// Tests for the language service autocomplete component
    /// </summary>
    public class AutocompleteTests : LanguageServiceTestBase<CompletionItem>
    {

        // TODO: Rewrite these tests when it is clear what their behavior is supposed to be (01/18/2018)
        #region Disabled Tests
        //[Fact]
        public void HandleCompletionRequestDisabled()
        {
            // TODO: What are these testing?
            InitializeTestObjects();
            langService.CurrentWorkspaceSettings.SqlTools.IntelliSense.EnableIntellisense = false;
            langService.HandleCompletionRequest(null, null);
        }

        //[Fact]
        public void HandleCompletionResolveRequestDisabled()
        {
            // TODO: What are these testing?
            InitializeTestObjects();
            langService.CurrentWorkspaceSettings.SqlTools.IntelliSense.EnableIntellisense = false;
            langService.HandleCompletionResolveRequest(null, null);
        }

        //[Fact]
        public void HandleSignatureHelpRequestDisabled()
        {
            // TODO: What are these testing?
            InitializeTestObjects();
            langService.CurrentWorkspaceSettings.SqlTools.IntelliSense.EnableIntellisense = false;
            langService.HandleSignatureHelpRequest(null, null);
        }
        #endregion

        [Fact]
        public void HandleSignatureHelpRequestNonMssqlFile()
        {
            InitializeTestObjects();

            // setup the mock for SendResult
            var signatureRequestContext = new EventFlowValidator<SignatureHelp>()
                .AddNullResultValidation()
                .Complete();

            langService.CurrentWorkspaceSettings.SqlTools.IntelliSense.EnableIntellisense = true;
            langService.HandleDidChangeLanguageFlavorNotification(new LanguageFlavorChangeParams {
                Uri = textDocument.TextDocument.Uri,
                Language = LanguageService.SQL_LANG.ToLower(),
                Flavor = "NotMSSQL"
            }, null);
            langService.HandleSignatureHelpRequest(textDocument, signatureRequestContext.Object);

            // Validate result
            signatureRequestContext.Validate();
        }               

        [Fact]
        public void AddOrUpdateScriptParseInfoNullUri()
        {
            InitializeTestObjects();
            langService.AddOrUpdateScriptParseInfo("abracadabra", scriptParseInfo);
            Assert.True(langService.ScriptParseInfoMap.ContainsKey("abracadabra"));
        }

        [Fact]
        public void GetDefinitionInvalidTextDocument()
        {
            InitializeTestObjects();
            textDocument.TextDocument.Uri = "invaliduri";
            Assert.Null(langService.GetDefinition(textDocument, null, null));
        }

        [Fact]
        public void RemoveScriptParseInfoNullUri()
        {
            InitializeTestObjects();
            Assert.False(langService.RemoveScriptParseInfo("abc123"));
        }

        [Fact]
        public void IsPreviewWindowNullScriptFileTest()
        {
            InitializeTestObjects();
            Assert.False(langService.IsPreviewWindow(null));
        }

        [Fact]
        public void GetCompletionItemsInvalidTextDocument()
        {
            InitializeTestObjects();
            textDocument.TextDocument.Uri = "somethinggoeshere";
            Assert.True(langService.GetCompletionItems(textDocument, scriptFile.Object, null).Length > 0);
        }

        [Fact]
        public void GetDiagnosticFromMarkerTest()
        {
            var scriptFileMarker = new ScriptFileMarker()
            {
                Message = "Message",
                Level = ScriptFileMarkerLevel.Error,
                ScriptRegion = new ScriptRegion()
                {
                    File = "file://nofile.sql",
                    StartLineNumber = 1,
                    StartColumnNumber = 1,
                    StartOffset = 0,
                    EndLineNumber = 1,
                    EndColumnNumber = 1,
                    EndOffset = 0
                }
            }; 
            var diagnostic = DiagnosticsHelper.GetDiagnosticFromMarker(scriptFileMarker);
            Assert.Equal(diagnostic.Message, scriptFileMarker.Message);
        }

        [Fact]
        public void MapDiagnosticSeverityTest()
        {
            var level = ScriptFileMarkerLevel.Error;
            Assert.Equal(DiagnosticsHelper.MapDiagnosticSeverity(level), DiagnosticSeverity.Error);
            level = ScriptFileMarkerLevel.Warning;
            Assert.Equal(DiagnosticsHelper.MapDiagnosticSeverity(level), DiagnosticSeverity.Warning);
            level = ScriptFileMarkerLevel.Information;
            Assert.Equal(DiagnosticsHelper.MapDiagnosticSeverity(level), DiagnosticSeverity.Information);
            level = (ScriptFileMarkerLevel)100;
            Assert.Equal(DiagnosticsHelper.MapDiagnosticSeverity(level), DiagnosticSeverity.Error);
        }

        /// <summary>
        /// Tests the primary completion list event handler
        /// </summary>
        [Fact]
        public void GetCompletionsHandlerTest()
        {
            InitializeTestObjects();
            var efv = new EventFlowValidator<CompletionItem[]>()
                .AddResultValidation(Assert.NotNull)
                .Complete();
            
            // request the completion list            
            langService.HandleCompletionRequest(textDocument, efv.Object);

            // verify that send result was called with a completion array
            efv.Validate();
        }
    }
}
