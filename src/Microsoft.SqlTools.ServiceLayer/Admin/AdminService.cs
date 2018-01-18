//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlTools.Dmp.Hosting;
using Microsoft.SqlTools.Dmp.Hosting.Extensibility;
using Microsoft.SqlTools.Dmp.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.Admin.Contracts;
using Microsoft.SqlTools.ServiceLayer.Capabilities;
using Microsoft.SqlTools.ServiceLayer.Connection;

namespace Microsoft.SqlTools.ServiceLayer.Admin
{
    /// <summary>
    /// Admin task service class
    /// </summary>
    public class AdminService
    {
        private static readonly Lazy<AdminService> instance = new Lazy<AdminService>(() => new AdminService());

        private static ConnectionService connectionService = null;

        private static readonly ConcurrentDictionary<string, DatabaseTaskHelper> serverTaskHelperMap =
            new ConcurrentDictionary<string, DatabaseTaskHelper>();
        
        /// <summary>
        /// Default, parameterless constructor.
        /// </summary>
        internal AdminService()
        {
        }

        /// <summary>
        /// Internal for testing purposes only
        /// </summary>
        internal static ConnectionService ConnectionServiceInstance
        {
            get
            {
                if (AdminService.connectionService == null)
                {
                    AdminService.connectionService = ConnectionService.Instance;
                }
                return AdminService.connectionService;
            }

            set
            {
                AdminService.connectionService = value;
            }
        }

        /// <summary>
        /// Gets the singleton instance object
        /// </summary>
        public static AdminService Instance
        {
            get { return instance.Value; }
        }

        /// <summary>
        /// Initializes the service instance
        /// </summary>
        public void InitializeService(IServiceHost serviceHost, IMultiServiceProvider serviceProvider)
        {
            serviceHost.SetRequestHandler(CreateDatabaseRequest.Type, HandleCreateDatabaseRequest);
            serviceHost.SetRequestHandler(CreateLoginRequest.Type, HandleCreateLoginRequest);
            serviceHost.SetRequestHandler(DefaultDatabaseInfoRequest.Type, HandleDefaultDatabaseInfoRequest);
            serviceHost.SetRequestHandler(GetDatabaseInfoRequest.Type, HandleGetDatabaseInfoRequest);

            CapabilitiesService capabilitiesService = serviceProvider.GetService<CapabilitiesService>();
            if (capabilitiesService != null)
            {
                capabilitiesService.AdminServicesProvider = AdminServicesProviderOptionsHelper.BuildAdminServicesProviderOptions();
            }
        }

        /// <summary>
        /// Handle a request for the default database prototype info
        /// </summary>
        public static void HandleDefaultDatabaseInfoRequest(
            DefaultDatabaseInfoParams optionsParams,
            RequestContext<DefaultDatabaseInfoResponse> requestContext)
        {
            try
            {
                var response = new DefaultDatabaseInfoResponse();
                ConnectionInfo connInfo;
                AdminService.ConnectionServiceInstance.TryFindConnection(
                    optionsParams.OwnerUri,
                    out connInfo);

                using (var taskHelper = CreateDatabaseTaskHelper(connInfo))
                {
                    response.DefaultDatabaseInfo = DatabaseTaskHelper.DatabasePrototypeToDatabaseInfo(taskHelper.Prototype);
                    requestContext.SendResult(response);
                }
            }
            catch (Exception ex)
            {
                requestContext.SendError(ex.ToString());
            }                
        }

        /// <summary>
        /// Handles a create database request
        /// </summary>
        internal static void HandleCreateDatabaseRequest(
            CreateDatabaseParams databaseParams,
            RequestContext<CreateDatabaseResponse> requestContext)
        {
            try
            {        
                ConnectionInfo connInfo;
                AdminService.ConnectionServiceInstance.TryFindConnection(
                    databaseParams.OwnerUri,
                    out connInfo);

                using (var taskHelper = CreateDatabaseTaskHelper(connInfo))
                {
                    DatabasePrototype prototype = taskHelper.Prototype;
                    DatabaseTaskHelper.ApplyToPrototype(databaseParams.DatabaseInfo, taskHelper.Prototype);

                    Database db = prototype.ApplyChanges();

                    requestContext.SendResult(new CreateDatabaseResponse()
                    {
                        Result = true,
                        TaskId = 0
                    });
                }
            }
            catch (Exception ex)
            {
                requestContext.SendError(ex.ToString());
            }         
        }

