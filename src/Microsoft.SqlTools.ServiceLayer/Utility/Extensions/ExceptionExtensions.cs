// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.SqlTools.ServiceLayer.Utility.Extensions
{
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Returns true if the passed exception or any inner exception is an OperationCanceledException instance.
        /// </summary>
        public static bool IsOperationCanceledException(this Exception e)
        {
            Exception current = e;
            while (current != null)
            {
                if (current is OperationCanceledException)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}