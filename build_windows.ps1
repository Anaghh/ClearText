$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework 4.x x64 compiler is required.' }
$versionMatch = [regex]::Match((Get-Content -LiteralPath 'native\ClearText.cs' -Raw), 'const string Version = "([^"]+)"')
$releaseVersion = $versionMatch.Groups[1].Value
if (-not $releaseVersion) { throw 'Cannot determine application version.' }
New-Item -ItemType Directory -Path dist,release,previews -Force | Out-Null
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+', '/out:dist\ClearText.exe', '/win32manifest:native\app.manifest', '/resource:native\MainWindow.xaml,MainWindow.xaml',
    "/reference:$frameworkPath\WPF\PresentationFramework.dll", "/reference:$frameworkPath\WPF\PresentationCore.dll", "/reference:$frameworkPath\WPF\WindowsBase.dll", "/reference:$frameworkPath\System.Xaml.dll", '/reference:System.Core.dll', '/reference:System.Xml.Linq.dll', 'native\ClearText.cs','native\NativeTests.cs')
& $compilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Native Windows compilation failed.' }
$testProcess = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\ClearText.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(30000)) { $testProcess.Kill(); throw 'Native tests timed out.' }
Get-Content -LiteralPath 'dist\self-test-results.txt'
if ($testProcess.ExitCode -ne 0) { throw 'Native tests failed.' }
$binaryZip = Join-Path $PSScriptRoot "release\ClearText-$releaseVersion-Windows-x64.zip"
Compress-Archive -LiteralPath 'dist\ClearText.exe','README.txt','TUTORIAL.md','LICENSE','THIRD_PARTY_NOTICES.md' -DestinationPath $binaryZip -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourceZip = Join-Path $PSScriptRoot "release\ClearText-$releaseVersion-Source.zip"
if (Test-Path -LiteralPath $sourceZip) { Remove-Item -LiteralPath $sourceZip }
$sourceFiles = @('native\MainWindow.xaml','native\ClearText.cs','native\NativeTests.cs','native\app.manifest','build_windows.ps1','Launch ClearText.cmd','README.md','README.txt','TUTORIAL.md','LICENSE','THIRD_PARTY_NOTICES.md','CONTRIBUTING.md','DESIGN_RESEARCH.md','.gitignore','.github\workflows\windows.yml','docs\images\cleartext-light.png','docs\images\cleartext-dark.png')
$archive = [System.IO.Compression.ZipFile]::Open($sourceZip, 'Create')
try {
    foreach ($sourceFile in $sourceFiles) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $PSScriptRoot $sourceFile), ('ClearText/' + $sourceFile.Replace('\','/'))) | Out-Null
    }
} finally { $archive.Dispose() }
$checksums = @($binaryZip,$sourceZip) | ForEach-Object { $digest = Get-FileHash -Algorithm SHA256 -LiteralPath $_; "$($digest.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($_))" }
$checksums | Set-Content -LiteralPath 'release\SHA256SUMS.txt' -Encoding ascii
$checksums
