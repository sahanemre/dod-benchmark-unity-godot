<#
.SYNOPSIS
    S1 (toplu hareket) benchmark'ini dort konfigurasyonda gozetimsiz calistirir.

.DESCRIPTION
    Her (konfigurasyon, profil, [DOTS pipeline], N, tekrar) icin exe'yi AYRI
    bir surec olarak baslatir. Uygulama kisa bir bekleme sonrasi testi kendisi
    baslatir (3 s warmup + 10 s olcum), warmup'in son ~100 ms'sinde process
    private bytes'i okur, tek satirlik sonucunu kendi dosyasina yazar ve kapanir.

    Profiller:
      published  makaledeki kosullarin aynisi
      parity     dort konfigurasyonda esit goruntu/render kosullari (varsayilan)

    Siralama karisiktir: tekrar -> konfigurasyon -> N -> profil -> pipeline.
    Boylece termal kayma tek bir konfigurasyona/profile yigilmaz.

    Zaten gecerli sonuc dosyasi olan kosular atlanir (-Force ile yeniden alinir);
    yarida kesilirse ayni komutu tekrar calistirmak kaldigi yerden devam eder.
    Basarisiz kosular failures.log'a yazilir, digerleri etkilenmez.
    Sonda tum kosu dosyalari s1_runs.csv'de birlestirilir.

.EXAMPLE
    .\tools\run_s1.ps1
    Varsayilan: parity, 4 konfigurasyon x 5 N x 10 tekrar.

.EXAMPLE
    .\tools\run_s1.ps1 -Profile published,parity -N 1000,100000 -OutDir results\benchmarks\s1_ab_profile
    published ile parity'nin A/B karsilastirmasi.

.EXAMPLE
    .\tools\run_s1.ps1 -Configs unity-dots -DotsPipeline original,lean -N 1000,100000 -OutDir results\benchmarks\s1_ab_pipeline
    Yalnizca Unity DOTS render pipeline'inin etkisi (parity, geri kalan her sey ayni).

.EXAMPLE
    .\tools\run_s1.ps1 -Configs unity-oop -UnityApi d3d11,d3d12 -N 100000 -OutDir results\benchmarks\s1_ab_api
    Unity grafik API'sinin etkisi (parity, yalnizca API degisir).

.EXAMPLE
    .\tools\run_s1.ps1 -Configs godot-oop -N 50000 -RunIndex 7 -Force
    Tek bir kosuyu yeniden alir.
