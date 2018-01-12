﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;
using Microsoft.SqlTools.ServiceLayer.Admin.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.DisasterRecovery.Contracts
{
    /// <summary>
    /// Response class which returns backup configuration information
    /// </summary>
    public class BackupConfigInfoResponse
    {
        public BackupConfigInfo BackupConfigInfo { get; set; }
    }

    /// <summary>
    /// Request class to get backup configuration information
    /// </summary>
    public static class BackupConfigInfoRequest
    {
        public static readonly
            RequestType<DefaultDatabaseInfoParams, BackupConfigInfoResponse> Type =
                RequestType<DefaultDatabaseInfoParams, BackupConfigInfoResponse>.Create("disasterrecovery/backupconfiginfo");
    }
}
