//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.TaskServices.Contracts
{
    public class CancelTaskParams
    {
        /// <summary>
        /// An id to unify the task
        /// </summary>
        public string TaskId { get; set; }
    }

    public static class CancelTaskRequest
    {
        public static readonly
            RequestType<CancelTaskParams, bool> Type =
            RequestType<CancelTaskParams, bool>.Create("tasks/canceltask");
    }
}
