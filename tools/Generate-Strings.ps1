# Erzeugt die .resw-Dateien (de-DE, en-US) aus tools/strings.csv.
# Aufruf: powershell -File tools/Generate-Strings.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csvPath = Join-Path $PSScriptRoot 'strings.csv'
# Semikolon ist der Spaltentrenner: Texte dürfen keins enthalten (sonst werden Spalten still verschoben).
$lineNo = 0
foreach ($line in [IO.File]::ReadAllLines($csvPath, [Text.Encoding]::UTF8)) {
    $lineNo++
    if ($line.Length -gt 0 -and ($line.Split(';').Count -ne 3)) { throw "strings.csv Zeile ${lineNo}: genau 3 Spalten erwartet (Semikolon im Text?): $line" }
}
$rows = Import-Csv -Path $csvPath -Delimiter ';' -Encoding UTF8

foreach ($lang in 'de-DE', 'en-US') {
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$sb.AppendLine('<!-- Automatisch erzeugt aus tools/strings.csv - nicht von Hand bearbeiten. -->')
    [void]$sb.AppendLine('<root>')
    [void]$sb.AppendLine('  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>')
    [void]$sb.AppendLine('  <resheader name="version"><value>2.0</value></resheader>')
    [void]$sb.AppendLine('  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>')
    [void]$sb.AppendLine('  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>')
    foreach ($row in $rows) {
        $value = $row.$lang
        if ([string]::IsNullOrWhiteSpace($value)) { throw "Fehlende Uebersetzung: $($row.Key) [$lang]" }
        $key = [System.Security.SecurityElement]::Escape($row.Key)
        $val = [System.Security.SecurityElement]::Escape($value)
        [void]$sb.AppendLine("  <data name=`"$key`" xml:space=`"preserve`"><value>$val</value></data>")
    }
    [void]$sb.AppendLine('</root>')

    $dir = Join-Path $root "src\SyncForge\Strings\$lang"
    New-Item -ItemType Directory -Force $dir | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $dir 'Resources.resw'), $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
    Write-Host "$lang : $($rows.Count) Eintraege"
}
