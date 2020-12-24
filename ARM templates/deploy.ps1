[CmdletBinding(SupportsShouldProcess=$true)]
Param (
    # [Parameter(Mandatory=$true)]
    # [string]$ResourceGroupName
)

az deployment create --template-file $PSScriptRoot/resourceGroup.json
