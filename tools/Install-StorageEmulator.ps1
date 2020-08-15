$AzureStorageEmulatorInstallerPath = "$env:temp\AzureEmulatorInstaller.msi"
if (!(Test-Path $AzureStorageEmulatorInstallerPath)) {
    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/?linkid=717179&clcid=0x409" -OutFile $AzureStorageEmulatorInstallerPath
    Unblock-File $AzureStorageEmulatorInstallerPath
}

Start-Process -FilePath msiexec -ArgumentList '/i',"$AzureStorageEmulatorInstallerPath","/passive","/norestart" -Wait
$AzureStorageEmulator = "${env:ProgramFiles(x86)}\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe"
& $AzureStorageEmulator start