#>
[CmdletBinding()]
param(
    # Listeler metin olarak alinip asagida virgulden bolunur. Boylece hem
    # PowerShell icinden (-N 1000,5000) hem "powershell -File ... -N 1000,5000"
    # ile dogru calisir. (-File ile [int[]] kullanilsaydi "1000,5000" tek bir
    # metin olarak gelip sessizce 10005000 sayisina donusebilirdi.)
    [string[]]$Configs = @('unity-oop', 'unity-dots', 'godot-oop', 'godot-dod'),

    [string[]]$N = @('1000', '5000', '10000', '50000', '100000'),

    [string[]]$Profile = @('parity'),

    # Yalnizca unity-dots icin; bos = profilin varsayilani (published: original, parity: lean)
    [string[]]$DotsPipeline = @(),

    # Yalnizca Unity icin (d3d11, d3d12): -force-d3d11/12 ile API secilir.
    # Bos = profilin varsayilani (published: zorlanmaz, Unity'nin listesindeki
    # ilk API; parity: d3d12). Verilirse dosya adina API eklenir.
    [string[]]$UnityApi = @(),

    # Unity'nin kayit defterine yazdigi ekran ayarlarini (Screenmanager*) her
    # kosudan once SILME. Varsayilan: silinir (kosular birbirini etkilemesin).
    [switch]$KeepUnityScreenPrefs,

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
elseif (-not [System.IO.Path]::IsPathRooted($OutDir)) { $OutDir = Join-Path $RepoRoot $OutDir }

$LogDir     = Join-Path $OutDir 'logs'
$FailureLog = Join-Path $OutDir 'failures.log'
$MergedCsv  = Join-Path $OutDir 's1_runs.csv'
# v2: 16 sutun (graphics_api eklendi). v1 (15 sutun) dosyalar da gecerli sayilir;
# eksik graphics_api kosunun logundan okunur, log yoksa "unrecorded" yazilir.
$HeaderV1   = 'engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes,' +
              'window_mode,render_width,render_height,dots_color,profile,dots_pipeline,focus_lost'
$Header     = $HeaderV1 + ',graphics_api'
$ColumnCount = 16
$RunFileRx  = '^(unity-oop|unity-dots|godot-oop|godot-dod)_(published|parity)(_(original|lean))?(_(d3d11|d3d12))?_N\d+_run\d+\.csv$'

$ValidConfigs   = @('unity-oop', 'unity-dots', 'godot-oop', 'godot-dod')
$ValidCounts    = @(1000, 5000, 10000, 50000, 100000)
$ValidProfiles  = @('published', 'parity')
$ValidPipelines = @('original', 'lean')
$ValidApis      = @('d3d11', 'd3d12')

# Konfigurasyon -> motor (komut satiri bicimi motora gore degisir)
$EngineOf = @{
    'unity-oop'  = 'unity'
    'unity-dots' = 'unity'
    'godot-oop'  = 'godot'
    'godot-dod'  = 'godot'
}

# Unity player goruntu argumanlari: player ilk frame'den dogru modda acilsin.
# published: makaledeki kosul (borderless fullscreen, yerel 1920x1080).
# parity   : exclusive fullscreen 1920x1080.
# Grafik API ayri secilir (Get-UnityApi): published zorlanmaz, parity d3d12.
$UnityProfileArgs = @{
    'published' = '-screen-fullscreen 1 -window-mode borderless -screen-width 1920 -screen-height 1080'
    'parity'    = '-screen-fullscreen 1 -window-mode exclusive -screen-width 1920 -screen-height 1080'
}

# Uygulamalarin cikis kodlari (S1RunSession.cs / S1RunSession.gd ile ayni)
$ExitCodeText = @{
    2  = 'gecersiz arguman'
    3  = 'olcum penceresinde ornek yok'
    4  = 'private bytes okunamadi'
    5  = 'sonuc dosyasi yazilamadi'
    6  = 'motor/eklenti kurulumu basarisiz'
    7  = 'procmem eklentisi yuklenemedi'
    8  = 'goruntu modu/cozunurluk uygulanamadi ya da olcumde degisti'
    9  = 'gerekli shader build''de yok'
    10 = 'build/grafik API profille uyusmuyor'
}

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

function Assert-InSet([string[]]$Values, [string[]]$Valid, [string]$Name) {
    foreach ($v in $Values) {
        if ($Valid -notcontains $v) {
            throw "Gecersiz -$Name degeri: '$v'. Gecerli: $($Valid -join ', ')"
        }
    }
}

$Configs = Split-List $Configs
Assert-InSet $Configs $ValidConfigs 'Configs'
$Profiles = Split-List $Profile
Assert-InSet $Profiles $ValidProfiles 'Profile'
$Pipelines = Split-List $DotsPipeline
Assert-InSet $Pipelines $ValidPipelines 'DotsPipeline'
$Apis = Split-List $UnityApi
Assert-InSet $Apis $ValidApis 'UnityApi'
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

# unity-dots icin pipeline varyantlari; digerleri icin tek varyant ($null)
function Get-PipelineVariants([string]$Config, [string]$Prof) {
    if ($Config -ne 'unity-dots') { return ,@($null) }
    if ($Pipelines.Count -gt 0) { return ,$Pipelines }
    if ($Prof -eq 'parity') { return ,@('lean') }
    return ,@('original')
}

# Unity icin API varyantlari: -UnityApi verilmisse onlar, yoksa tek varyant ($null = profil varsayilani)
function Get-ApiVariants([string]$Config) {
    if ($EngineOf[$Config] -ne 'unity' -or $Apis.Count -eq 0) { return ,@($null) }
    return ,$Apis
}

# Kosuda zorlanacak API: acik secim, yoksa parity -> d3d12, published -> yok
function Get-UnityApi([string]$Prof, [string]$ApiVariant) {
    if ($ApiVariant) { return $ApiVariant }
    if ($Prof -eq 'parity') { return 'd3d12' }
    return $null
}

function Get-RunFileName([string]$Config, [string]$Prof, [string]$Pipeline, [string]$Api, [int]$EntityCount, [int]$Index) {
    $variant = $Prof
    if ($Pipeline) { $variant += "_$Pipeline" }
    if ($Api) { $variant += "_$Api" }
    return ('{0}_{1}_N{2}_run{3:D2}.csv' -f $Config, $variant, $EntityCount, $Index)
}

# v1 (15 sutun) dosyalar icin graphics_api'yi kosunun logundan oku.
function Get-ApiFromLog([string]$CsvPath) {
    $base = Join-Path (Join-Path (Split-Path -Parent $CsvPath) 'logs') ([System.IO.Path]::GetFileNameWithoutExtension($CsvPath))
    foreach ($log in @("$base.log", "$base.out.log")) {
        if (-not (Test-Path -LiteralPath $log -PathType Leaf)) { continue }
        $m = Select-String -LiteralPath $log -Pattern 'profile=\S+ .*?(graphics|driver)=(\S+)' | Select-Object -First 1
        if ($null -ne $m) {
            $v = $m.Matches[0].Groups[2].Value
            switch ($v) {
                'Direct3D11' { return 'd3d11' }
                'Direct3D12' { return 'd3d12' }
                default { return $v.ToLowerInvariant() }
            }
        }
    }
    return 'unrecorded'
}

# Unity'nin kayit defterindeki ekran ayarlari (HKCU\Software\<Sirket>\<Urun>,
# Screenmanager* degerleri): kosudan once loga dokulur ve (varsayilan) silinir.
# Sirket/urun adi build'in <Urun>_Data\app.info dosyasindan okunur.
function Reset-UnityScreenPrefs([string]$ExePath, [string]$DumpPath) {
    if ([Environment]::OSVersion.Platform -ne 'Win32NT') { return }
    $exeName = [System.IO.Path]::GetFileNameWithoutExtension($ExePath)
    $company = 'DefaultCompany'
    $product = $exeName
    $appInfo = Join-Path (Split-Path -Parent $ExePath) ($exeName + '_Data\app.info')
    if (Test-Path -LiteralPath $appInfo -PathType Leaf) {
        $info = @(Get-Content -LiteralPath $appInfo)
        if ($info.Count -ge 2) { $company = $info[0].Trim(); $product = $info[1].Trim() }
    }
    $key = "HKCU:\Software\$company\$product"
    $dump = New-Object System.Collections.Generic.List[string]
    $dump.Add("key: $key")
    if (Test-Path -LiteralPath $key) {
        $item = Get-Item -LiteralPath $key
        foreach ($name in ($item.GetValueNames() | Where-Object { $_ -like 'Screenmanager*' })) {
            $val = $item.GetValue($name)
            if ($val -is [byte[]]) { $val = ([System.Text.Encoding]::ASCII.GetString($val)).Trim([char]0) }
            $dump.Add(("{0} = {1}" -f $name, $val))
            if (-not $KeepUnityScreenPrefs) { Remove-ItemProperty -LiteralPath $key -Name $name }
        }
    }
    else { $dump.Add('(anahtar yok)') }
    $dump.Add($(if ($KeepUnityScreenPrefs) { '(korundu: -KeepUnityScreenPrefs)' } else { '(Screenmanager* silindi)' }))
    [System.IO.File]::WriteAllLines($DumpPath, $dump.ToArray())
}

function Write-Failure([string]$Name, [string]$Reason) {
    $line = "{0}`t{1}`t{2}" -f (Get-Date -Format 's'), $Name, $Reason
    Add-Content -LiteralPath $FailureLog -Value $line -Encoding UTF8
}

# Kosu dosyasini dogrular; gecerliyse veri satirini, degilse $null dondurur.
function Read-RunRow([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $lines = @(Get-Content -LiteralPath $Path | Where-Object { $_.Trim() -ne '' })
    if ($lines.Count -ne 2) { return $null }
    $head = $lines[0].Trim()
    $row = $lines[1].Trim()
    if ($head -eq $HeaderV1 -and $row.Split(',').Count -eq ($ColumnCount - 1)) {
        return $row + ',' + (Get-ApiFromLog $Path)
    }
    if ($head -ne $Header) { return $null }
    if ($row.Split(',').Count -ne $ColumnCount) { return $null }
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
            Profile  = $c[12]
            Pipeline = $c[13]
            Api      = $c[15]
            Count    = [int]$c[3]
            Run      = [int]$c[4]
            Line     = $row
        })
    }

    $out = New-Object System.Collections.Generic.List[string]
    $out.Add($Header)
    foreach ($r in ($rows | Sort-Object Profile, Engine, Paradigm, Pipeline, Api, Count, Run)) { $out.Add($r.Line) }

    # UTF-8 (BOM'suz): analiz scriptleri ve Excel icin sorunsuz
    [System.IO.File]::WriteAllLines($MergedCsv, $out.ToArray(), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("Birlestirildi: {0} satir -> {1}" -f $rows.Count, $MergedCsv)
}

