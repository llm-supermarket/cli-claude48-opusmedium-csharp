class CliClaude48OpusmediumCsharp < Formula
  desc "rclone-compatible file and filename encryption CLI"
  homepage "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp"
  version "1.0.0"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-darwin-arm64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    else
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-darwin-amd64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-linux-arm64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    else
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-linux-amd64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
  end

  def install
    bin.install "cli-claude48-opusmedium-csharp-darwin-arm64" => "cli-claude48-opusmedium-csharp" if OS.mac? && Hardware::CPU.arm?
    bin.install "cli-claude48-opusmedium-csharp-darwin-amd64" => "cli-claude48-opusmedium-csharp" if OS.mac? && !Hardware::CPU.arm?
    bin.install "cli-claude48-opusmedium-csharp-linux-arm64" => "cli-claude48-opusmedium-csharp" if OS.linux? && Hardware::CPU.arm?
    bin.install "cli-claude48-opusmedium-csharp-linux-amd64" => "cli-claude48-opusmedium-csharp" if OS.linux? && !Hardware::CPU.arm?
  end

  test do
    assert_match "cli-claude48-opusmedium-csharp #{version}", shell_output("#{bin}/cli-claude48-opusmedium-csharp --version")
  end
end