        /// <summary>
        /// Handle get database info request
        /// </summary>
        internal static void HandleGetDatabaseInfoRequest(
            GetDatabaseInfoParams databaseParams,
            RequestContext<GetDatabaseInfoResponse> requestContext)
        {
            try
            {
                Task task = Task.Run(() =>
                {
                    ConnectionInfo connInfo;
                    AdminService.ConnectionServiceInstance.TryFindConnection(
                            databaseParams.OwnerUri,
                            out connInfo);
                    DatabaseInfo info = null;

                    if (connInfo != null)
                    {
                        info = GetDatabaseInfo(connInfo);
                    }

                    requestContext.SendResult(new GetDatabaseInfoResponse
                    {
                        DatabaseInfo = info
                    });
                }).ContinueWith(t => {requestContext.SendError(t.Exception.ToString());});
            }
            catch (Exception ex)
            {
                requestContext.SendError(ex.ToString());
            }
        }
        
        /// <summary>
        /// Return database info for a specific database
        /// </summary>
        /// <param name="connInfo"></param>
        /// <returns></returns>
        internal static DatabaseInfo GetDatabaseInfo(ConnectionInfo connInfo)
        {
            using (DatabaseTaskHelper taskHelper = CreateDatabaseTaskHelper(connInfo, true))
            {
                return DatabaseTaskHelper.DatabasePrototypeToDatabaseInfo(taskHelper.Prototype);
            }
        }

        /// <summary>
        /// Create database task helper
        /// </summary>
        /// <param name="connInfo">connection info</param>
        /// <param name="databaseExists">flag indicating whether to create taskhelper for existing database or not</param>
        /// <returns></returns>
        internal static DatabaseTaskHelper CreateDatabaseTaskHelper(ConnectionInfo connInfo, bool databaseExists = false)
        {
            XmlDocument xmlDoc = CreateDataContainerDocument(connInfo, databaseExists);
            CDataContainer dataContainer;

             // add alternate port to server name property if provided
            var connectionDetails = connInfo.ConnectionDetails;
            string serverName = !connectionDetails.Port.HasValue
                ? connectionDetails.ServerName
                : string.Format("{0},{1}", connectionDetails.ServerName, connectionDetails.Port.Value);

            // check if the connection is using SQL Auth or Integrated Auth
            //TODO: ConnectionQueue try to get an existing connection (ConnectionQueue)
            if (string.Equals(connectionDetails.AuthenticationType, "SqlLogin", StringComparison.OrdinalIgnoreCase))
            {
                var passwordSecureString = BuildSecureStringFromPassword(connectionDetails.Password);
                dataContainer = new CDataContainer(
                    CDataContainer.ServerType.SQL,
                    serverName,
                    false,
                    connectionDetails.UserName,
                    passwordSecureString,
                    xmlDoc.InnerXml);
            }
            else
            {
                dataContainer = new CDataContainer(
                    CDataContainer.ServerType.SQL,
                    serverName,
                    true,
                    null,
                    null,
                    xmlDoc.InnerXml);
            }

            var taskHelper = new DatabaseTaskHelper(dataContainer);
            return taskHelper;
        }

        internal static System.Security.SecureString BuildSecureStringFromPassword(string password) {
            var passwordSecureString = new System.Security.SecureString();
            if (password != null) {
                foreach (char c in password) {
                    passwordSecureString.AppendChar(c);
                }
            }
            return passwordSecureString;
        }

        /// <summary>
        /// Create data container document
        /// </summary>
        /// <param name="connInfo">connection info</param>
        /// <param name="databaseExists">flag indicating whether to create document for existing database or not</param>
        /// <returns></returns>
        private static XmlDocument CreateDataContainerDocument(ConnectionInfo connInfo, bool databaseExists)
        {
            string xml = string.Empty;

            if (!databaseExists)
            {
                xml =
                string.Format(@"<?xml version=""1.0""?>
                <formdescription><params>
                <servername>{0}</servername>
                <connectionmoniker>{0} (SQLServer, user = {1})</connectionmoniker>
                <servertype>sql</servertype>
                <urn>Server[@Name='{0}']</urn>
                <itemtype>Database</itemtype>                
                </params></formdescription> ",
                connInfo.ConnectionDetails.ServerName.ToUpper(),
                connInfo.ConnectionDetails.UserName);
            }
            else
            {
                xml =
                string.Format(@"<?xml version=""1.0""?>
                <formdescription><params>
                <servername>{0}</servername>
                <connectionmoniker>{0} (SQLServer, user = {1})</connectionmoniker>
                <servertype>sql</servertype>
                <urn>Server[@Name='{0}']</urn>
                <database>{2}</database>                
                </params></formdescription> ",
                connInfo.ConnectionDetails.ServerName.ToUpper(),
                connInfo.ConnectionDetails.UserName,
                connInfo.ConnectionDetails.DatabaseName);
            }
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            return xmlDoc;
        }

        /// <summary>
        /// Handles a create login request
        /// </summary>
        internal static void HandleCreateLoginRequest(
            CreateLoginParams loginParams,
            RequestContext<CreateLoginResponse> requestContext)
        {
            requestContext.SendResult(new CreateLoginResponse());
        }
    }
}
