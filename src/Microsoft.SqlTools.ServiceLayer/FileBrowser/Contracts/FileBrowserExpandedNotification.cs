//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.FileBrowser.Contracts
{
    /// <summary>
    /// Event params for expanding a node
    /// </summary>
    public class FileBrowserExpandedParams
    {
        /// <summary>
        /// Connection uri
        /// </summary>
        public string OwnerUri { get; set; }

        /// <summary>
        /// Expand path
        /// </summary>
        public string ExpandPath { get; set; }

        /// <summary>
        /// Children nodes
        /// </summary>
        public FileTreeNode[] Children { get; set; }

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
    /// Notification for expand completion
    /// </summary>
    public static class FileBrowserExpandedNotification
    {
        public static readonly
            EventType<FileBrowserExpandedParams> Type =
            EventType<FileBrowserExpandedParams>.Create("filebrowser/expandcomplete");
    }

}