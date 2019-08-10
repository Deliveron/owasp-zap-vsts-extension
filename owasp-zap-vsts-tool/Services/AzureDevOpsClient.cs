using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using owasp_zap_vsts_tool.Models;
using TestCaseResult = Microsoft.TeamFoundation.TestManagement.WebApi.TestCaseResult;

namespace owasp_zap_vsts_tool.Services
{
    public class AzureDevOpsClient
    {

        public async Task CreateBugFromPenTestAsync(string collectionUri, string teamProjectName, string team, string releaseUri, string releaseEnvironmentUri, string filePath, bool failOnHigh, string personalAccessToken, string targetUrl, string bugTitlePrefix)
        {
            try
            {

            var connection = GetConnection(collectionUri, personalAccessToken);

            var testManagementClient = connection.GetClient<TestManagementHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();
            var witClient = connection.GetClient<WorkItemTrackingHttpClient>();
            var workClient = connection.GetClient<WorkHttpClient>();

            var queryModel = new QueryModel(query: "SELECT * FROM TestRun WHERE ReleaseUri = '" + releaseUri + "' and ReleaseEnvironmentUri in ('" + releaseEnvironmentUri + "')");
            var testRunResult = await testManagementClient.GetTestRunsByQueryAsync(queryModel, teamProjectName);

            var project = await projectClient.GetProject(teamProjectName);
            string projectId = project.Id.ToString();

            var teamContext = new TeamContext(teamProjectName, team);
            var teamSettings = await workClient.GetTeamSettingsAsync(teamContext);
            var teamFieldValues = await workClient.GetTeamFieldValuesAsync(teamContext);

            
            var teamIterations = await workClient.GetTeamIterationsAsync(teamContext, "current");
            var teamIteration = await workClient.GetTeamIterationAsync(teamContext, teamIterations[0].Id);


                string areaPath = teamFieldValues.DefaultValue;
            string iterationPath = "";
            if (teamIterations.Count > 0)
                iterationPath = teamIterations[0].Path;
            else
                iterationPath = teamIteration.Path;

            TestRun testRun;

            if (testRunResult == null || testRunResult.Count == 0)
            {
                var runCreateModel = new RunCreateModel("OWASP ZAP Security Tests", isAutomated: true, releaseUri: releaseUri, releaseEnvironmentUri: releaseEnvironmentUri);
                testRun = await testManagementClient.CreateTestRunAsync(runCreateModel, projectId);
            }
            else
            {
                testRun = testRunResult.FirstOrDefault();
            }

            
            var report = GetReport(filePath, targetUrl);
            string title = String.Empty;
                string description = String.Empty;
                string solution = String.Empty;
                string severity = string.Empty;
                

            bool testsPassed = true;
            bool highFailure = false;
            var results = new List<TestCaseResult>();
 

            if (report != null && report.Issues != null && report.Issues.Count() > 0)
            {
                Console.WriteLine("Issues(" + report.Issues.Count() + " found.");
                foreach (Issue issue in report.Issues)
                {
                    severity = issue.RiskDescription.Split(' ')[0];
                    if (severity.Contains("Information"))
                    {
                        continue;
                    }
                    testsPassed = false;
                    //create bug
                    if (issue.RiskDescription.Contains("High"))
                    {
                        highFailure = true;
                    }
                    title = issue.IssueDescription;
                        description = issue.Description;
                        solution = issue.Solution;
                    await CreateBugAsync(collectionUri, teamProjectName, team, targetUrl, severity, bugTitlePrefix + " " + title, description, solution, areaPath, iterationPath, personalAccessToken);

                    results.Add(new TestCaseResult
                    {
                        AutomatedTestName = bugTitlePrefix + " " + title,
                        Outcome = testsPassed ? "Passed" : "Failed",
                        TestCaseTitle = bugTitlePrefix + " " + title,
                        State = "Completed"
                    });
                }
                var testResults = await testManagementClient.AddTestResultsToTestRunAsync(results.ToArray(), teamProjectName, testRun.Id);
            }

            // TODO CloseFixedBugs()

            if (!failOnHigh)
                return;

            var updateProperties = new RunUpdateModel(state: "Completed", completedDate: DateTime.Today.ToShortDateString());
            var updateResults = await testManagementClient.UpdateTestRunAsync(updateProperties, projectId, testRun.Id);

            if (highFailure && failOnHigh)
            {
                Console.WriteLine("High Issue Found. Deployment Failed.");
                throw new Exception("High Issue Found.  Deployment Failed.");
            }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task AttachReportToBuildTestRunAsync(string collectionUri, string teamProjectName, string releaseUri, string releaseEnvironmentUri, string reportFile, string personalAccessToken)
        {
            var connection = GetConnection(collectionUri, personalAccessToken);

            var testManagementClient = connection.GetClient<TestManagementHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            Console.WriteLine("Attaching Report");

            var queryModel = new QueryModel(query: "SELECT * FROM TestRun WHERE ReleaseUri = '" + releaseUri + "' and ReleaseEnvironmentUri in ('" + releaseEnvironmentUri + "')");
            var testRunResult = await testManagementClient.GetTestRunsByQueryAsync(queryModel, teamProjectName);
            TestRun testRun;

            if (testRunResult == null || testRunResult.Count == 0)
            {
                var runCreateModel = new RunCreateModel("OWASP ZAP Security Tests", isAutomated: true, releaseUri: releaseUri, releaseEnvironmentUri: releaseEnvironmentUri);

                var project = await projectClient.GetProject(teamProjectName);
                string projectId = project.Id.ToString();

                testRun = await testManagementClient.CreateTestRunAsync(runCreateModel, projectId);
            }
            else
            {
                testRun = testRunResult.FirstOrDefault();
            }

            string stream = File.ReadAllText(reportFile);

            var attachmentModel = new TestAttachmentRequestModel(stream: Base64Encode(stream), fileName: "OwaspZapTestResultsReport.html");
            var testAttachmentResult = await testManagementClient.CreateTestRunAttachmentAsync(attachmentModel, teamProjectName, testRun.Id);
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private static async Task CreateBugAsync(string collectionUri, string teamProjectName, string team, string url, string severity, string title, string description, string solution, string areaPath, string iterationPath, string personalAccessToken)
        {
            var connection = GetConnection(collectionUri, personalAccessToken);

            var witClient = connection.GetClient<WorkItemTrackingHttpClient>();
            var workClient = connection.GetClient<WorkHttpClient>();

            string fullTitle = severity + " - " + title + " in " + url;


            // Check to see if bug exists for team and is current open
            var wiql = new Wiql();
            wiql.Query = "SELECT [System.Title] FROM WorkItems WHERE [System.TeamProject] = '" + teamProjectName + "' AND [System.WorkItemType] = 'Bug' AND [System.Title] CONTAINS '" + fullTitle + "' AND [System.State] <> 'Closed' AND [System.State] <> 'Removed' AND [System.AreaPath] UNDER '" + areaPath + "' AND [System.IterationPath] UNDER '" + iterationPath + "'";

            var results = await witClient.QueryByWiqlAsync(wiql);

            string severityValue = severity == "High" ? "2 - High" : severity == "Low" ? "4 - Low" : "3 - Medium";

            if (results.WorkItems.Count() == 0)
            {
                // create new bug

                var doc = new JsonPatchDocument();
                doc.Add(
                    new JsonPatchOperation()
                    {
                        Path = "/fields/System.Title",
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Value = fullTitle
                    });
                doc.Add(
                    new JsonPatchOperation()
                    {
                        Path = "/fields/Microsoft.VSTS.TCM.ReproSteps",
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Value = description + " " + solution
                    });
                doc.Add(
                new JsonPatchOperation()
                {
                    Path = "/fields/Microsoft.VSTS.Common.Severity",
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Value = severityValue
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

                var witCreated = await witClient.CreateWorkItemAsync(doc, teamProjectName, "Bug");
                System.Console.WriteLine("Bug created");
            }
            else
            {
                // if bug was created and resolved but bug was still found, move back to active
                var bug = results.WorkItems.First();
                var bugDetail = await witClient.GetWorkItemAsync(bug.Id);

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
                    await witClient.UpdateWorkItemAsync(doc, bug.Id);
                    System.Console.WriteLine("Bug is not fixed.  Moving to active.");
                }
                else
                {
                    System.Console.WriteLine("Bug is already active");
                }
            }
        }


        private static Report GetReport(string filePath, string url)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filePath);

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
                            OriginalSiteUrl = element.Attribute("name").Value,
                            Description = e.Element("desc").Value,
                            Solution = e.Element("solution").Value,
                            Instances = (from i in e.Descendants("instance")
                                         select new IssueInstance
                                         {
                                             Uri = i.Element("uri").Value,
                                             Evidence = i.Element("evidence") != null ? i.Element("evidence").Value : ""
                                            }).ToList()

                        }) ;; ;
                           
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
    }
}
