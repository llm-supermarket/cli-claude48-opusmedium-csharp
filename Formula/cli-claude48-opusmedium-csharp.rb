class CliClaude48OpusmediumCsharp < Formula
  desc "rclone-compatible file and filename encryption CLI"
  homepage "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp"
  version "1.0.0"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-darwin-arm64.tar.gz"
      sha256 "5509eb58147c321e29c35b9341b63cba7cced27d7f51d52b16474062a78510cc"
    else
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-darwin-amd64.tar.gz"
      sha256 "75d4f06be418876ca7026291ec36971f986b9a0c39f47e525e8729320c866004"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-linux-arm64.tar.gz"
      sha256 "dace151b29f07b718c06ec8bf9097aed672446b06f6c646665def03358ec4360"
    else
      url "https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp/releases/download/v1.0.0/cli-claude48-opusmedium-csharp-linux-amd64.tar.gz"
      sha256 "7012b41677161fb08d6664a65b685c5a58d0ee59ad385150524f5c07ad55a630"
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