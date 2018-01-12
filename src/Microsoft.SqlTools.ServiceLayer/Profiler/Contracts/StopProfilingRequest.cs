//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.Profiler.Contracts
{
    /// <summary>
    /// Stop Profiling request parameters
    /// </summary>
    public class StopProfilingParams
    {
        public string SessionId { get; set; }
    }

    public class StopProfilingResult
    {
        public bool Succeeded { get; set; }

        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Start Profile request type
    /// </summary>
    public static class StopProfilingRequest
    {
        /// <summary>
        /// Request definition
        /// </summary>
        public static readonly
            RequestType<StopProfilingParams, StopProfilingResult> Type =
            RequestType<StopProfilingParams, StopProfilingResult>.Create("profiler/stop");
    }
}
