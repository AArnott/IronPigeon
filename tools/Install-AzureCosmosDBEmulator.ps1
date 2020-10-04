[CmdletBinding(SupportsShouldProcess=$true)]
Param (
    [ValidateSet('repo','user','machine')]
    [string]$InstallLocality='machine'
)

if ($IsLinux -or $IsMacOS) {
    Write-Error "Azure Cosmos DB Emulator is only compatible with Windows. Use an Azure Cosmos DB Table API account for local development instead. Set CosmosDBConnectionString to the connection string as an environment variable."
    exit 2
}

if ((Test-Path "$env:ProgramW6432\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator") -or (Test-Path "$env:ProgramFiles\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator")) {
    Write-Verbose "Azure Cosmos DB Emulator is already installed."
    exit
}

if ($InstallLocality -ne 'machine') {
    Write-Error "Azure Cosmos DB Emulator not found and will not be installed without the `"-InstallLocality Machine`" switch."
    exit 1
}

$toolsPath = & "$PSScriptRoot\..\azure-pipelines\Get-TempToolsPath.ps1"
$binaryToolsPath = Join-Path $toolsPath 'AzureCosmosDBEmulator'
if (!(Test-Path $binaryToolsPath)) { $null = mkdir $binaryToolsPath }
$installerPath = Join-Path $binaryToolsPath AzureCosmosDBEmulatorInstaller.msi

Write-Verbose "Downloading installer to $installerPath"
(New-Object System.Net.WebClient).DownloadFile('https://aka.ms/cosmosdb-emulator', $installerPath)

Write-Host "Installing Azure Cosmos DB Emulator..."
cmd /c start /wait "$InstallerPath" /passive
