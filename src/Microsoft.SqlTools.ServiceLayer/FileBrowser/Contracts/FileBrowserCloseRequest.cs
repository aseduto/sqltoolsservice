//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.FileBrowser.Contracts
{
    /// <summary>
    /// Parameters to pass to close file browser
    /// </summary>
    public class FileBrowserCloseParams
    {
        /// <summary>
        /// Connection uri
        /// </summary>
        public string OwnerUri { get; set; }
    }

    /// <summary>
    /// Response for closing the browser
    /// </summary>
    public class FileBrowserCloseResponse
    {
        /// <summary>
        /// Result of the operation
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Error message if any
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Requst to close the file browser
    /// </summary>
    public static class FileBrowserCloseRequest
    {
        public static readonly
            RequestType<FileBrowserCloseParams, FileBrowserCloseResponse> Type =
            RequestType<FileBrowserCloseParams, FileBrowserCloseResponse>.Create("filebrowser/close");
    }

    /// <summary>
    /// Notification for close completion
    /// </summary>
    public static class FileBrowserClosedNotification
    {
        public static readonly
            EventType<FileBrowserCloseResponse> Type =
            EventType<FileBrowserCloseResponse>.Create("filebrowser/closecomplete");
    }
}
