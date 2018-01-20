//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.FileBrowser;
using Microsoft.SqlTools.ServiceLayer.FileBrowser.Contracts;
using Microsoft.SqlTools.ServiceLayer.IntegrationTests.Utility;
using Microsoft.SqlTools.ServiceLayer.Test.Common.RequestContextMocking;
using Moq;
using Xunit;

namespace Microsoft.SqlTools.ServiceLayer.IntegrationTests.FileBrowser
{
    /// <summary>
    /// File browser service tests
    /// </summary>
    public class FileBrowserServiceTests
    {
        #region Request handle tests

        [Fact]
        public void HandleFileBrowserOpenRequestTest()
        {
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo();
            FileBrowserService service = new FileBrowserService();
            var efv = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();

            var openParams = new FileBrowserOpenParams
            {
                OwnerUri = liveConnection.ConnectionInfo.OwnerUri,
                ExpandPath = "",
                FileFilters = new string[1] {"*"}
            };

            service.HandleFileBrowserOpenRequest(openParams, efv.Object);
            efv.Validate();
        }

        [Fact]
        public void HandleFileBrowserExpandRequestTest()
        {
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo();
            FileBrowserService service = new FileBrowserService();
            var efv = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();

            var inputParams = new FileBrowserExpandParams
            {
                OwnerUri = liveConnection.ConnectionInfo.OwnerUri,
                ExpandPath = ""
            };

            service.HandleFileBrowserExpandRequest(inputParams, efv.Object);
            efv.Validate();
        }

        [Fact]
        public void HandleFileBrowserValidateRequestTest()
        {
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo();
            FileBrowserService service = new FileBrowserService();

            var efv = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();

            var inputParams = new FileBrowserValidateParams
            {
                OwnerUri = liveConnection.ConnectionInfo.OwnerUri,
                ServiceType = ""
            };

            service.HandleFileBrowserValidateRequest(inputParams, efv.Object);
            efv.Validate();
        }

        [Fact]
        public void HandleFileBrowserCloseRequestTest()
        {
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo();
            FileBrowserService service = new FileBrowserService();
            var efv = new EventFlowValidator<FileBrowserCloseResponse>()
                .AddResultValidation(r => Assert.True(r.Succeeded))
                .Complete();
            
            var inputParams = new FileBrowserCloseParams
            {
                OwnerUri = liveConnection.ConnectionInfo.OwnerUri
            };
            service.HandleFileBrowserCloseRequest(inputParams, efv.Object);
            efv.Validate();            
        }

        #endregion

        [Fact]
        public void OpenFileBrowserTest()
        {
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo();
            FileBrowserService service = new FileBrowserService();

            var openParams = new FileBrowserOpenParams
            {
                OwnerUri = liveConnection.ConnectionInfo.OwnerUri,
                ExpandPath = "",
                FileFilters = new[] { "*" }
            };

            var efv = new EventFlowValidator<bool>()
                .AddEventValidation(FileBrowserOpenedNotification.Type, eventParams =>
                {
                    Assert.True(eventParams.Succeeded);
                    Assert.NotNull(eventParams.FileTree);
                    Assert.NotNull(eventParams.FileTree.RootNode);
                    Assert.NotNull(eventParams.FileTree.RootNode.Children);
                    Assert.True(eventParams.FileTree.RootNode.Children.Count > 0);
                })
                .Complete();
            service.RunFileBrowserOpenTask(openParams, efv.Object);
            efv.Validate();
        }

        [Fact]
        public void ValidateSelectedFilesWithNullValidatorTest()
        {
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo();
            FileBrowserService service = new FileBrowserService();

            var validateParams = new FileBrowserValidateParams
            {
                // Do not pass any service so that the file validator will be null
                ServiceType = "",
                OwnerUri = liveConnection.ConnectionInfo.OwnerUri,
                SelectedFiles = new[] { "" }
            };

            var efv = new EventFlowValidator<bool>()
                .AddEventValidation(FileBrowserValidatedNotification.Type, eventParams => Assert.True(eventParams.Succeeded))
                .Complete();

            // Validate files with null file validator
            service.RunFileBrowserValidateTask(validateParams, efv.Object);
            efv.Validate();
        }

        [Fact]
        public void InvalidFileValidationTest()
        {
            FileBrowserService service = new FileBrowserService();
            service.RegisterValidatePathsCallback("TestService", ValidatePaths);

            var validateParams = new FileBrowserValidateParams
            {
                // Do not pass any service so that the file validator will be null
                ServiceType = "TestService",
                SelectedFiles = new[] { "" }
            };

            var efv = new EventFlowValidator<bool>()
                .AddEventValidation(FileBrowserValidatedNotification.Type, eventParams => Assert.False(eventParams.Succeeded))
                .Complete();

            // Validate files with null file validator
            service.RunFileBrowserValidateTask(validateParams, efv.Object);

            // Verify complete notification event was fired and the result
            efv.Validate();
        }

        #region private methods

        private static bool ValidatePaths(FileBrowserValidateEventArgs eventArgs, out string message)
        {
            message = string.Empty;
            return false;
        }

        #endregion
    }
}
