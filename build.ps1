# FogForge build script (Windows, .NET Framework 4.x compiler + Windows SDK rc.exe)
#   ./build.ps1 -Stage app                      -> out/FogForge.exe
#   ./build.ps1 -Stage setup -AppExe out/FogForge.exe   -> out/FogForge Setup.exe (embeds the given app exe)
#   ./build.ps1                                 -> both
param(
  [ValidateSet("all", "app", "setup")] [string]$Stage = "all",
  [string]$AppExe = "",
  [string]$Out = "out"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root "src"
$out  = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }
$obj  = Join-Path $out "obj"
New-Item -ItemType Directory -Force -Path $out, $obj | Out-Null

# Version comes from src/DbdPackManager.cs (AppInfo.Version) - single source of truth
$m = Select-String -Path (Join-Path $src "DbdPackManager.cs") -Pattern 'public const string Version = "(\d+)\.(\d+)\.(\d+)"' | Select-Object -First 1
if (-not $m) { throw "AppInfo.Version not found" }
$v = $m.Matches[0].Groups; $ver = "$($v[1].Value).$($v[2].Value).$($v[3].Value)"
Write-Host "FogForge version $ver"

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "C# compiler (.NET Framework 4) not found" }
$rc = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter rc.exe -ErrorAction SilentlyContinue |
      Where-Object { $_.FullName -match "\\x64\\" } | Sort-Object FullName | Select-Object -Last 1
if (-not $rc) { throw "rc.exe (Windows SDK) not found" }

$refs = @("/r:System.dll", "/r:System.Core.dll", "/r:System.Drawing.dll", "/r:System.Windows.Forms.dll", "/r:System.IO.Compression.dll", "/r:System.IO.Compression.FileSystem.dll", "/r:Microsoft.VisualBasic.dll")
$common = @("DbdPackManager.cs", "Sprachen.cs", "Payload.cs", "Remote.cs") | ForEach-Object { Join-Path $src $_ }

function Res($name) {
  # copy .rc with the current version, compile to .res
  $text = Get-Content -Raw -Encoding UTF8 (Join-Path $src "$name.rc")
  $comma = $ver.Replace(".", ",") + ",0"
  $text = $text -replace 'FILEVERSION [\d,]+', "FILEVERSION $comma" -replace 'PRODUCTVERSION [\d,]+', "PRODUCTVERSION $comma"
  $text = $text -replace '"FileVersion", "[\d.]+"', "`"FileVersion`", `"$ver.0`"" -replace '"ProductVersion", "[\d.]+"', "`"ProductVersion`", `"$ver.0`""
  $rcFile = Join-Path $src "$name.build.rc"
  [IO.File]::WriteAllText($rcFile, $text, (New-Object Text.UTF8Encoding($false)))
  $resFile = Join-Path $obj "$name.res"
  Push-Location $src
  try { & $rc.FullName /nologo /c 65001 /fo $resFile $rcFile | Out-Host; if ($LASTEXITCODE -ne 0) { throw "rc failed for $name" } }
  finally { Pop-Location; Remove-Item $rcFile -Force }
  return $resFile
}

if ($Stage -in @("all", "app")) {
  $res = Res "app"
  $exe = Join-Path $out "FogForge.exe"
  & $csc /nologo /target:winexe /optimize+ "/out:$exe" "/win32res:$res" "/resource:$(Join-Path $src 'logo.png'),logo.png" "/resource:$(Join-Path $src 'banner.jpg'),banner.jpg" @refs @common
  if ($LASTEXITCODE -ne 0) { throw "Building FogForge.exe failed" }
  Write-Host "Built $exe"
  if ($AppExe -eq "") { $AppExe = $exe }
}

if ($Stage -in @("all", "setup")) {
  if ($AppExe -eq "") { $AppExe = Join-Path $out "FogForge.exe" }
  if (-not (Test-Path $AppExe)) { throw "App exe not found: $AppExe" }
  $res = Res "setup"
  $embedded = Join-Path $obj "app.exe"
  Copy-Item $AppExe $embedded -Force
  $setup = Join-Path $out "FogForge Setup.exe"
  & $csc /nologo /target:winexe /optimize+ /main:DbdCustom.SetupProgram "/out:$setup" "/win32res:$res" "/resource:$embedded,app.exe" "/resource:$(Join-Path $src 'logo.png'),logo.png" "/resource:$(Join-Path $src 'banner.jpg'),banner.jpg" @refs @common (Join-Path $src "Installer.cs")
  if ($LASTEXITCODE -ne 0) { throw "Building the installer failed" }
  Write-Host "Built $setup"
}
