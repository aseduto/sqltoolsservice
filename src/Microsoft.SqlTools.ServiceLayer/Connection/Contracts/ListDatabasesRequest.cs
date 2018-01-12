//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.Connection.Contracts
{
    /// <summary>
    /// List databases request mapping entry 
    /// </summary>
    public static class ListDatabasesRequest
    {
        public static readonly
            RequestType<ListDatabasesParams, ListDatabasesResponse> Type =
            RequestType<ListDatabasesParams, ListDatabasesResponse>.Create("connection/listdatabases");
    }
}
