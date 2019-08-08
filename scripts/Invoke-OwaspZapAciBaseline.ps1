param([string] $targetUrl,
	  [string] $resourceGroupName,
	  [string] $location)

function Invoke-OwaspZapAciBaselineScan {
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
		[String] $targetUrl
	  , [Parameter(Mandatory = $true)]
		[String] $resourceGroupName
	  , [Parameter(Mandatory = $true)]
		[String] $location
	  , [Parameter(Mandatory = $true)]
		[String] $storageShareName
	  , [Parameter(Mandatory = $true)]
		[String] $containerGroupName
	  , [Parameter(Mandatory = $true)]
		[String] $containerDnsName
	  , [Parameter(Mandatory = $true)]
		[String] $dockerImageName
		)
	
	$storageAccountName = "cizapstr" + -join ((65..90) + (97..122) | Get-Random -Count 5 | % {[char]$_})
	$storageAccountName = $storageAccountName.ToLower()

	write-host "Checking to see if resource group exists"
	Get-AzureRmResourceGroup -Name $resourceGroupName -ErrorVariable notPresent -ErrorAction SilentlyContinue

	if ($notPresent)
	{
		"Resource group doesn't exist. Creating..."
		New-AzureRmResourceGroup -Name $resourceGroupName -Location $location
	}

	$newStorageAccount = New-AzureRmStorageAccount -ResourceGroupName $resourceGroupName `
	  -Name $storageAccountName `
	  -SkuName "Standard_LRS" `
	  -Location $location `
	  -Kind StorageV2 

	$storageAccountKeyList = Get-AzureRmStorageAccountKey -ResourceGroupName $resourceGroupName -AccountName $storageAccountName
	$storageAccountKey = ($storageAccountKeyList | Select-Object -First 1).Value 
	$storageAccountContext = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageAccountKey
	New-AzureStorageShare -Name $storageShareName -Context $storageAccountContext

	$secpasswd = ConvertTo-SecureString $storageAccountKey -AsPlainText -Force
	$storageCredentials = New-Object System.Management.Automation.PSCredential ($storageAccountName, $secpasswd)

	$containerGroup = Get-AzureRmContainerGroup -ResourceGroupName $resourceGroupName -Name $containerGroupName -ErrorAction SilentlyContinue
	if (!$containerGroup) {
		New-AzureRmContainerGroup -ResourceGroupName $resourceGroupName -Name $containerGroupName -Image $dockerImageName -Command "zap-baseline.py -t $targetUrl -x issues.xml -r testreport.html" `
			 -IpAddressType Public -DnsNameLabel $containerDnsName -Location $location -AzureFileVolumeShareName $storageShareName `
			-AzureFileVolumeMountPath '/zap/wrk' -AzureFileVolumeAccountCredential $storageCredentials -RestartPolicy "Never"
		 }
	start-sleep 240
	$containerGroup = Get-AzureRmContainerGroup -ResourceGroupName $resourceGroupName -Name $containerGroupName -ErrorAction SilentlyContinue
	$container = $containerGroup.Containers[0]
	write-host "current state:" + $container.CurrentState

	Do
		{
			start-sleep 10
			$containerGroup = Get-AzureRmContainerGroup -ResourceGroupName $resourceGroupName -Name $containerGroupName -ErrorAction SilentlyContinue
			$container = $containerGroup.Containers[0]
			write-host "Running scan..."
		} until ($container.CurrentState -ne "Running")

	write-host "current state:" + $container.CurrentState
	start-sleep 10
	Remove-AzureRmContainerGroup -ResourceGroupName $resourceGroupName -Name $containerGroupName
	write-host "ACI removed"

	$ctx = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageAccountKey
	Do
	{
	$attemptCount
	$attemptCount++

	write-host 'Trying file copy: Attempt $attemptCount'

	get-azurestoragefilecontent -ShareName $storageShareName -Path "issues.xml" -Context $ctx -Destination "$basePath\issues.xml" -force -verbose
	get-azurestoragefilecontent -ShareName $storageShareName -Path "testreport.html" -Context $ctx -Destination "$basePath\testreport.html" -force -verbose

	$copyIssuesValidation = 	Test-Path "$basePath\issues.xml" -ErrorAction SilentlyContinue -Verbose
	$copyTestReportValidation = 	Test-Path "$basePath\testreport.html" -ErrorAction SilentlyContinue -Verbose

	start-sleep 10
	} until (($copyIssuesValidation -eq $true) -and ($copyTestReportValidation -eq $true))
	write-host "Files downloaded"
	write-host "Removing Storage Account"

	Remove-AzureRmStorageAccount -ResourceGroup $resourceGroupName -AccountName $storageAccountName -Force
	write-host "Storage account removed"
}


	#$location = "CentralUS"
	#$targetScanUrl = "https://wapp-appdemo-cus-dev.azurewebsites.net"
	$storageShareName  = "acishare"
	#$resourceGroupName = "rg-cizap2"
	$containerGroupName = "cizapweekly"
	$containerDnsName = "cizapweekly"
	$dockerImageName = "owasp/zap2docker-weekly"
	$basePath = $env:AGENT_RELEASEDIRECTORY

Invoke-OwaspZapAciBaselineScan -basePath $basePath -targetUrl $targetUrl `
		-resourceGroupName $resourceGroupName -location $location -storageShareName $storageShareName `
		-containerGroupName $containerGroupName -containerDnsName $containerDnsName -dockerImageName $dockerImageName