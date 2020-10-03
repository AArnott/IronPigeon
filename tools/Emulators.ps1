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

if (!(Test-Path $EmulatorStoragePath)) { New-Item -ItemType Directory -Path $EmulatorStoragePath | Out-Null }
yarn run azurite-blob -l $EmulatorStoragePath -d $EmulatorLogPath
