// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.SqlTools.ServiceLayer.Utility.Extensions
{
    public static class NullableExtensions
    {
        /// <summary>
        /// Extension method to evaluate a bool? and determine if it has the value and is true.
        /// This way we avoid throwing if the bool? doesn't have a value.
        /// </summary>
        /// <param name="obj">The <c>bool?</c> to process</param>
        /// <returns>
        /// <c>true</c> if <paramref name="obj"/> has a value and it is <c>true</c>
        /// <c>false</c> otherwise.
        /// </returns>
        public static bool HasTrue(this bool? obj)
        {
            return obj.HasValue && obj.Value;
        }
    }
}