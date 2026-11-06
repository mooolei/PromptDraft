# Security and data handling

PromptDraft runs locally. Drafts, saved answers and view preferences are stored as unencrypted JSON beside the executable. Clipboard contents may be visible to other local applications.

The Markdown preview disables raw HTML, does not load remote images, and blocks link navigation. It uses the Windows MSHTML engine; rendering support is limited by that component.

Please do not attach real prompts, account credentials or personal data to a public issue. For vulnerabilities involving data exposure, use the repository's private vulnerability reporting feature if enabled, or contact a maintainer privately before publishing details.

Release binaries are currently unsigned. Build scripts verify locked dependency hashes; release packages include a SHA-256 checksum file.
