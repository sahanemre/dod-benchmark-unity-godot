<#
.SYNOPSIS
    S1 (toplu hareket) benchmark'ini dort konfigurasyonda gozetimsiz calistirir.

.DESCRIPTION
    Her (konfigurasyon, N, tekrar) icin exe'yi AYRI bir surec olarak baslatir.
    Uygulama kisa bir bekleme sonrasi testi kendisi baslatir (3 s warmup +
    10 s olcum), warmup bitince process private bytes'i bir kez okur, tek
    satirlik sonucunu kendi dosyasina yazar ve kapanir.

    Siralama karisiktir: tekrar -> konfigurasyon -> N. Boylece termal kayma
    tek bir konfigurasyona yigilmaz.

    Zaten sonuc dosyasi olan kosular atlanir (-Force ile yeniden alinir);
    yarida kesilirse ayni komutu tekrar calistirmak kaldigi yerden devam eder.
    Basarisiz kosular failures.log'a yazilir, digerleri etkilenmez.
    Sonda tum kosu dosyalari s1_runs.csv'de birlestirilir.

.EXAMPLE
    .\tools\run_s1.ps1
    Varsayilan: 4 konfigurasyon x 5 N x 10 tekrar.

.EXAMPLE
    .\tools\run_s1.ps1 -Configs godot-oop -N 50000
    Yalnizca Godot OOP, N=50000 (eksik tekrarlar).

.EXAMPLE
    .\tools\run_s1.ps1 -Configs unity-dots -N 100000 -RunIndex 7 -Force
    Tek bir kosuyu yeniden alir.

.EXAMPLE
    .\tools\run_s1.ps1 -MergeOnly
    Yalnizca birlestirme yapar.
