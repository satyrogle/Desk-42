# ============================================================
# DESK 42 — technical pipeline verification asset generator
#
# Emits TECH_PIPELINE_TEST_NONPRODUCTION.wav: a short, neutral sine tone
# whose ONLY purpose is to prove the transport chain
#
#   audio source -> FMOD Studio -> event -> bank
#                -> Unity FMOD integration -> FmodAudioBackend -> output
#
# THIS IS NOT PRODUCTION AUDIO.
#   - It is not the Venn motif.
#   - No narrative identity is derived from it.
#   - It must never back event:/Proof/EliasRegistration18A, which stays an
#     intentionally unfilled slot until AudioLab delivers authored content.
#
# The generator is tracked, not the .wav: output is pure deterministic math
# with no RNG and no timestamp, so every run on any machine reproduces the
# same bytes and therefore the same SHA-256 recorded in the provenance file.
# ============================================================

param(
    # Defaults are the tone's definition, not incidental values — changing any
    # of them changes the asset hash and must be reflected in provenance.
    [string]$OutputPath        = (Join-Path $PSScriptRoot "assets\TECH_PIPELINE_TEST_NONPRODUCTION.wav"),
    [int]   $SampleRate        = 48000,
    [double]$FrequencyHz       = 1000.0,
    [double]$DurationSeconds   = 0.40,
    [double]$AmplitudeDbfs     = -12.0,
    [double]$FadeMilliseconds  = 5.0
)

$ErrorActionPreference = "Stop"

$OutputDir = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$TotalSamples = [int][Math]::Round($SampleRate * $DurationSeconds)
$FadeSamples  = [int][Math]::Round($SampleRate * ($FadeMilliseconds / 1000.0))
$Peak         = [Math]::Pow(10.0, $AmplitudeDbfs / 20.0) * 32767.0

# 16-bit mono PCM.
$BitsPerSample = 16
$Channels      = 1
$BytesPerSample = $BitsPerSample / 8
$DataBytes     = $TotalSamples * $Channels * $BytesPerSample
$ByteRate      = $SampleRate * $Channels * $BytesPerSample
$BlockAlign    = $Channels * $BytesPerSample

$Stream = [System.IO.File]::Create($OutputPath)
$Writer = New-Object System.IO.BinaryWriter($Stream)

try {
    # --- RIFF header ---
    $Writer.Write([char[]]"RIFF")
    $Writer.Write([uint32](36 + $DataBytes))
    $Writer.Write([char[]]"WAVE")

    # --- fmt chunk (PCM) ---
    $Writer.Write([char[]]"fmt ")
    $Writer.Write([uint32]16)               # chunk size for PCM
    $Writer.Write([uint16]1)                # format = PCM
    $Writer.Write([uint16]$Channels)
    $Writer.Write([uint32]$SampleRate)
    $Writer.Write([uint32]$ByteRate)
    $Writer.Write([uint16]$BlockAlign)
    $Writer.Write([uint16]$BitsPerSample)

    # --- data chunk ---
    $Writer.Write([char[]]"data")
    $Writer.Write([uint32]$DataBytes)

    $TwoPiFOverSr = 2.0 * [Math]::PI * $FrequencyHz / $SampleRate

    for ($n = 0; $n -lt $TotalSamples; $n++) {
        # Linear fade in/out. Without it the discontinuity at the boundaries
        # is an audible click, which would be indistinguishable from a
        # transport fault when we are specifically testing the transport.
        $gain = 1.0
        if ($FadeSamples -gt 0) {
            if ($n -lt $FadeSamples) {
                $gain = $n / [double]$FadeSamples
            }
            elseif ($n -ge ($TotalSamples - $FadeSamples)) {
                $gain = ($TotalSamples - 1 - $n) / [double]$FadeSamples
            }
        }

        $value = [int][Math]::Round([Math]::Sin($TwoPiFOverSr * $n) * $Peak * $gain)
        if ($value -gt 32767)  { $value = 32767 }
        if ($value -lt -32768) { $value = -32768 }

        $Writer.Write([int16]$value)
    }
}
finally {
    $Writer.Dispose()
    $Stream.Dispose()
}

$Hash = (Get-FileHash -Path $OutputPath -Algorithm SHA256).Hash
$Size = (Get-Item $OutputPath).Length

Write-Host "Technical verification asset written."
Write-Host "  path        $OutputPath"
Write-Host "  format      ${SampleRate} Hz, ${BitsPerSample}-bit, mono PCM"
Write-Host "  tone        ${FrequencyHz} Hz sine, ${DurationSeconds}s, ${AmplitudeDbfs} dBFS, ${FadeMilliseconds}ms fades"
Write-Host "  size        $Size bytes"
Write-Host "  sha256      $Hash"
Write-Host ""
Write-Host "NON-PRODUCTION. Transport verification only. Not the Venn motif."
