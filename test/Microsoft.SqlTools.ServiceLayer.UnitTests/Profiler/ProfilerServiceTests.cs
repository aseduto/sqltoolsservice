//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.Connection;
using Microsoft.SqlTools.ServiceLayer.Profiler;
using Microsoft.SqlTools.ServiceLayer.Profiler.Contracts;
using Microsoft.SqlTools.ServiceLayer.UnitTests.Utility;
using Moq;
using Xunit;

namespace Microsoft.SqlTools.ServiceLayer.UnitTests.Profiler
{
    /// <summary>
    /// Unit tests for ProfilerService
    /// </summary>
    public class ProfilerServiceTests
    {   
        /// <summary>
        /// Test starting a profiling session and receiving event callback
        /// </summary>
        /// <returns></returns>
        // TODO: Fix flaky test. See https://github.com/Microsoft/sqltoolsservice/issues/459
        //[Fact]
        public void TestStartProfilingRequest()
        {
            string sessionId = null;
            string testUri = "profiler_uri";
            var requestContext = new Mock<RequestContext<StartProfilingResult>>();
            requestContext.Setup(rc => rc.SendResult(It.IsAny<StartProfilingResult>()))
                .Callback<StartProfilingResult>((result) => 
                {
                    // capture the session id for sending the stop message
                    sessionId = result.SessionId;
                });

            var sessionListener = new TestSessionListener();

            var profilerService = new ProfilerService();
            profilerService.SessionMonitor.AddSessionListener(sessionListener);
            profilerService.ConnectionServiceInstance = TestObjects.GetTestConnectionService();
            ConnectionInfo connectionInfo = TestObjects.GetTestConnectionInfo();
            profilerService.ConnectionServiceInstance.OwnerToConnectionMap.Add(testUri, connectionInfo);
            profilerService.XEventSessionFactory = new TestXEventSessionFactory();

            var requestParams = new StartProfilingParams();
            requestParams.OwnerUri = testUri;
            requestParams.TemplateName = "Standard";

            profilerService.HandleStartProfilingRequest(requestParams, requestContext.Object);

            // wait a bit for profile sessions to be polled
            Thread.Sleep(500);

            requestContext.VerifyAll();

            Assert.Equal(sessionListener.PreviousSessionId, sessionId);
            Assert.Equal(sessionListener.PreviousEvents.Count, 1);
        }
    }
}
