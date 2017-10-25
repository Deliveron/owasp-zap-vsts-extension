function Invoke-OwaspZapActiveScan {
	<#
	.Synopsis
	Runs Owasp Zap scan

	.Description
	Script will execute Owasp Zap container using Docker Swarm

	.Example

	#>
	[CmdletBinding(SupportsShouldProcess = $true)]
	param (
		[Parameter(Mandatory = $true)]
		[String] $basePath
	  , [Parameter(Mandatory = $true)]
		[String] $dockerServer
	  , [Parameter(Mandatory = $true)]
		[String] $containerName
	  , [Parameter(Mandatory = $true)]
		[String] $dockerUsername
	  , [Parameter(Mandatory = $true)]
		[String] $dockerKeyFile
	  , [Parameter(Mandatory = $true)]
		[String] $containerPort
	  , [Parameter(Mandatory = $true)]
		[String] $targetUrl
	  , [Parameter(Mandatory = $true)]
		[String] $containerApiKey
	  , [Parameter(Mandatory = $false)]
		[String] $contextFile
	)

	# Install SSH Sessions module
	Install-PackageProvider NuGet -Force -Scope CurrentUser
	Import-PackageProvider NuGet -Force 
	install-module -Name "SSHSessions" -Force -Scope CurrentUser
	import-module -Name "SSHSessions" -Force

	# Download pscp
	invoke-webrequest -Uri 'https://the.earth.li/~sgtatham/putty/latest/w64/pscp.exe' -outFile "$basePath\pscp.exe"

	# Connect to VM
	New-SshSession -ComputerName $dockerServer -UserName $dockerUsername -KeyFile $dockerKeyFile

	# Pull Weekly
	Invoke-SshCommand -ComputerName $dockerServer -Command "docker -H 172.16.0.5:2375 pull $containerName"

	start-sleep 10

	# Run Zap Container
	$dockerRunCommand = $SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 run -u zap -p ' + $containerPort + ':' + $containerPort + ' -d ' + $containerName + ' zap.sh -daemon -port ' + $containerPort + ' -host 0.0.0.0 -addonupdate -config database.recoverylog=false -config api.key=' + $containerApiKey + ' -config api.addrs.addr.name=.* -config api.addrs.addr.regex=true')
	$dockerRunCommand
	$containerId = $dockerRunCommand.Result
	
	start-sleep 20

	if($contextFile -ne $null -and $contextFile.Length -gt 0)
	{
		$hasContext = $true
		# Copy Context file and load it
		$copyTarget = $dockerUsername +"@" + $containerName + ":/home/" + $dockerUsername
		echo y | pscp -2 -i $puttyPrivateKey $contextFile $copyTarget
		start-sleep 10
		$SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 cp ' + $contextFile + ' ' +  $containerId.Replace("`n","")  + ':/zap/' + $contextFile)
		start-sleep 10
		$SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 exec ' +  $containerId.Replace("`n","") + ' curl "http://localhost:' + $containerPort + '/JSON/context/action/importContext?zapapiformat=JSON&apikey=' + $containerApiKey +'&contextFile=%2Fzap%2F' + $contextFile + '"')
		start-sleep 10
	}

	# Open URL
	$targetUrlEncoded = [System.Net.WebUtility]::UrlEncode($targetUrl)
	$SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 exec ' +  $containerId.Replace("`n","") + ' curl "http://localhost:' + $containerPort + '/JSON/spider/action/scan/?zapapiformat=JSON&apikey=' + $containerApiKey + '&formMethod=GET&url=' + $targetUrlEncoded +  '&maxChildren=1&recurse=0&contextName=&subtreeOnly=1"')

	start-sleep 20

	# Spider
	# TODO
	#start-sleep 10

	# Active Scan
	$contextUrl = ""
	if($hasContext)
	{
 		$contextUrl = "&contextId=0"
	}

	$SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 exec ' +  $containerId.Replace("`n","") + ' curl "http://localhost:' + $containerPort + '/JSON/ascan/action/scan/?zapapiformat=JSON&apikey=' + $containerApiKey + '&formMethod=GET&url=' + $targetUrlEncoded +  '&recurse=1&inScopeOnly=&scanPolicyName=Default+Policy&method=&postData="' + $contextUrl)

	Do
	{
		start-sleep 10
		$progress = $SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 exec ' +  $containerId.Replace("`n","") + ' curl "http://localhost:' + $containerPort + '/JSON/ascan/view/status/?zapapiformat=JSON&apikey=' + $containerApiKey + '&formMethod=GET&scanId=0"').Result
		write-host $progress
	} until ($progress.Contains('100') -or $progress.Contains('Does Not Exist'))

	start-sleep 10

	# Display Alerts
	write-host "Downloading Reports"
	$Alerts =$SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 exec ' +  $containerId.Replace("`n","") + ' curl  http://localhost:' + $containerPort + '/OTHER/core/other/xmlreport/?apikey=' + $containerApiKey + ' ').Result
	$Alerts | out-file -FilePath $basePath\OwaspZapAlerts.xml

	start-sleep 5
	# Report
	$Report = $SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 exec ' +  $containerId.Replace("`n","") + ' curl http://localhost:' + $containerPort + '/OTHER/core/other/htmlreport/?apikey=' + $containerApiKey + ' ').Result
	$Report | out-file -FilePath $basePath\OwaspZapReport.html

	start-sleep 5
	# Remove Container
	$SshSessions.$dockerServer.RunCommand('docker -H 172.16.0.5:2375 rm -f ' +  $containerId.Replace("`n",""))

}

# Retrieving values from enviornment variables created from VSTS release variables

$basePath = $env:AGENT_RELEASEDIRECTORY
$dockerKeyFile = $basePath + $env:PrivateKeyFile   # "\TfsWorkItemMgmt\drop\scripts\SSH-Sessions\privatekey.key"
$dockerServer = $env:DockerServer                  # "myappmgmt.centralus.cloudapp.azure.com"
$dockerUsername = $env:DockerUsername              # user
$containerName = $env:ContainerName                # "owasp/zap2docker-weekly"
$containerPort = $env:ContainerPort                # 8098
$containerApiKey = $env:ContainerApiKey            # "aE4w8dhwWE24VGDsreP"
$contextFile = $env:ContextFile                    # "\TfsWorkItemMgmt\drop\scripts\contexts\delivermoredev.context"
$targetUrl = $env:TargetUrl                        # "https://delivermore.azurewebsites.net"

Invoke-OwaspZapActiveScan -basePath $basePath -dockerServer $dockerServer -dockerUsername $dockerUsername -dockerKeyFile $dockerKeyFile -containerName $containerName -containerPort $containerPort -containerApiKey $containerApiKey -contextFile $contextFile -targetUrl $targetUrl