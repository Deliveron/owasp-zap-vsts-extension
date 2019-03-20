using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using owasp_zap_vsts_tool.Services;

namespace IntegrationTests
{
    [TestClass]
    public class OwaspZapTests
    {
        [TestMethod]
        public async Task OwaspZapFailures_ShouldCreateBugs()
        {

            string collectionUri = "https://myaccount.visualstudio.com";
            string teamProjectName = "My Team Project";
            string team = "SRE";
            string releaseUri = "vstfs:///ReleaseManagement/Release/209";
            string releaseEnvironmentUri = "vstfs:///ReleaseManagement/Environment/538";
            string filePath = @"C:\alerts\full_OwaspZapAlerts.xml";
            bool failOnHigh = false;
            string personalAccessToken = "1234";
            string targetUrl = "https://mysite.azurewebsites.net";
            string bugTitlePrefix = "Unit Test";


            var client = new AzureDevOpsClient();

           await client.CreateBugFromPenTestAsync(collectionUri, teamProjectName, team, releaseUri, releaseEnvironmentUri, filePath, failOnHigh, personalAccessToken, targetUrl, bugTitlePrefix);
        }

    }
}
