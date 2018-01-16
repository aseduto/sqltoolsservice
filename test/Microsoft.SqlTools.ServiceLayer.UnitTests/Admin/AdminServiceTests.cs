//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.ServiceLayer.Admin;
using Xunit;

namespace Microsoft.SqlTools.ServiceLayer.UnitTests.Admin
{
    /// <summary>
    /// Tests for AdminService Class
    /// </summary>
    public class AdminServiceTests
    {
        [Fact]
        public void TestBuildingSecureStringFromPassword()
        {
            string password = "test_password";
            var secureString = AdminService.BuildSecureStringFromPassword(password);
            Assert.Equal(password.Length, secureString.Length);
        }

        [Fact]
        public void TestBuildingSecureStringFromNullPassword()
        {
            string password = null;
            var secureString = AdminService.BuildSecureStringFromPassword(password);
            Assert.Equal(0, secureString.Length);
        }
    }
}
