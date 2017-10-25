using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using System.IO;
using owasp_zap_vsts_tool.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace owasp_zap_vsts_tool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                System.Console.WriteLine("Please enter a command for example: attachreport or createbugfrompentest");

                System.Environment.ExitCode = 1;
                return;
            }

            if (args[0].ToLower() == "attachreport")
            {
                if (args.Length < 6)
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

                AttachReportToBuildTestRun(collectionUri, teamProjectName, releaseUri, releaseEnvironmentUri, reportFile, personalAccessToken);

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

                bool failOnHigh = true;
                if (args.Length == 8)
                {
                    string rawStringFailOnHigh = args[7].Split('=')[1];
                    bool.TryParse(rawStringFailOnHigh, out failOnHigh);
                }

                string personalAccessToken = string.Empty;
                if (args.Length == 9)
                {
                    personalAccessToken = args[8].Split('=')[1];
                }

                CreateBugFromPenTest(collectionUri, teamProjectName, team, releaseUri, releaseEnvironmentUri, filePath, failOnHigh, personalAccessToken);
            }
        }

        private static void CreateBugFromPenTest(string collectionUri, string teamProjectName, string team, string releaseUri, string releaseEnvironmentUri, string filePath, bool failOnHigh, string personalAccessToken)
        {
            var connection = GetConnection(collectionUri, personalAccessToken);

            // Create Test Run
            var testManagementClient = connection.GetClient<TestManagementHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            var runCreateModel = new RunCreateModel("OWASP ZAP Security Tests", isAutomated: true, releaseUri: releaseUri, releaseEnvironmentUri: releaseEnvironmentUri);

            string projectId = projectClient.GetProject(teamProjectName).Result.Id.ToString();

            var testRun = testManagementClient.CreateTestRunAsync(projectId, runCreateModel).Result;

            var report = GetReport(filePath);
            string title = String.Empty;

            bool testsPassed = true;
            bool highFailure = false;
            var results = new List<TestResultCreateModel>();

            foreach (Issue issue in report.Issues)
            {
                testsPassed = false;
                //create bug
                if (issue.RiskDescription.Contains("High"))
                {
                    highFailure = true;
                }
                title = issue.IssueDescription;

                CreateBug(collectionUri, teamProjectName, team, title);

                results.Add(new TestResultCreateModel
                {
                    AutomatedTestName = title,
                    Outcome = testsPassed ? "Passed" : "Failed",
                    TestCaseTitle = title,
                    State = "Completed"
                });

            }



            var testResults = testManagementClient.AddTestResultsToTestRunAsync(results.ToArray(), teamProjectName, testRun.Id).Result;

            var updateProperties = new RunUpdateModel(state: "Completed", completedDate: DateTime.Today.ToShortDateString());
            var updateResults = testManagementClient.UpdateTestRunAsync(projectId, testRun.Id, updateProperties).Result;

            if (highFailure)
            {
                Console.WriteLine("High Issue Found. Deployment Failed.");
                throw new Exception("High Issue Found.  Deployment Failed.");
            }

        }

        private static void CreateBug(string collectionUri, string teamProjectName, string team, string title, string areaPath, string iterationPath, string personalAccessToken)
        {
            var connection = GetConnection(collectionUri, personalAccessToken);

            // Create instance of WorkItemTrackingHttpClient using VssConnection
            var witClient = connection.GetClient<WorkItemTrackingHttpClient>();
            var workClient = connection.GetClient<WorkHttpClient>();

            // var team = teamClient.GetTeamAsync(teamProjectName, "Team A").Result;
            //teamClient.


            // Check to see if bug exists for team and is current open
            var wiql = new Wiql();
            wiql.Query = "SELECT [System.Title] FROM WorkItems WHERE [System.TeamProject] = '" + teamProjectName + "' AND [System.WorkItemType] = 'Bug' AND [System.Title] CONTAINS '" + title + "' AND [System.State] <> 'Closed' AND [System.State] <> 'Removed' AND [System.AreaPath] UNDER '" + areaPath + "' AND [System.IterationPath] UNDER '" + iterationPath + "'";

            var results = witClient.QueryByWiqlAsync(wiql).Result;

            if (results.WorkItems.Count() == 0)
            {
                // create new bug

                var doc = new JsonPatchDocument();
                doc.Add(
                    new JsonPatchOperation()
                    {
                        Path = "/fields/System.Title",
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Value = title
                    });
                doc.Add(
                    new JsonPatchOperation()
                    {
                        Path = "/fields/System.AreaPath",
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Value = areaPath
                    });
                doc.Add(
                    new JsonPatchOperation()
                    {
                        Path = "/fields/System.IterationPath",
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Value = iterationPath
                    });
                doc.Add(
                   new JsonPatchOperation()
                   {
                       Path = "/fields/System.Tags",
                       Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                       Value = "OWASP"
                   });

                var witCreated = witClient.CreateWorkItemAsync(doc, teamProjectName, "Bug").Result;
                System.Console.WriteLine("Bug created");
            }
            else
            {
                // if bug was created and resolved but bug was still found, move back to active
                var bug = results.WorkItems.First();
                var bugDetail = witClient.GetWorkItemAsync(bug.Id).Result;

                string state = bugDetail.Fields.First(w => w.Key == "System.State").Value.ToString();

                if (state.ToString() == "resolved")
                {
                    var doc = new JsonPatchDocument();
                    doc.Add(
                        new JsonPatchOperation()
                        {
                            Path = "/fields/System.State",
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
                            Value = "Active"
                        });
                    witClient.UpdateWorkItemAsync(doc, bug.Id);
                    System.Console.WriteLine("Bug is not fixed.  Moving to active.");

                }
                else
                {
                    System.Console.WriteLine("Bug is already active");
                }

            }



        }


        public static Report GetReport(string filePath)
        {
            string url = ConfigurationManager.AppSettings["TargetURL"];
            filePath = ConfigurationManager.AppSettings["AlertsFilePath"];

            XmlDocument doc = new XmlDocument();
            doc.Load(filePath);
            //doc.Load(filePath);
            string response = doc.InnerXml;

            if (response != null)
            {
                XDocument document = XDocument.Parse(response);

                IEnumerable<XElement> elements =
                    document.Element("OWASPZAPReport")
                        .Elements("site")
                        .Where(e => e.Attribute("host").Value == new Uri(url).Host);

                var issues = new List<Issue>();
                foreach (XElement element in elements)
                {
                    issues.AddRange(
                        from e in element.Descendants("alertitem")
                        select new Issue
                        {
                            IssueDescription = e.Element("alert").Value,
                            RiskDescription = e.Element("riskdesc").Value,
                            OriginalSiteUrl = element.Attribute("name").Value
                        });

                }

                return new Report { Issues = issues };
            }

            return null;
        }


        private static VssConnection GetConnection(string collectionUri, string personalAccessToken)
        {
            VssConnection connection = null;

            if (personalAccessToken.Length > 0)
            {
                connection = new VssConnection(new Uri(collectionUri), new VssBasicCredential(string.Empty, personalAccessToken));
            }
            else // Attempt to connect TFS 2015 method
            {
                VssCredentials creds = new VssClientCredentials();
                creds.Storage = new VssClientCredentialStorage();
                connection = new VssConnection(new Uri(collectionUri), creds);
            }

            return connection;
        }
        private static void AttachReportToBuildTestRun(string collectionUri, string teamProjectName, string releaseUri, string releaseEnvironmentUri, string reportFile, string personalAccessToken)
        {
            var connection = GetConnection(collectionUri, personalAccessToken);

            var testManagementClient = connection.GetClient<TestManagementHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            Console.WriteLine("Attaching Report");

            var queryModel = new QueryModel(query: "SELECT * FROM TestRun WHERE ReleaseUri = '" + releaseUri + "' and ReleaseEnvironmentUri in ('" + releaseEnvironmentUri + "')");
            var testRunResult = testManagementClient.GetTestRunsByQueryAsync(queryModel, teamProjectName).Result;
            TestRun testRun;

            if (testRunResult == null || testRunResult.Count == 0)
            {
                var runCreateModel = new RunCreateModel("OWASP ZAP Security Tests", isAutomated: true, releaseUri: releaseUri, releaseEnvironmentUri: releaseEnvironmentUri);

                string projectId = projectClient.GetProject(teamProjectName).Result.Id.ToString();

                testRun = testManagementClient.CreateTestRunAsync(projectId, runCreateModel).Result;
            }
            else
            {
                testRun = testRunResult.FirstOrDefault();
            }

            string stream = File.ReadAllText(reportFile);

            var attachmentModel = new TestAttachmentRequestModel(stream: Base64Encode(stream), fileName: "OwaspZapTestResultsReport.html");
            var testAttachmentResult = testManagementClient.CreateTestRunAttachmentAsync(attachmentModel, teamProjectName, testRun.Id).Result;

        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
