# 从 deps.json 里摘掉内核程序集的登记。
# 留着的话运行时把它们当 TPA 项按路径找文件，而 TPA 命中却找不到文件时不会回退解析回调，
# 直接抛 FileNotFoundException；摘掉之后解析失败会落到 AssemblyLoadContext.Resolving，
# 由 EmbeddedAssemblyLoader 从 kernel 子目录或主库内嵌资源提供字节，模组改写器才有机会介入。
# 三处都要动：libraries 段、targets 段下该程序集自己的条目、以及别的条目对它的依赖声明，
# 少动一处 hostpolicy 就会按残留的依赖名去找那个已经不在列表里的程序集。
param(
    [Parameter(Mandatory = $true)][string]$DepsPath,
    [Parameter(Mandatory = $true)][string]$Keep
)

$keepList = $Keep -split ';' | Where-Object { $_ }
$json = Get-Content -Raw -Path $DepsPath | ConvertFrom-Json

$strip = @()
foreach ($key in $json.libraries.PSObject.Properties.Name) {
    $assembly = ($key -split '/')[0]
    if ($assembly -like 'NetCraft*' -and $keepList -notcontains $assembly) {
        $strip += $key
    }
}
if ($strip.Count -eq 0) { return }

foreach ($key in $strip) {
    $json.libraries.PSObject.Properties.Remove($key)
}

foreach ($frameworkName in @($json.targets.PSObject.Properties.Name)) {
    $target = $json.targets.$frameworkName
    foreach ($key in $strip) {
        if ($target.PSObject.Properties.Name -contains $key) {
            $target.PSObject.Properties.Remove($key)
        }
    }
    foreach ($entryName in @($target.PSObject.Properties.Name)) {
        $dependencies = $target.$entryName.dependencies
        if ($null -eq $dependencies) { continue }
        foreach ($name in @($dependencies.PSObject.Properties.Name)) {
            if ($name -like 'NetCraft*' -and $keepList -notcontains $name) {
                $dependencies.PSObject.Properties.Remove($name)
            }
        }
    }
}

# 不带 BOM 写出 带 BOM 的 JSON 宿主解析会失败
$text = $json | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($DepsPath, $text, (New-Object System.Text.UTF8Encoding $false))
