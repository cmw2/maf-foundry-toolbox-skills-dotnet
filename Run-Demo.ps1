param(
    [Parameter(Mandatory)]
    [string] $AzureConfigDir,
    [Parameter(Mandatory)]
    [string] $ProjectEndpoint,
    [Parameter(Mandatory)]
    [string] $Model,
    [string] $ToolboxName = 'local-advising-proof-toolbox',
    [string] $ToolboxVersion = '1'
)

$ErrorActionPreference = 'Stop'
$env:AZURE_CONFIG_DIR = $AzureConfigDir
$env:FOUNDRY_PROJECT_ENDPOINT = $ProjectEndpoint
$env:FOUNDRY_MODEL = $Model
$env:FOUNDRY_TOOLBOX_NAME = $ToolboxName
$env:FOUNDRY_TOOLBOX_VERSION = $ToolboxVersion

Push-Location $PSScriptRoot
try {
    dotnet run --project '.\ToolboxSkillsLocal.csproj'
    if ($LASTEXITCODE -ne 0) {
        throw "The demo failed with exit code $LASTEXITCODE. Do not treat it as verified."
    }
}
finally {
    Pop-Location
}
