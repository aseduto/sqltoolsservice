//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.SqlTools.Dmp.Contracts;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Moq;

namespace Microsoft.SqlTools.ServiceLayer.Test.Common.RequestContextMocking
{
    public static class RequestContextMocks
    {

        public static Mock<RequestContext<TResponse>> Create<TResponse>(Action<TResponse> resultCallback)
        {
            var requestContext = new Mock<RequestContext<TResponse>>(null, null);

            // Setup the mock for SendResult
            var sendResultFlow = requestContext
                .Setup(rc => rc.SendResult(It.IsAny<TResponse>()));
            if (resultCallback != null)
            {
                sendResultFlow.Callback(resultCallback);
            }
            return requestContext;
        }

        public static Mock<RequestContext<TResponse>> AddEventHandling<TResponse, TParams>(
            this Mock<RequestContext<TResponse>> mock,
            EventType<TParams> expectedEvent,
            Action<EventType<TParams>, TParams> eventCallback)
        {
            var flow = mock.Setup(rc => rc.SendEvent(
                It.Is<EventType<TParams>>(m => m == expectedEvent),
                It.IsAny<TParams>()));
            if (eventCallback != null)
            {
                flow.Callback(eventCallback);
            }

            return mock;
        }

        public static Mock<RequestContext<TResponse>> AddErrorHandling<TResponse>(
            this Mock<RequestContext<TResponse>> mock,
            Action<string, int> errorCallback)
        {
            // Setup the mock for SendError
            var sendErrorFlow = mock.Setup(rc => rc.SendError(It.IsAny<string>(), It.IsAny<int>()));
            if (errorCallback != null)
            {
                sendErrorFlow.Callback<string, int>(errorCallback);
            }

            return mock;
        }
    }
}
