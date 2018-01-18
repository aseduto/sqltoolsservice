//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Composition;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Extensibility;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.Capabilities.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.Capabilities
{
    [Export(typeof(IHostedService))]
    public class CapabilitiesService : HostedService<CapabilitiesService>
    {
        public CapabilitiesService()
        {
            FeaturesMetadata = new List<FeatureMetadataProvider>();
        }
        
        public AdminServicesProviderOptions AdminServicesProvider { get; set; }
        
        public ConnectionProviderOptions ConnectionProvider { get; set; }
        
        public List<FeatureMetadataProvider> FeaturesMetadata { get; }
        
        public override void InitializeService(IServiceHost serviceHost)
        {
            serviceHost.SetRequestHandler(ListCapabilitiesRequest.Type, HandleDmpCapabilitiesRequest);
        }

        internal void HandleDmpCapabilitiesRequest(
            ListCapabilitiesParams input,
            RequestContext<CapabilitiesResult> requestContext)
        {
            // Build the capabilities for output
            CapabilitiesResult result = new CapabilitiesResult
            {
                Capabilities = new DmpServerCapabilities
                {
                    ProtocolVersion = SqlToolsServiceProviderDetails.ProviderDetails.ProviderProtocolVersion,
                    ProviderName = SqlToolsServiceProviderDetails.ProviderDetails.ProviderName,
                    ProviderDisplayName = SqlToolsServiceProviderDetails.ProviderDetails.ProviderDescription,
                    
                    AdminServicesProvider = AdminServicesProvider,
                    ConnectionProvider = ConnectionProvider,
                    Features = FeaturesMetadata.ToArray()
                }
            };
            requestContext.SendResult(result);
        }
    }
}