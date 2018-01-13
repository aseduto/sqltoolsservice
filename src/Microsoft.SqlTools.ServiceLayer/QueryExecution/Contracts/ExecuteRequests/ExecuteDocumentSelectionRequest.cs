//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.QueryExecution.Contracts.ExecuteRequests
{
    /// <summary>
    /// Parameters for executing a query from a document open in the workspace
    /// </summary>
    public class ExecuteDocumentSelectionParams : ExecuteRequestParamsBase
    {
        /// <summary>
        /// The selection from the document
        /// </summary>
        public SelectionData QuerySelection { get; set; }
    }

    public static class ExecuteDocumentSelectionRequest
    {
        public static readonly
            RequestType<ExecuteDocumentSelectionParams, ExecuteRequestResult> Type =
            RequestType<ExecuteDocumentSelectionParams, ExecuteRequestResult>.Create("query/executeDocumentSelection");
    }
}
