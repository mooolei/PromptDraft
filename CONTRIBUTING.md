# Contributing

欢迎报告问题和提交改进。请保持这个工具轻量、离线、容易使用。

## 开发

1. 在 Windows 上克隆仓库，执行 `scripts/build.ps1`。
2. 修改 `src/`，使用 `scripts/test.ps1` 验证核心行为。
3. 涉及界面、剪贴板或绘制时，在交互式桌面运行 `scripts/test.ps1 -Ui`。
4. 使用 `scripts/package.ps1` 检查分发包。

自动化测试必须使用合成数据；不要把真实的 prompt、收藏、`data/`、`backups/` 或访问令牌加入提交或 Issue。截图也请使用示例内容。

## 修改约定

- 保持当前数据格式向后兼容；默认不联网、不收集遥测。
- 内容保存失败时，不应静默关闭或覆盖无法读取的数据。
- 涉及文字复制时，同时验证中文、多行和 Markdown；富文本复制应包含纯文本后备格式。
- 列表绘制的改动需要验证多条内容同时显示、滚动和选中重绘。
- 提交 PR 时说明问题、解决后的行为和实际运行过的测试。

源代码沿用 .NET Framework 编译器支持的 C# 语法。依赖版本和校验值固定在 `dependencies.lock.json`，更新依赖时请同步第三方许可。

## 发布版本（维护者）

同步更新 `src/PromptDraft.cs` 中的程序集版本、`CHANGELOG.md`、README 下载文件名和 `.github/release-notes.md`，最后修改 `.github/release-version.txt`。该版本文件的修改进入 `main` 后，会触发 Windows 构建、核心测试、打包和 GitHub Release 发布；也可以在 Actions 中手动运行 `Publish Windows release`。

发布流程核对版本号和 ZIP 校验值，再上传下载包及 SHA-256 文件。只在发布任务中使用 GitHub 自动提供的临时仓库写入令牌，不需要个人访问令牌。已有版本不会被覆盖；失败时请先检查 Actions 日志及可能遗留的草稿 Release。
