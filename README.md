# cli-claude48-opusmedium-csharp

A small CLI tool that encrypts and decrypts using the rclone encryption defaults.

Rclone uses a custom salt if no salt is provided, which this tool will use by default. A few similar tools:

- https://github.com/rclone/rclone
- https://github.com/mcolatosti/rclonedecrypt
- https://github.com/br0kenpixel/rclone-rcc
- @fyears/rclone-crypt

Rclone encryption uses:
- NaCl SecretBox (XSalsa20 + Poly1305) for the file contents.
- AES256 for the filenames.
- scrypt for keymaterial.

Files and filenames produced by this tool are byte-for-byte compatible with
rclone's `crypt` backend (name encryption mode `standard`), so you can decrypt
rclone's output with this tool and vice-versa.

## Installation

The CLI is a self-contained, cross-platform binary &mdash; no .NET runtime or other
framework needs to be installed.

**Homebrew (macOS/Linux)**
```bash
brew tap llm-supermarket/cli-claude48-opusmedium-csharp https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp
brew install cli-claude48-opusmedium-csharp
```

**Scoop (Windows)**
```powershell
scoop bucket add cli-claude48-opusmedium-csharp https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp
scoop install cli-claude48-opusmedium-csharp
```

## Examples usage

The tool has two commands, `encrypt` and `decrypt`. Use `-i`/`--input-file` for the
input and the optional `-o`/`--output-file` for the output. When `-o` is omitted the
output name is **derived from the encrypted/decrypted filename** (exactly like
rclone): encrypting `report.txt` produces a file whose name is the encrypted name,
and decrypting that file restores `report.txt`.

### Encrypt and decrypt a file (interactive prompt)

```bash
# You will be prompted for a password and an optional salt.
cli-claude48-opusmedium-csharp encrypt -i report.txt -o report.enc
cli-claude48-opusmedium-csharp decrypt -i report.enc -o report.txt
```

### Let the filename carry the original name

```bash
# Encrypt: output is named with the encrypted filename, e.g. kr9tu4e1da4u3nifdd99g9tf5o
cli-claude48-opusmedium-csharp encrypt -i TEST_FILE.txt

# Decrypt: the original name (TEST_FILE.txt) is recovered automatically
cli-claude48-opusmedium-csharp decrypt -i kr9tu4e1da4u3nifdd99g9tf5o
```

### Using a salt (rclone "password2")

```bash
# Provide the salt with --salt (or its alias --password2)
cli-claude48-opusmedium-csharp encrypt -i secret.txt -o secret.enc --salt "my-salt"
cli-claude48-opusmedium-csharp decrypt -i secret.enc -o secret.txt --salt "my-salt"
```

### Choosing the filename encoding

The encoding used to turn the encrypted filename bytes into a filesystem-safe
string. The default is `base32` (rclone's default); `base64` is also supported.

```bash
cli-claude48-opusmedium-csharp encrypt -i TEST_FILE.txt --filename-encoding base64
cli-claude48-opusmedium-csharp decrypt -i <encrypted-name> --filename-encoding base64
```

### Supplying the password without a prompt

Most secure first:

```bash
# 1. Environment variable (recommended for scripts)
export RCRYPT_PASSWORD='correct horse battery staple'
export RCRYPT_SALT='optional-salt'          # optional
cli-claude48-opusmedium-csharp encrypt -i report.txt -o report.enc

# 2. --password flag (DISCOURAGED, see warning below)
cli-claude48-opusmedium-csharp encrypt -i report.txt -o report.enc --password 'correct horse battery staple'
```

> [!WARNING]
> Passing `--password` on the command line is **insecure**: the value is typically
> written to your shell history and is visible in the process list to other users
> on the machine. Prefer the interactive prompt or the `RCRYPT_PASSWORD`
> environment variable. If you do use `--password`, clear the offending shell
> history entry afterwards, for example:
> ```bash
> history -d $(history 1)   # bash/zsh: delete the last command from history
> ```

## Details

### Password and salt

- **Password** is resolved in this order: `--password` flag &rarr; `RCRYPT_PASSWORD`
  environment variable &rarr; interactive (masked) prompt.
- **Salt** (rclone's `password2`) is optional and resolved as: `--salt`/`--password2`
  flag &rarr; `RCRYPT_SALT` environment variable &rarr; interactive prompt (press
  Enter to skip). When no salt is given, rclone's built-in default salt is used.

### Command-line flags

| Flag | Default | Description |
|------|---------|-------------|
| `-i`, `--input-file` | *(required)* | File to encrypt or decrypt |
| `-o`, `--output-file` | *(derived)* | Output file; defaults to the encrypted/decrypted filename |
| `--password` | *(prompt)* | Password (insecure; prefer prompt or env var) |
| `--salt`, `--password2` | *(none)* | Optional salt / password2 |
| `--filename-encoding` | `base32` | Filename encoding: `base32` or `base64` |
| `-h`, `--help` | | Show help |
| `-v`, `--version` | | Show version |

## Building from Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/llm-supermarket/cli-claude48-opusmedium-csharp
cd cli-claude48-opusmedium-csharp
dotnet build
dotnet test
```

Publish a self-contained single-file binary for your platform:

```bash
dotnet publish src/Cli/RcloneCrypt.Cli.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true
```

## Releases

Pushing a `vX.Y.Z` tag triggers the [Build and Release workflow](.github/workflows/build-release.yml),
which cross-compiles self-contained binaries for Linux and macOS (amd64/arm64) and
Windows (amd64), publishes a GitHub Release, and updates the Scoop manifest
(`cli-claude48-opusmedium-csharp.json`) and Homebrew formula
(`Formula/cli-claude48-opusmedium-csharp.rb`) in this repo.
