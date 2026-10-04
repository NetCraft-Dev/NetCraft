# NetCraft 构建脚本
# 用法：
#   .\build.ps1              # 构建 webui + Debug
#   .\build.ps1 Release      # 构建 webui + Release
#   .\build.ps1 Clean        # 清理
#   .\build.ps1 Rebuild      # 清理 + 重新构建（含 webui）
#   .\build.ps1 Debug -SkipFrontend   # 跳过 webui 仅构建 .NET（调试用）

param(
    [Parameter(Position=0)]
    [ValidateSet("Debug", "Release", "Clean", "Rebuild")]
    [string]$Action = "Debug",
    [switch]$SkipFrontend
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$solution = Join-Path $root "NetCraft.slnx"
$frontendBuildScript = Join-Path $root "webui\build-frontend.ps1"
$tpgaWwwroot = Join-Path $root "NetCraft.TPGA\wwwroot"

if (-not (Test-Path $solution)) {
    Write-Error "找不到解决方案文件: $solution"
    exit 1
}

function Build-Frontend {
    if ($SkipFrontend) {
        Write-Host "===== 跳过 webui 构建（-SkipFrontend）=====" -ForegroundColor DarkGray
        return
    }
    if (-not (Test-Path $frontendBuildScript)) {
        Write-Error "找不到 webui 构建脚本: $frontendBuildScript"
        exit 1
    }
    Write-Host "===== 构建 webui（多入口 player/admin）=====" -ForegroundColor Cyan
    & $frontendBuildScript
    if ($LASTEXITCODE -ne 0) {
        Write-Error "webui 构建失败 exit=$LASTEXITCODE"
        exit $LASTEXITCODE
    }
    if (Test-Path $tpgaWwwroot) {
        $count = (Get-ChildItem -Path $tpgaWwwroot -Recurse -File).Count
        Write-Host "webui 产物已输出到 NetCraft.TPGA\wwwroot（$count 个文件）" -ForegroundColor Green
    }
}

switch ($Action) {
    "Clean" {
        Write-Host "===== 清理解决方案 =====" -ForegroundColor Cyan
        dotnet clean $solution
        #清理 webui 构建产物
        if (Test-Path $tpgaWwwroot) {
            Remove-Item -Path $tpgaWwwroot -Recurse -Force
            Write-Host "已清理 NetCraft.TPGA\wwwroot" -ForegroundColor Green
        }
        Write-Host "===== 清理完成 =====" -ForegroundColor Green
    }
    "Rebuild" {
        Write-Host "===== 清理 + 重新构建（Debug）=====" -ForegroundColor Cyan
        dotnet clean $solution
        if (Test-Path $tpgaWwwroot) {
            Remove-Item -Path $tpgaWwwroot -Recurse -Force
        }
        Build-Frontend
        Write-Host "===== 构建 .NET 解决方案（Debug）=====" -ForegroundColor Cyan
        dotnet build $solution -c Debug
        if ($LASTEXITCODE -ne 0) {
            Write-Error "构建失败"
            exit $LASTEXITCODE
        }
        Write-Host "===== 构建成功 =====" -ForegroundColor Green
    }
    default {
        $config = $Action
        Build-Frontend
        Write-Host "===== 构建 NetCraft 解决方案（$config）=====" -ForegroundColor Cyan
        dotnet build $solution -c $config
        if ($LASTEXITCODE -eq 0) {
            $mainDll = Join-Path $root "NetCraft\bin\$config\net10.0\NetCraft.dll"
            if (Test-Path $mainDll) {
                $size = [math]::Round((Get-Item $mainDll).Length / 1024, 2)
                Write-Host ""
                Write-Host "===== 构建成功 =====" -ForegroundColor Green
                Write-Host "主库: $mainDll"
                Write-Host "大小: $size KB（内嵌所有子库）"

                Add-Type -AssemblyName System.Reflection -ErrorAction SilentlyContinue
                try {
                    $bytes = [System.IO.File]::ReadAllBytes($mainDll)
                    $asm = [System.Reflection.Assembly]::Load($bytes)
                    $resources = $asm.GetManifestResourceNames() | Where-Object { $_ -like "NetCraft.Embedded.*.dll" }
                    Write-Host "内嵌子库数量: $($resources.Count)"
                } catch {
                    Write-Warning "无法反射读取内嵌资源: $_"
                }
            }

            #检查 TPGA webui 资源是否被嵌入
            $tpgaDll = Join-Path $root "NetCraft.TPGA\bin\$config\net10.0\NetCraft.TPGA.dll"
            if (Test-Path $tpgaDll) {
                Add-Type -AssemblyName System.Reflection -ErrorAction SilentlyContinue
                try {
                    $bytes = [System.IO.File]::ReadAllBytes($tpgaDll)
                    $asm = [System.Reflection.Assembly]::Load($bytes)
                    $webResources = $asm.GetManifestResourceNames() | Where-Object { $_ -like "*.wwwroot.*" }
                    Write-Host "TPGA 内嵌 webui 资源数量: $($webResources.Count)"
                } catch {
                    Write-Warning "无法反射读取 TPGA 资源: $_"
                }
            }
        } else {
            Write-Error "构建失败"
            exit $LASTEXITCODE
        }
    }
}
