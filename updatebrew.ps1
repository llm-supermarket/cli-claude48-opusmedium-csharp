param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$repo = "llm-supermarket/cli-claude48-opusmedium-csharp"
$name = "cli-claude48-opusmedium-csharp"
$platforms = @("darwin-amd64", "darwin-arm64", "linux-amd64", "linux-arm64")
$formulaPath = "$PSScriptRoot/Formula/$name.rb"
$base = "https://github.com/$repo/releases/download/v$Version"

# Download each release tarball and compute its SHA256.
$hash = @{}
foreach ($platform in $platforms) {
    $url = "$base/$name-$platform.tar.gz"
    $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) "$name-$platform.tar.gz"

    Write-Host "Downloading $url ..."
    Invoke-WebRequest -Uri $url -OutFile $tempFile

    $hash[$platform] = (Get-FileHash -Path $tempFile -Algorithm SHA256).Hash.ToLower()
    Write-Host "SHA256 for ${platform}: $($hash[$platform])"

    Remove-Item $tempFile
}

# Regenerate the formula wholesale so it stays correct across releases (a
# placeholder-substitution approach only works for the first release, and a
# case-insensitive replace would clobber the `version` keyword / `--version`).
$formula = @"
class CliClaude48OpusmediumCsharp < Formula
  desc "rclone-compatible file and filename encryption CLI"
  homepage "https://github.com/$repo"
  version "$Version"

  on_macos do
    if Hardware::CPU.arm?
      url "$base/$name-darwin-arm64.tar.gz"
      sha256 "$($hash['darwin-arm64'])"
    else
      url "$base/$name-darwin-amd64.tar.gz"
      sha256 "$($hash['darwin-amd64'])"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "$base/$name-linux-arm64.tar.gz"
      sha256 "$($hash['linux-arm64'])"
    else
      url "$base/$name-linux-amd64.tar.gz"
      sha256 "$($hash['linux-amd64'])"
    end
  end

  def install
    bin.install "$name-darwin-arm64" => "$name" if OS.mac? && Hardware::CPU.arm?
    bin.install "$name-darwin-amd64" => "$name" if OS.mac? && !Hardware::CPU.arm?
    bin.install "$name-linux-arm64" => "$name" if OS.linux? && Hardware::CPU.arm?
    bin.install "$name-linux-amd64" => "$name" if OS.linux? && !Hardware::CPU.arm?
  end

  test do
    assert_match "$name #{version}", shell_output("#{bin}/$name --version")
  end
end
"@

Set-Content -Path $formulaPath -Value $formula -NoNewline
Write-Host "Wrote $formulaPath for version $Version"
