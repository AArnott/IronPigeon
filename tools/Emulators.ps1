#!/usr/bin/env pwsh

<#
.SYNOPSIS
Performs operations on the Azure storage emulator.
.PARAMETER Clear
Clears logs and local stores for the storage emulator. Useful for resetting state.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
Param (
    [Switch]$Clear
)

$EmulatorStoragePath = "$PSScriptRoot/obj/azurite"
$EmulatorLogPath = "$PSScriptRoot/obj/azurite.log"

if ($Clear) {
    Remove-Item -Path $EmulatorStoragePath,$EmulatorLogPath -Force -Recurse
}

if (Test-Path "$env:ProgramW6432\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator") {
    Import-Module "$env:ProgramW6432\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator"
} elseif (Test-Path "$env:ProgramFiles\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator") {
    Import-Module "$env:ProgramFiles\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator"
} else {
    Write-Error "Azure CosmosDB Emulator not found."
}

Start-CosmosDbEmulator

if (!(Test-Path $EmulatorStoragePath)) { New-Item -ItemType Directory -Path $EmulatorStoragePath | Out-Null }
cmd /c start yarn run azurite-blob -l $EmulatorStoragePath -d $EmulatorLogPath
