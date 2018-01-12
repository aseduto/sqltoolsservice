//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.FileBrowser.Contracts
{
    /// <summary>
    /// Event params for opening a file browser
    /// Returns full directory structure on the server side
    /// </summary>
    public class FileBrowserOpenedParams
    {
        /// <summary>
        /// Connection uri
        /// </summary>
        public string OwnerUri { get; set; }

        /// <summary>
        /// Entire file/folder tree 
        /// </summary>
        public FileTree FileTree { get; set; }

        /// <summary>
        /// Result of the operation
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Notification for completing file browser opening
    /// </summary>
    public static class FileBrowserOpenedNotification
    {
        public static readonly
            EventType<FileBrowserOpenedParams> Type =
            EventType<FileBrowserOpenedParams>.Create("filebrowser/opencomplete");
    }

}
