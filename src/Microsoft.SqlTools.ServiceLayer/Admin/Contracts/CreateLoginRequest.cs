//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.Admin.Contracts
{
    public class CreateLoginParams
    {
        public string OwnerUri { get; set; }

        public LoginInfo DatabaseInfo { get; set; }
    }

    public class CreateLoginResponse
    {
        public bool Result { get; set; }

        public int TaskId { get; set; }
    }

    public static class CreateLoginRequest
    {
        public static readonly
            RequestType<CreateLoginParams, CreateLoginResponse> Type =
                RequestType<CreateLoginParams, CreateLoginResponse>.Create("admin/createlogin");
    }
}
