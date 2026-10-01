param(
    [Parameter(Mandatory = $true)]
    [string]$LayoutPath,
    [switch]$Remove
)

# Insert / remove the "Random Student Picker" component in a ClassIsland component layout file.
# The layout file is usually located at:
#   <ClassIsland data folder>/Config/ComponentLayouts/Default.json
# ASCII-only messages so Windows PowerShell 5.1 can parse this file regardless of encoding.

$componentId = "7a4f3c61-2b8e-4d95-9f32-5c1e86b7a0d4"
$settingsJson = '{"ButtonText":"\u968F\u673A\u62BD\u53D6","FontSize":16,"ShowIcon":true,"ShowResultInComponent":true,"FontColor":"#FFFFFFFF","UseCustomFontColor":false,"Spacing":4}'

$newComponent = '{"Id":"' + $componentId + '","NameCache":"","Settings":' + $settingsJson + ',"HideOnRule":false,"HidingRules":{"Mode":0,"IsReversed":false,"Groups":[{"Rules":[{"IsReversed":false,"Id":"","Settings":null,"IsActive":false}],"Mode":1,"IsReversed":false,"IsEnabled":true,"IsActive":false}],"IsActive":false},"IsResourceOverridingEnabled":false,"MainWindowSecondaryFontSize":14,"MainWindowBodyFontSize":16,"MainWindowEmphasizedFontSize":18,"MainWindowLargeFontSize":20,"IsCustomForegroundColorEnabled":false,"ForegroundColor":"#1E90FFFF","BackgroundOpacity":0.5,"IsCustomBackgroundOpacityEnabled":false,"BackgroundColor":"#000000FF","IsCustomBackgroundColorEnabled":false,"CustomCornerRadius":8,"IsCustomCornerRadiusEnabled":false,"Opacity":1,"RelativeLineNumber":0,"IsMinWidthEnabled":false,"MinWidth":100,"IsMaxWidthEnabled":false,"MaxWidth":300,"IsFixedWidthEnabled":false,"FixedWidth":200,"HorizontalAlignment":0,"IsCustomMarginEnabled":false,"MarginLeft":0,"MarginTop":0,"MarginRight":0,"MarginBottom":0,"LastWidthCache":80,"IsActive":false}'

if (-not (Test-Path $LayoutPath)) {
    throw "Layout file not found: $LayoutPath"
}

$raw = Get-Content $LayoutPath -Raw

if ($Remove) {
    $index = $raw.IndexOf('{"Id":"' + $componentId + '"')
    if ($index -lt 0) {
        Write-Host "Component not present, nothing to remove."
        return
    }

    $depth = 0
    $end = $index
    for ($i = $index; $i -lt $raw.Length; $i++) {
        $c = $raw[$i]
        if ($c -eq '{') { $depth++ }
        elseif ($c -eq '}') {
            $depth--
            if ($depth -eq 0) { $end = $i; break }
        }
    }

    $raw = $raw.Remove($index, $end - $index + 1)
    if ($index -lt $raw.Length -and $raw[$index] -eq ',') {
        $raw = $raw.Remove($index, 1)
    }
    elseif ($index -gt 0 -and $raw[$index - 1] -eq ',') {
        $raw = $raw.Remove($index - 1, 1)
    }

    Copy-Item $LayoutPath "$LayoutPath.bak" -Force
    Set-Content -Path $LayoutPath -Value $raw -NoNewline -Encoding UTF8
    Write-Host "Removed the picker component from the layout."
    return
}

if ($raw.Contains('{"Id":"' + $componentId + '"')) {
    Write-Host "Component already present in the layout."
    return
}

$anchor = '{"Id":"df3f8295-21f6-482e-bada-fa0e5f14bb66","NameCache":"","Settings":null,'
if (-not $raw.Contains($anchor)) {
    throw "Anchor not found; please drag the component onto the main window manually."
}

$raw = $raw.Replace($anchor, $newComponent + "," + $anchor)
Copy-Item $LayoutPath "$LayoutPath.bak" -Force
Set-Content -Path $LayoutPath -Value $raw -NoNewline -Encoding UTF8
Write-Host "Inserted the picker component at the head of the first main window line."
