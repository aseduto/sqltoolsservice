//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Contracts;
using Moq;

namespace Microsoft.SqlTools.ServiceLayer.UnitTests.Utility
{
    public static class ProtocolEndpointMocks
    {
        public static Mock<IServiceHost> AddEventHandling<TParams>(
            this Mock<IServiceHost> mock,
            EventType<TParams> expectedEvent,
            Action<EventType<TParams>, TParams> eventCallback)
        {
            var flow = mock.Setup(h => h.SendEvent(
                It.Is<EventType<TParams>>(m => m == expectedEvent),
                It.IsAny<TParams>()));
            if (eventCallback != null)
            {
                flow.Callback(eventCallback);
            }

            return mock;
        }
    }
}
