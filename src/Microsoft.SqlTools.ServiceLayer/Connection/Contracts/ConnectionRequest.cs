//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.Connection.Contracts
{
    /// <summary>
    /// Connect request mapping entry 
    /// </summary>
    public static class ConnectionRequest
    {
        public static readonly
            RequestType<ConnectParams, bool> Type =
            RequestType<ConnectParams, bool>.Create("connection/connect");
    }
}