function Resolve-BuildPath([object]$Section, [string]$Key, [string]$What) {
    $prop = $Section.PSObject.Properties[$Key]
    if ($null -eq $prop -or [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
        throw "s1_builds.json icinde $What icin yol yok ('$Key')."
    }
    $path = [Environment]::ExpandEnvironmentVariables([string]$prop.Value)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Dosya bulunamadi ($What): $path"
    }
    return (Resolve-Path -LiteralPath $path).Path
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

if ($MergeOnly) {
    Merge-Results
    return
}

# ---- exe yollari (profile gore) -------------------------------------------
if (-not (Test-Path -LiteralPath $BuildsFile -PathType Leaf)) {
    throw "Build dosyasi bulunamadi: $BuildsFile"
}
$builds = Get-Content -LiteralPath $BuildsFile -Raw | ConvertFrom-Json
$Exe = @{}
foreach ($prof in $Profiles) {
    $sectionProp = $builds.PSObject.Properties[$prof]
    if ($null -eq $sectionProp) { throw "s1_builds.json icinde '$prof' bolumu yok." }
    foreach ($cfg in $Configs) {
        $Exe["$prof|$cfg"] = Resolve-BuildPath $sectionProp.Value $cfg "$prof / $cfg"
    }
}

# published Godot DOD: makaledeki debug export'un yukledigi GDExtension DLL'inin
# AYNISI olmali. Export klasorundeki DLL, eski build klasorundeki referansla
# karsilastirilir (SHA-256).
if ($Profiles -contains 'published' -and $Configs -contains 'godot-dod') {
    $refPath = Resolve-BuildPath $builds 'published_godot_dod_reference_dll' 'published Godot DOD referans DLL'
    $dllName = [System.IO.Path]::GetFileName($refPath)
    $usedPath = Join-Path (Split-Path -Parent $Exe['published|godot-dod']) $dllName
    if (-not (Test-Path -LiteralPath $usedPath -PathType Leaf)) {
        throw "published Godot DOD export klasorunde $dllName yok: $usedPath"
    }
    $refHash  = (Get-FileHash -LiteralPath $refPath -Algorithm SHA256).Hash
    $usedHash = (Get-FileHash -LiteralPath $usedPath -Algorithm SHA256).Hash
    if ($refHash -ne $usedHash) {
        throw ("published Godot DOD, makaledeki DLL'i kullanmiyor.`n  referans: {0}`n  export  : {1}`n" +
               "Referans DLL'i export klasorune kopyalayin (bkz. tools/S1_CALISTIRMA.md).") -f $refPath, $usedPath
    }
    Write-Host "published Godot DOD DLL dogrulandi (SHA-256 makaledeki ile ayni)."
}

# ---- kosu plani ------------------------------------------------------------
if ($null -ne $RunIndices) { $indices = $RunIndices } else { $indices = @(1..$Runs) }

$plan = New-Object System.Collections.Generic.List[object]
$skipped = 0
foreach ($k in $indices) {
    foreach ($cfg in $Configs) {
        foreach ($count in $Counts) {
            foreach ($prof in $Profiles) {
                foreach ($pipe in (Get-PipelineVariants $cfg $prof)) {
                    foreach ($api in (Get-ApiVariants $cfg)) {
                        $name = Get-RunFileName $cfg $prof $pipe $api $count $k
                        $file = Join-Path $OutDir $name
                        if (-not $Force -and $null -ne (Read-RunRow $file)) { $skipped++; continue }
                        $plan.Add([pscustomobject]@{
                            Config = $cfg; Profile = $prof; Pipeline = $pipe; Api = $api
                            Count = $count; Run = $k; Out = $file; Name = $name
                        })
                    }
                }
            }
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
$focusCount = 0
$started = Get-Date

try {
    for ($i = 0; $i -lt $plan.Count; $i++) {
        $job = $plan[$i]
        $exePath = $Exe["$($job.Profile)|$($job.Config)"]
        $tmp = $job.Out + '.tmp'
        foreach ($stale in @($job.Out, $tmp)) {
            if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Force }
        }

        $logBase = Join-Path $LogDir ([System.IO.Path]::GetFileNameWithoutExtension($job.Out))
        $outArg = $job.Out -replace '\\', '/'
        $label = $job.Profile
        if ($job.Pipeline) { $label += "/$($job.Pipeline)" }
        if ($job.Api) { $label += "/$($job.Api)" }

        Write-Host -NoNewline ("[{0}/{1}] {2,-10} {3,-18} N={4,-6} run={5,-3} ... " -f `
            ($i + 1), $plan.Count, $job.Config, $label, $job.Count, $job.Run)
        $t0 = Get-Date

        # -batchmode/headless YOK (render is yukunun parcasi).
        $appArgs = '--profile={0} --n={1} --run-index={2} --idle-delay={3} --out="{4}"' -f `
            $job.Profile, $job.Count, $job.Run, $idleArg, $outArg
        if ($job.Pipeline) { $appArgs += " --dots-pipeline=$($job.Pipeline)" }

        if ($EngineOf[$job.Config] -eq 'unity') {
            # Onceki kosudan kalan ekran ayarlari bu kosuyu etkilemesin
            Reset-UnityScreenPrefs $exePath "$logBase.registry.txt"
            $unityArgs = $UnityProfileArgs[$job.Profile]
            $forceApi = Get-UnityApi $job.Profile $job.Api
            if ($forceApi) {
                $unityArgs += " -force-$forceApi"
                $appArgs += " --graphics-api=$forceApi"
            }
            $argLine = '{0} -logFile "{1}.log" {2}' -f $unityArgs, $logBase, $appArgs
            $p = Start-Process -FilePath $exePath -ArgumentList $argLine -PassThru `
                -WorkingDirectory (Split-Path -Parent $exePath)
        }
        else {
            # Godot kullanici argumanlari "--" sonrasina gelir
            $argLine = '-- ' + $appArgs
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
            $focusNote = ''
            if ($c[14] -eq '1') { $focusNote = '  ODAK KAYBI'; $focusCount++ }
            Write-Host ("OK  {0,9} ms  {1,13:N0} B  {2} {3}x{4} {5}  ({6:N0} s){7}" -f `
                $c[5], [int64]$c[7], $c[8], $c[9], $c[10], $c[15], $secs, $focusNote) `
                -ForegroundColor $(if ($focusNote) { 'Yellow' } else { 'Green' })
            $okCount++
        }
        else {
            Write-Host ("HATA: {0}" -f $reason) -ForegroundColor Red
            Write-Failure $job.Name $reason
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
if ($focusCount -gt 0) {
    Write-Host ("Odak kaybi olan kosu: {0} (CSV'de focus_lost=1). Yeniden almak icin -RunIndex ve -Force." -f $focusCount) -ForegroundColor Yellow
}
if ($failCount -gt 0) {
    Write-Host "Basarisiz kosular: $FailureLog" -ForegroundColor Yellow
    Write-Host "Yeniden denemek icin ayni komutu tekrar calistirin (eksik kosular alinir)." -ForegroundColor Yellow
}

Merge-Results

if ($failCount -gt 0) { exit 1 }
