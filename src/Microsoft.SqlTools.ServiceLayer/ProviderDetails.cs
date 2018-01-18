using Microsoft.SqlTools.Dmp.Contracts;

namespace Microsoft.SqlTools.ServiceLayer
{
    public class SqlToolsServiceProviderDetails
    {
        public static readonly ProviderDetails ProviderDetails = new ProviderDetails
        {
            ProviderProtocolVersion = "1.0",
            ProviderName = "MSSQL",
            ProviderDescription = "Microsoft SQL Server"
        };
    }
}