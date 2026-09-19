# This release check makes the public delivery list intentionally explicit.
# It checks the visitor-facing files only; source code, test output and private
# project material are never candidates for publication.

$ErrorActionPreference = 'Stop'
$siteRoot = Join-Path $PSScriptRoot '..\website'
$siteRoot = [IO.Path]::GetFullPath($siteRoot)
$htmlFiles = @(Get-ChildItem -LiteralPath $siteRoot -Filter '*.html' -File)

if ($htmlFiles.Count -eq 0) {
    throw 'No public HTML files were found.'
}

foreach ($file in $htmlFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $brokenEncoding = $content.IndexOf([char]0x00C3) -ge 0 -or
        $content.IndexOf([char]0x00C2) -ge 0 -or
        $content.IndexOf([char]0xFFFD) -ge 0
    if ($brokenEncoding -or
        $content -match 'file:|\?ber|f\?r|m\?ssen|l\?uft|g\?ltig|z\?hlt|fr\?her') {
        throw "Unsafe local path or broken text encoding in $($file.Name)."
    }
    if ($content -notmatch 'assets/my-nina-plugins-mark\.svg') {
        throw "The portal favicon is missing in $($file.Name)."
    }
    if ($content -notmatch 'assets/site\.css' -or $content -notmatch 'assets/site\.js') {
        throw "The shared portal layout is missing in $($file.Name)."
    }

    $references = [regex]::Matches($content, '(?:href|src)="([^\"]+)"')
    foreach ($referenceMatch in $references) {
        $reference = $referenceMatch.Groups[1].Value
        if ($reference -match '^[a-z]+:' -or $reference -match '^//') {
            continue
        }
        $target = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $reference))
        if (-not $target.StartsWith($siteRoot, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $target -PathType Leaf)) {
            throw "Broken local reference '$reference' in $($file.Name)."
        }
    }
}

$package = Join-Path $siteRoot 'downloads\SequencerItemRecovery.zip'
$checksumFile = Join-Path $siteRoot 'downloads\SequencerItemRecovery.zip.sha256'
$actualHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToUpperInvariant()
$declaredHash = (Get-Content -LiteralPath $checksumFile -Raw -Encoding ASCII).Split()[0].ToUpperInvariant()
if ($actualHash -ne $declaredHash) {
    throw 'The published ZIP package does not match its SHA-256 checksum file.'
}

$currentPackage = Join-Path $siteRoot 'downloads\SequencerItemRecovery-0.1.12.0.zip'
$currentChecksumFile = Join-Path $siteRoot 'downloads\SequencerItemRecovery-0.1.12.0.zip.sha256'
$currentActualHash = (Get-FileHash -LiteralPath $currentPackage -Algorithm SHA256).Hash.ToUpperInvariant()
$currentDeclaredHash = (Get-Content -LiteralPath $currentChecksumFile -Raw -Encoding ASCII).Split()[0].ToUpperInvariant()
if ($currentActualHash -ne $currentDeclaredHash) {
    throw 'The current ZIP package does not match its SHA-256 checksum file.'
}

Write-Output "Public website validation passed: $($htmlFiles.Count) HTML pages and ZIP SHA-256 files verified."
