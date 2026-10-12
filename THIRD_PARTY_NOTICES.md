# Third-Party Notices

本项目的构建与发布包中使用了以下第三方组件，特此致谢并保留其许可要求：

| 组件 | 版本 | 许可 | 来源 |
| --- | --- | --- | --- |
| [scrcpy](https://github.com/Genymobile/scrcpy) | 4.1（构建时自动获取最新） | Apache-2.0 | Genymobile，屏幕投屏核心引擎（含 SDL3 等依赖，遵循其各自许可） |
| Android SDK Platform-Tools（adb） | latest | Apache-2.0 | Google，ADB 调试/设备识别 |

**说明：**
- 上述二进制**不提交到本仓库**；由 `scripts/build.ps1` 在构建时自动下载，或随 Release 打包提供。
- 各组件的完整许可文本请访问其官方项目查看。
- 本项目的自有代码（`src/PhoneMirror.cs`、图标、文档）以 [Apache-2.0](LICENSE) 发布。
