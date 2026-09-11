# Uploads the publish/ folder to Simply.com over FTP.
# Usage:  .\deploy-ftp.ps1 -Password '<your FTP password>'
# FTP host/user/folder come from the Simply "Get started" page.
param(
  [string]$FtpHost = 'ftp.simply.com',
  [string]$User    = 'omarchycommuni.org',
  [Parameter(Mandatory=$true)][string]$Password,
  [string]$Remote  = '/public_html',
  [string]$Local   = "$PSScriptRoot\publish"
)

$cred = New-Object System.Net.NetworkCredential($User, $Password)

function Ftp($method, $uri) {
  $r = [System.Net.FtpWebRequest]::Create($uri)
  $r.Method = $method; $r.Credentials = $cred
  $r.UsePassive = $true; $r.UseBinary = $true; $r.KeepAlive = $false; $r.Timeout = 300000
  return $r
}

function EnsureDir($path) {
  try { (Ftp ([System.Net.WebRequestMethods+Ftp]::MakeDirectory) "ftp://$FtpHost$path").GetResponse().Close() }
  catch { } # already exists
}

function UploadFile($localFile, $remotePath) {
  $req = Ftp ([System.Net.WebRequestMethods+Ftp]::UploadFile) "ftp://$FtpHost$remotePath"
  $bytes = [System.IO.File]::ReadAllBytes($localFile)
  $req.ContentLength = $bytes.Length
  $s = $req.GetRequestStream(); $s.Write($bytes, 0, $bytes.Length); $s.Close()
  $resp = $req.GetResponse(); $resp.Close()
}

# Remove Simply's placeholder so nothing shadows the app.
try { (Ftp ([System.Net.WebRequestMethods+Ftp]::DeleteFile) "ftp://$FtpHost$Remote/index.html").GetResponse().Close(); Write-Host "removed placeholder index.html" } catch { }

$files = Get-ChildItem -Path $Local -Recurse -File
$dirs = $files | ForEach-Object { $_.DirectoryName.Substring($Local.Length).Replace('\','/') } | Sort-Object -Unique
foreach ($d in $dirs) { if ($d) { EnsureDir "$Remote$d" } }

$i = 0
foreach ($f in $files) {
  $rel = $f.FullName.Substring($Local.Length).Replace('\','/')
  $i++; Write-Host ("[{0}/{1}] {2} ({3} KB)" -f $i, $files.Count, $rel, [math]::Round($f.Length/1KB))
  UploadFile $f.FullName "$Remote$rel"
}
Write-Host "DONE - uploaded $($files.Count) files to $FtpHost$Remote"
