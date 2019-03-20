using System;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.WebApi;
using owasp_zap_vsts_tool.Services;

namespace owasp_zap_vsts_tool
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    System.Console.WriteLine("Please enter a command for example: attachreport or createbugfrompentest");

                    System.Environment.ExitCode = 1;
                    return;
                }

                var azureDevOpsClient = new AzureDevOpsClient();

                if (args[0].ToLower() == "attachreport")
                {
                    if (args.Length < 3)
                    {
                        System.Console.WriteLine("Please enter the following arguments for CreateBug using format argument=value");
                        System.Console.WriteLine("    collectionUri=\"http://tfs2015:8080/tfs/DefaultCollection\"   (Url to Tfs including collection)");
                        System.Console.WriteLine("    teamProjectName=\"Enterprise\"   (Name of Team Project)");

                    }

                    string collectionUri = args[1].Split('=')[1];
                    string teamProjectName = args[2].Split('=')[1];
                    string releaseUri = args[3].Split('=')[1];
                    string releaseEnvironmentUri = args[4].Split('=')[1];
                    string reportFile = args[5].Split('=')[1];

                    string personalAccessToken = string.Empty;
                    if (args.Length == 7)
                    {
                        personalAccessToken = args[6].Split('=')[1];
                    }

                    await azureDevOpsClient.AttachReportToBuildTestRunAsync(collectionUri, teamProjectName, releaseUri, releaseEnvironmentUri, reportFile, personalAccessToken);
                }

                if (args[0].ToLower() == "createbugfrompentest")
                {
                    if (args.Length < 3)
                    {
                        System.Console.WriteLine("Please enter the following arguments for createbugfrompentest using format argument=value");
                        System.Console.WriteLine("    collectionUri=\"http://tfs2015:8080/tfs/DefaultCollection\"   (Url to Tfs including collection)");
                        System.Console.WriteLine("    teamProjectName=\"Enterprise\"   (Name of Team Project)");
                        System.Console.WriteLine("    team=\"Team A\"   (Name of team where the bug will be created)");
                        System.Console.WriteLine("    title=\"New Bug\"   (Title of bug)");

                        return;
                    }

                    string collectionUri = args[1].Split('=')[1];
                    string teamProjectName = args[2].Split('=')[1];
                    string team = args[3].Split('=')[1];
                    string releaseUri = args[4].Split('=')[1];
                    string releaseEnvironmentUri = args[5].Split('=')[1];
                    string filePath = args[6].Split('=')[1];
                    string bugTitlePrefix = args[7].Split('=')[1];
                    string targetUrl = args[8].Split('=')[1];
                    bool failOnHigh = true;
                    if (args.Length >= 10)
                    {
                        string rawStringFailOnHigh = args[9].Split('=')[1];
                        bool.TryParse(rawStringFailOnHigh, out failOnHigh);
                    }

                    string personalAccessToken = string.Empty;
                    if (args.Length == 11)
                    {
                        personalAccessToken = args[10].Split('=')[1];
                    }

                   await azureDevOpsClient.CreateBugFromPenTestAsync(collectionUri, teamProjectName, team, releaseUri, releaseEnvironmentUri, filePath, failOnHigh, personalAccessToken, targetUrl, bugTitlePrefix);
                }
            }
            catch(Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                System.Environment.ExitCode = 99;
                return;
            }
        }
    }
}
