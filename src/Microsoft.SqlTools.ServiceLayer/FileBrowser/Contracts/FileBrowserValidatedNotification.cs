//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.FileBrowser.Contracts
{
    /// <summary>
    /// Event params for validation completion
    /// </summary>
    public class FileBrowserValidatedParams
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
    /// Notification for validation completion
    /// </summary>
    public static class FileBrowserValidatedNotification
    {
        public static readonly
            EventType<FileBrowserValidatedParams> Type =
            EventType<FileBrowserValidatedParams>.Create("filebrowser/validatecomplete");
    }

}
