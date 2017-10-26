# owasp-zap-vsts-extension
Tools to run OWASP ZAP container in VSTS build and release

## Install (full configuration instructions are coming soon)
Create Azure Container Service using Docker Swarm

Create CI build to compile owasp=zap-vsts-tool and include Invoke-OwaspZapActiveScan.ps1 in artifact

Create Release with CI build as artifact. Include powershell task to call Invoke-OwaspZapActiveScan.ps1.  There are no parameters but you need to pass the the values through parameters.  Each custom environment variable belows needs to be created as variables (without the $env:)

$basePath = $env:AGENT_RELEASEDIRECTORY
$dockerKeyFile = $basePath + $env:PrivateKeyFile   # "\TfsWorkItemMgmt\drop\scripts\SSH-Sessions\privatekey.key"
$dockerServer = $env:DockerServer                  # "myappmgmt.centralus.cloudapp.azure.com"
$dockerUsername = $env:DockerUsername              # user
$containerName = $env:ContainerName                # "owasp/zap2docker-weekly"
$containerPort = $env:ContainerPort                # 8098
$containerApiKey = $env:ContainerApiKey            # "aE4w8dhwWE24VGDsreP"
$contextFile = $env:ContextFile                    # "\TfsWorkItemMgmt\drop\scripts\contexts\delivermoredev.context"
$targetUrl = $env:TargetUrl                        # "https://yoursite.azurewebsites.net"

### Attach Report - Create a cmd task to execute the owasp-zap-vsts-tool.exe

tool: $(System.DefaultWorkingDirectory)/owasp-zap-vsts CI/drop/owasp-zap-vsts-tool/bin/Release/owasp-zap-vsts-tool.exe
Arguments: attachreport collectionUri="https://myacct.visualstudio.com" teamProjectName="CLExtended" releaseUri=$(Release.ReleaseUri) releaseEnvironmentUri=$(Release.EnvironmentUri) filepath=$(System.DefaultWorkingDirectory)\OwaspZapReport.html personalAccessToken=abc123

### Create Bugs - Create a cmd task 

tool: $(System.DefaultWorkingDirectory)/owasp-zap-vsts CI/drop/owasp-zap-vsts-tool/bin/Release/owasp-zap-vsts-tool.exe
arguments: createbugfrompentest collectionUri="https://myacct.visualstudio.com" teamProjectName="CLExtended" team=Demo releaseUri=$(Release.ReleaseUri) releaseEnvironmentUri=$(Release.EnvironmentUri) filepath=$(Agent.ReleaseDirectory)\OwaspZapAlerts.xml personalAccessToken=abc123
