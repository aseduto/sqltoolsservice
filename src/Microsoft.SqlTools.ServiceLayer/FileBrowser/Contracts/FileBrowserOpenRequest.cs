//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.FileBrowser.Contracts
{
    /// <summary>
    /// Parameters for opening file browser
    /// </summary>
    public class FileBrowserOpenParams
    {
        /// <summary>
        /// Connection uri
        /// </summary>
        public string OwnerUri { get; set; }

        /// <summary>
        /// The initial path to expand the nodes for (e.g. Backup will set this path to default backup folder)
        /// </summary>
        public string ExpandPath { get; set; }

        /// <summary>
        /// File extension filter (e.g. *.bak)
        /// </summary>
        public string[] FileFilters { get; set; }

        /// <summary>
        /// True if this is a request to change file filter
        /// </summary>
        public bool ChangeFilter { get; set; }
    }

    /// <summary>
    /// Request to open a file browser
    /// </summary>
    public static class FileBrowserOpenRequest
    {
        public static readonly
            RequestType<FileBrowserOpenParams, bool> Type =
            RequestType<FileBrowserOpenParams, bool>.Create("filebrowser/open");
    }
}