#>
[CmdletBinding()]
param(
    # Listeler metin olarak alinip asagida virgulden bolunur. Boylece hem
    # PowerShell icinden (-N 1000,5000) hem "powershell -File ... -N 1000,5000"
    # ile dogru calisir. (-File ile [int[]] kullanilsaydi "1000,5000" tek bir
    # metin olarak gelip sessizce 10005000 sayisina donusebilirdi.)
    [string[]]$Configs = @('unity-oop', 'unity-dots', 'godot-oop', 'godot-dod'),

    [string[]]$N = @('1000', '5000', '10000', '50000', '100000'),

    [ValidateRange(1, 1000)]
    [int]$Runs = 10,

    # Verilirse -Runs yerine yalnizca bu tekrar numaralari calistirilir.
    [string[]]$RunIndex,

    [switch]$Force,
    [switch]$MergeOnly,

    [ValidateRange(20, 3600)]
    [int]$TimeoutSec = 120,

    [ValidateRange(0, 600)]
    [int]$CooldownSec = 5,

    [ValidateRange(0.0, 60.0)]
    [double]$IdleDelaySec = 2.0,

    [string]$BuildsFile,
    [string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $BuildsFile) { $BuildsFile = Join-Path $PSScriptRoot 's1_builds.json' }
if (-not $OutDir)     { $OutDir = Join-Path $RepoRoot 'results\benchmarks\s1' }

$LogDir     = Join-Path $OutDir 'logs'
$FailureLog = Join-Path $OutDir 'failures.log'
$MergedCsv  = Join-Path $OutDir 's1_runs.csv'
$Header     = 'engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes'
$RunFileRx  = '^(unity-oop|unity-dots|godot-oop|godot-dod)_N\d+_run\d+\.csv$'

# Konfigurasyon -> motor (komut satiri bicimi motora gore degisir)
$EngineOf = @{
    'unity-oop'  = 'unity'
    'unity-dots' = 'unity'
    'godot-oop'  = 'godot'
    'godot-dod'  = 'godot'
}

# Uygulamalarin cikis kodlari (BenchmarkManager / DOTSBenchmarkSpawner /
# BenchmarkOrchestrator ile ayni)
$ExitCodeText = @{
    2 = 'gecersiz arguman'
    3 = 'olcum penceresinde ornek yok'
    4 = 'private bytes okunamadi'
    5 = 'sonuc dosyasi yazilamadi'
    6 = 'motor/eklenti kurulumu basarisiz'
    7 = 'procmem eklentisi yuklenemedi'
}

$ValidConfigs = @('unity-oop', 'unity-dots', 'godot-oop', 'godot-dod')
$ValidCounts  = @(1000, 5000, 10000, 50000, 100000)

# "a,b" ya da @('a','b') -> @('a','b')
function Split-List([string[]]$Values) {
    $items = New-Object System.Collections.Generic.List[string]
    foreach ($v in $Values) {
        foreach ($part in ($v -split ',')) {
            $t = $part.Trim()
            if ($t -ne '') { $items.Add($t) }
        }
    }
    return ,$items.ToArray()
}

function ConvertTo-IntList([string[]]$Values, [string]$Name) {
    $result = New-Object System.Collections.Generic.List[int]
    foreach ($t in (Split-List $Values)) {
        $num = 0
        if (-not [int]::TryParse($t, [System.Globalization.NumberStyles]::None,
                [System.Globalization.CultureInfo]::InvariantCulture, [ref]$num)) {
            throw "-$Name icin gecersiz deger: '$t' (tam sayi olmali)"
        }
        $result.Add($num)
    }
    return ,$result.ToArray()
}

$Configs = Split-List $Configs
foreach ($cfg in $Configs) {
    if ($ValidConfigs -notcontains $cfg) {
        throw "Gecersiz konfigurasyon: '$cfg'. Gecerli: $($ValidConfigs -join ', ')"
    }
}
$Counts = ConvertTo-IntList $N 'N'
foreach ($count in $Counts) {
    if ($ValidCounts -notcontains $count) {
        throw "Gecersiz N: $count. Gecerli: $($ValidCounts -join ', ')"
    }
}
$RunIndices = $null
if ($RunIndex) {
    $RunIndices = ConvertTo-IntList $RunIndex 'RunIndex'
    foreach ($k in $RunIndices) {
        if ($k -lt 1) { throw "Gecersiz -RunIndex: $k (1 veya daha buyuk olmali)" }
    }
}

function Get-RunFileName([string]$Config, [int]$EntityCount, [int]$Index) {
    return ('{0}_N{1}_run{2:D2}.csv' -f $Config, $EntityCount, $Index)
}

function Write-Failure([string]$Config, [int]$EntityCount, [int]$Index, [string]$Reason) {
    $line = "{0}`t{1}`tN={2}`trun={3}`t{4}" -f (Get-Date -Format 's'), $Config, $EntityCount, $Index, $Reason
    Add-Content -LiteralPath $FailureLog -Value $line -Encoding UTF8
}

# Kosu dosyasini dogrular; gecerliyse veri satirini, degilse $null dondurur.
function Read-RunRow([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $lines = @(Get-Content -LiteralPath $Path | Where-Object { $_.Trim() -ne '' })
    if ($lines.Count -ne 2) { return $null }
    if ($lines[0].Trim() -ne $Header) { return $null }
    $row = $lines[1].Trim()
    if ($row.Split(',').Count -ne 8) { return $null }
    return $row
}

function Merge-Results {
    $rows = New-Object System.Collections.Generic.List[object]
    $files = @(Get-ChildItem -LiteralPath $OutDir -File -ErrorAction SilentlyContinue |
               Where-Object { $_.Name -match $RunFileRx })
    foreach ($f in $files) {
        $row = Read-RunRow $f.FullName
        if ($null -eq $row) {
            Write-Warning "Atlandi (beklenmeyen bicim): $($f.Name)"
            continue
        }
        $c = $row.Split(',')
        $rows.Add([pscustomobject]@{
            Engine   = $c[0]
            Paradigm = $c[1]
            Count    = [int]$c[3]
            Run      = [int]$c[4]
            Line     = $row
        })
    }

    $out = New-Object System.Collections.Generic.List[string]
    $out.Add($Header)
    foreach ($r in ($rows | Sort-Object Engine, Paradigm, Count, Run)) { $out.Add($r.Line) }

    # UTF-8 (BOM'suz): analiz scriptleri ve Excel icin sorunsuz
    [System.IO.File]::WriteAllLines($MergedCsv, $out.ToArray(), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("Birlestirildi: {0} satir -> {1}" -f $rows.Count, $MergedCsv)
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

if ($MergeOnly) {
    Merge-Results
    return
}

# ---- exe yollari ---------------------------------------------------------
if (-not (Test-Path -LiteralPath $BuildsFile -PathType Leaf)) {
    throw "Build dosyasi bulunamadi: $BuildsFile"
}
$builds = Get-Content -LiteralPath $BuildsFile -Raw | ConvertFrom-Json
$Exe = @{}
foreach ($cfg in $Configs) {
    $prop = $builds.PSObject.Properties[$cfg]
    if ($null -eq $prop -or [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
        throw "s1_builds.json icinde '$cfg' icin exe yolu yok."
    }
    $path = [Environment]::ExpandEnvironmentVariables([string]$prop.Value)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Exe bulunamadi ($cfg): $path"
    }
    $Exe[$cfg] = (Resolve-Path -LiteralPath $path).Path
}

# ---- kosu plani (karisik sira: tekrar -> konfigurasyon -> N) -------------
if ($null -ne $RunIndices) { $indices = $RunIndices } else { $indices = @(1..$Runs) }

$plan = New-Object System.Collections.Generic.List[object]
$skipped = 0
foreach ($k in $indices) {
    foreach ($cfg in $Configs) {
        foreach ($count in $Counts) {
            $file = Join-Path $OutDir (Get-RunFileName $cfg $count $k)
            if (-not $Force -and $null -ne (Read-RunRow $file)) { $skipped++; continue }
            $plan.Add([pscustomobject]@{ Config = $cfg; Count = $count; Run = $k; Out = $file })
        }
    }
}

# Tahmini sure: acilis (~6 s) + bekleme + 13 s test + sogutma
$perRun = 6 + $IdleDelaySec + 13 + $CooldownSec
Write-Host ("Planlanan kosu: {0} | atlanan (zaten var): {1} | tahmini sure: ~{2:N0} dk" -f `
    $plan.Count, $skipped, ($plan.Count * $perRun / 60))
Write-Host "Sonuclar: $OutDir"
if ($plan.Count -eq 0) {
    Merge-Results
    return
}

# Olcum sirasinda bilgisayarin uyumasini / ekranin kapanmasini engelle
if (-not ('S1Bench.Power' -as [type])) {
    Add-Type -Namespace S1Bench -Name Power -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern uint SetThreadExecutionState(uint esFlags);
'@
}
# ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
[S1Bench.Power]::SetThreadExecutionState([uint32]2147483651) | Out-Null

$idleArg = $IdleDelaySec.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$okCount = 0
$failCount = 0
$started = Get-Date

try {
    for ($i = 0; $i -lt $plan.Count; $i++) {
        $job = $plan[$i]
        $exePath = $Exe[$job.Config]
        $tmp = $job.Out + '.tmp'
        foreach ($stale in @($job.Out, $tmp)) {
            if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Force }
        }

        $logBase = Join-Path $LogDir ([System.IO.Path]::GetFileNameWithoutExtension($job.Out))
        $outArg = $job.Out -replace '\\', '/'

        Write-Host -NoNewline ("[{0}/{1}] {2,-10} N={3,-6} run={4,-3} ... " -f `
            ($i + 1), $plan.Count, $job.Config, $job.Count, $job.Run)
        $t0 = Get-Date

        # Pencere/cozunurluk normal acilistaki gibi; -batchmode/headless YOK
        # (render is yukunun parcasi).
        if ($EngineOf[$job.Config] -eq 'unity') {
            $argLine = '-logFile "{0}.log" --n {1} --run-index {2} --idle-delay {3} --out "{4}"' -f `
                $logBase, $job.Count, $job.Run, $idleArg, $outArg
            $p = Start-Process -FilePath $exePath -ArgumentList $argLine -PassThru `
                -WorkingDirectory (Split-Path -Parent $exePath)
        }
        else {
            # Godot kullanici argumanlari "--" sonrasina gelir
            $argLine = '-- --n={0} --run-index={1} --idle-delay={2} --out="{3}"' -f `
                $job.Count, $job.Run, $idleArg, $outArg
            $p = Start-Process -FilePath $exePath -ArgumentList $argLine -PassThru `
                -WorkingDirectory (Split-Path -Parent $exePath) `
                -RedirectStandardOutput "$logBase.out.log" -RedirectStandardError "$logBase.err.log"
        }
        $null = $p.Handle   # PowerShell 5.1: ExitCode'un okunabilmesi icin gerekli

        $reason = $null
        if (-not $p.WaitForExit($TimeoutSec * 1000)) {
            try { $p.Kill() } catch { }
            $null = $p.WaitForExit(10000)
            $reason = "zaman asimi (${TimeoutSec}s), surec sonlandirildi"
        }
        else {
            $p.WaitForExit()   # yonlendirilen ciktilarin bosalmasi icin
            $code = $p.ExitCode
            if ($code -ne 0) {
                $desc = if ($ExitCodeText.ContainsKey([int]$code)) { $ExitCodeText[[int]$code] } else { 'beklenmeyen hata / cokme' }
                $reason = "cikis kodu $code ($desc)"
            }
            elseif ($null -eq (Read-RunRow $job.Out)) {
                $reason = 'cikis kodu 0 ama gecerli sonuc dosyasi yok'
            }
        }

        $secs = ((Get-Date) - $t0).TotalSeconds
        if ($null -eq $reason) {
            $c = (Read-RunRow $job.Out).Split(',')
            Write-Host ("OK  {0,8} ms  {1,10:N0} B  ({2:N0} s)" -f $c[5], [int64]$c[7], $secs) -ForegroundColor Green
            $okCount++
        }
        else {
            Write-Host ("HATA: {0}" -f $reason) -ForegroundColor Red
            Write-Failure $job.Config $job.Count $job.Run $reason
            if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
            $failCount++
        }

        if ($i -lt $plan.Count - 1 -and $CooldownSec -gt 0) {
            Start-Sleep -Seconds $CooldownSec
        }
    }
}
finally {
    # Uyku ayarlarini eski haline getir (ES_CONTINUOUS)
    [S1Bench.Power]::SetThreadExecutionState([uint32]2147483648) | Out-Null
}

$elapsed = (Get-Date) - $started
Write-Host ""
Write-Host ("Bitti: {0} basarili, {1} basarisiz, {2} atlandi | sure: {3:hh\:mm\:ss}" -f `
    $okCount, $failCount, $skipped, $elapsed)
if ($failCount -gt 0) {
    Write-Host "Basarisiz kosular: $FailureLog" -ForegroundColor Yellow
    Write-Host "Yeniden denemek icin ayni komutu tekrar calistirin (eksik kosular alinir)." -ForegroundColor Yellow
}

Merge-Results

if ($failCount -gt 0) { exit 1 }
