//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.Capabilities.Contracts
{

    public class ListCapabilitiesParams
    {
        public string HostName { get; set; }
        
        public string HostVersion { get; set; }
    }

    public class CapabilitiesResult
    {
        public DmpServerCapabilities Capabilities { get; set; }
    }
    
    /// <summary>
    /// Defines a message that is sent from the client to request
    /// the version of the server.
    /// </summary>
    public static class ListCapabilitiesRequest
    {
        public static readonly
            RequestType<ListCapabilitiesParams, CapabilitiesResult> Type =
            RequestType<ListCapabilitiesParams, CapabilitiesResult>.Create("capabilities/list");
    }
}
