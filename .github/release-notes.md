修复 Markdown 粘贴与显示时换行丢失的问题，适用于 Windows 10/11。

- Prompt 草稿和 AI 收藏均支持网页常用的 LF 换行，同时兼容 CRLF 和 CR。
- 保留原有空行、Markdown 标记、代码缩进、中文与 emoji。
- 支持 Ctrl+V、Shift+Insert 和右键粘贴，并保留选区替换和一步撤销。
- 之前保存的 LF 文本重新打开即可正确显示；仅查看不会改写原有存储内容。
- AI 收藏阅读预览保留普通换行，列表、表格和代码块仍按 Markdown 排版。

下载 `PromptDraft-v1.2.2-windows.zip`，完整解压后双击 `PromptDraft.exe`。需 .NET Framework 4.8 或兼容运行时，程序未签名。

下载包不含任何个人草稿、收藏或备份。旧用户升级前请备份整个原程序文件夹，仅替换程序文件，保留原有 `data/` 和 `backups/`。

同时提供 `.sha256` 文件供核对下载完整性。源码采用 MIT 许可证，第三方依赖许可见 `THIRD_PARTY_NOTICES.txt`。
