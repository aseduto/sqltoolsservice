//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.Capabilities;
using Microsoft.SqlTools.ServiceLayer.Capabilities.Contracts;
using Microsoft.SqlTools.ServiceLayer.Test.Common.RequestContextMocking;
using Moq;
using Xunit;

namespace Microsoft.SqlTools.ServiceLayer.UnitTests.Capabilities
{
    public class CapabilitiesServiceTests
    {
        [Fact]
        public void ConstructionTest()
        {
            // If: I construct a new capabilities service
            var cs = new CapabilitiesService();
            
            // Then: The provider options should not be set
            Assert.Null(cs.AdminServicesProvider);
            Assert.Null(cs.ConnectionProvider);
            Assert.NotNull(cs.FeaturesMetadata);
            Assert.Empty(cs.FeaturesMetadata);
        }

        [Fact]
        public void InitializeService()
        {
            // Setup: Create mock service host
            var mockServiceHost = new Mock<IServiceHost>();
            mockServiceHost.Setup(sh => sh.SetRequestHandler(
                ListCapabilitiesRequest.Type,
                It.IsAny<Action<ListCapabilitiesParams, RequestContext<CapabilitiesResult>>>(),
                It.IsAny<bool>()
            ));
            
            // If: I initialize a capabilities service
            var cs = new CapabilitiesService();
            cs.InitializeService(mockServiceHost.Object);
            
            // Then: The request handler should have been called
            mockServiceHost.Verify(sh => sh.SetRequestHandler(
                ListCapabilitiesRequest.Type,
                It.IsAny<Action<ListCapabilitiesParams, RequestContext<CapabilitiesResult>>>(),
                It.IsAny<bool>()
            ), Times.Once);
        }

        [Fact]
        public void HandleDmpCapabilitiesRequest()
        {
            // Setup: Create capabilities service with some bogus capabilities
            var cs = new CapabilitiesService
            {
                AdminServicesProvider = new AdminServicesProviderOptions(),
                ConnectionProvider = new ConnectionProviderOptions()
            };
            cs.FeaturesMetadata.Add(new FeatureMetadataProvider());
            
            // If: I request the dmp capabilities
            // Then: The capabilities I set should be returned
            var efv = new EventFlowValidator<CapabilitiesResult>()
                .AddResultValidation(c =>
                {
                    Assert.NotNull(c.Capabilities);
                    
                    Assert.NotEmpty(c.Capabilities.ProtocolVersion);
                    Assert.NotEmpty(c.Capabilities.ProviderName);
                    Assert.NotEmpty(c.Capabilities.ProviderDisplayName);
                    
                    Assert.Equal(cs.AdminServicesProvider, c.Capabilities.AdminServicesProvider);
                    Assert.Equal(cs.ConnectionProvider, c.Capabilities.ConnectionProvider);
                    Assert.NotNull(c.Capabilities.Features);
                    Assert.Equal(1, c.Capabilities.Features.Length);
                    Assert.Equal(cs.FeaturesMetadata[0], c.Capabilities.Features[0]);
                })
                .Complete();
            var input = new ListCapabilitiesParams();
            cs.HandleDmpCapabilitiesRequest(input, efv.Object);
            efv.Validate();
        }
    }
}