[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Commit,
    [string]$OutputDirectory = ".tmp\production-frontend",
    [string]$LicensePath = ""
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$output = Join-Path $repo $OutputDirectory
$publish = Join-Path $output "publish"
$archive = Join-Path $output "paragateway-frontend-$Commit.tar.gz"
$publishLog = Join-Path $output "publish.log"
if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
$head = (& git -C $repo rev-parse HEAD).Trim()
if ($head -ne $Commit) { throw "当前 HEAD 为 $head，不是指定提交 $Commit。" }
$worktreeStatus = @(& git -C $repo status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw "无法确认 Git 工作区状态。" }
if ($worktreeStatus.Count -gt 0) {
    throw "Git 工作区存在未提交内容，已停止生成提交归档。请先提交并确认工作区干净。"
}

# Remove stale output before any validation so a failed run cannot leave an old
# archive that looks like the requested commit.
if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }

$licenseValue = [Environment]::GetEnvironmentVariable("DevExpress_License", "Process")
$licenseSource = "environment"
if ([string]::IsNullOrWhiteSpace($licenseValue)) {
    $licenseCandidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($LicensePath)) {
        $licenseCandidates.Add($LicensePath)
    }
    if (-not [string]::IsNullOrWhiteSpace($env:DEVEXPRESS_LICENSE_FILE)) {
        $licenseCandidates.Add($env:DEVEXPRESS_LICENSE_FILE)
    }
    if (-not [string]::IsNullOrWhiteSpace($env:APPDATA)) {
        $licenseCandidates.Add((Join-Path $env:APPDATA "DevExpress\DevExpress_License.txt"))
    }

    $licenseFile = $licenseCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($licenseFile)) {
        throw "未找到 DevExpress 许可证。请设置 DevExpress_License、DEVEXPRESS_LICENSE_FILE 或 -LicensePath。"
    }

    $licenseValue = [IO.File]::ReadAllText($licenseFile)
    $licenseSource = $licenseFile
}
if ([string]::IsNullOrWhiteSpace($licenseValue)) {
    throw "DevExpress 许可证为空，已停止生成生产前端归档。"
}

$previousLicenseValue = [Environment]::GetEnvironmentVariable("DevExpress_License", "Process")
$restoreLicenseValue = $previousLicenseValue
try {
    [Environment]::SetEnvironmentVariable("DevExpress_License", $licenseValue, "Process")
    Write-Verbose "Using DevExpress license from $licenseSource."

    $project = Join-Path $repo "frontend-blazor\ParaGateway.Frontend.csproj"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    dotnet clean $project -c Release --nologo -p:UseAppHost=false 2>&1 |
        Tee-Object -FilePath $publishLog
    $cleanExitCode = $LASTEXITCODE
    if ($cleanExitCode -ne 0) { throw "前端 Release 清理失败，退出码为 $cleanExitCode。" }

    New-Item -ItemType Directory -Path $publish -Force | Out-Null
    dotnet publish $project -c Release -o $publish --no-restore -p:UseAppHost=false --nologo 2>&1 |
        Tee-Object -FilePath $publishLog
    $publishExitCode = $LASTEXITCODE
    if ($publishExitCode -ne 0) { throw "前端 Release 发布失败，退出码为 $publishExitCode。" }
    if (Select-String -LiteralPath $publishLog -Pattern "DX1000|DX1001|DX1002|DX1003|For evaluation purposes only") {
        throw "DevExpress 许可证未被构建接受，已停止生成生产前端归档。"
    }

    $appWasm = @(Get-ChildItem -LiteralPath (Join-Path $publish "wwwroot\_framework") -Filter "ParaGateway.Frontend*.wasm" -File)
    if ($appWasm.Count -ne 1) {
        throw "无法唯一识别 ParaGateway 前端 WASM，已停止生成生产前端归档。"
    }
    $appWasmText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($appWasm[0].FullName))
    if ([regex]::Matches($appWasmText, "LCPv1!").Count -ne 1) {
        throw "前端 WASM 未包含唯一的 DevExpress 许可证属性，已停止生成生产前端归档。"
    }
    [IO.File]::WriteAllText(
        (Join-Path $publish "wwwroot\release-commit.txt"),
        $Commit,
        [Text.UTF8Encoding]::new($false))
}
finally {
    [Environment]::SetEnvironmentVariable("DevExpress_License", $restoreLicenseValue, "Process")
    if (Test-Path $publishLog) { Remove-Item -LiteralPath $publishLog -Force }
}
$index = Join-Path $publish "wwwroot\index.html"
if (-not (Test-Path $index)) { throw "前端发布产物缺少 wwwroot/index.html。" }
$archiveResult = tar -czf $archive -C $publish .
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $archive -PathType Leaf)) {
    throw "前端归档生成失败。"
}
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "commit=$Commit"
Write-Output "archive=$archive"
Write-Output "sha256=$hash"
