param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath
)

# Windows.Media.Ocr requires STA thread
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    powershell -STA -NoProfile -File $MyInvocation.MyCommand.Path $ImagePath
    return
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime

$null = [Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime]
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]

function AwaitAsync($asyncOp) {
    while ($asyncOp.Status -eq 0) { Start-Sleep -Milliseconds 50 }
    if ($asyncOp.Status -eq 2) { throw $asyncOp.ErrorCode }
    return $asyncOp.GetResults()
}

try {
    if (-not (Test-Path $ImagePath)) { throw "File not found: $ImagePath" }

    $fullPath = Resolve-Path -LiteralPath $ImagePath
    Write-Host "Reading: $fullPath" -ForegroundColor Cyan

    $file = AwaitAsync([Windows.Storage.StorageFile]::GetFileFromPathAsync($fullPath))
    $stream = AwaitAsync($file.OpenReadAsync())

    $decoder = AwaitAsync([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream))
    $bitmap = AwaitAsync($decoder.GetSoftwareBitmapAsync())

    # Try user profile languages first, fallback to English
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    if ($null -eq $engine) {
        $enLang = New-Object 'Windows.Globalization.Language' 'en-US'
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($enLang)
    }
    if ($null -eq $engine) { throw 'No OCR engine available. Install language pack.' }

    $result = AwaitAsync($engine.RecognizeAsync($bitmap))

    if ([string]::IsNullOrWhiteSpace($result.Text)) {
        Write-Host "`n(no text detected)" -ForegroundColor Yellow
    } else {
        Write-Host "`n'````"
        Write-Output $result.Text
        Write-Host "````"
    }
}
catch {
    Write-Error "ERROR: $_"
    exit 1
}
