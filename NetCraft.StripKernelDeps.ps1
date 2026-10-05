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
$text = $json | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($DepsPath, $text, (New-Object System.Text.UTF8Encoding $false))
