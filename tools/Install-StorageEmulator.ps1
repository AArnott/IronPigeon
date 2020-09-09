[CmdletBinding(SupportsShouldProcess = $true)]
Param (
    [ValidateSet('repo', 'user', 'machine')]
    [string]$InstallLocality = 'user'
)

$AzureStorageEmulator = "${env:ProgramFiles(x86)}\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe"

if (!(Test-Path $AzureStorageEmulator)) {
    if ($InstallLocality -eq 'machine') {
        $AzureStorageEmulatorInstallerPath = "$env:temp\AzureEmulatorInstaller.msi"
        if (!(Test-Path $AzureStorageEmulatorInstallerPath)) {
            Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/?linkid=717179&clcid=0x409" -OutFile $AzureStorageEmulatorInstallerPath
            Unblock-File $AzureStorageEmulatorInstallerPath
        }

        Start-Process -FilePath msiexec -ArgumentList '/i', "$AzureStorageEmulatorInstallerPath", "/passive", "/norestart" -Wait
    }
    else {
        Write-Warning "Azure Storage emulator must be running, but installing it has been skipped since -InstallLocality Machine was not specified."
    }
}

& $AzureStorageEmulator start
