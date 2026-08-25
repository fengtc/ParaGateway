[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Commit,
    [string]$OutputDirectory = ".tmp\production-frontend"
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$output = Join-Path $repo $OutputDirectory
$publish = Join-Path $output "publish"
$archive = Join-Path $output "paragateway-frontend-$Commit.tar.gz"
$head = (& git -C $repo rev-parse HEAD).Trim()
if ($head -ne $Commit) { throw "当前 HEAD 为 $head，不是指定提交 $Commit。" }
if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $publish -Force | Out-Null
dotnet publish (Join-Path $repo "frontend-blazor\ParaGateway.Frontend.csproj") -c Release -o $publish --no-restore -p:UseAppHost=false --nologo
$index = Join-Path $publish "wwwroot\index.html"
if (-not (Test-Path $index)) { throw "前端发布产物缺少 wwwroot/index.html。" }
tar -czf $archive -C $publish .
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "commit=$Commit"
Write-Output "archive=$archive"
Write-Output "sha256=$hash"
