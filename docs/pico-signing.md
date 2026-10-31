# PICO APK 签名说明

## 当前配置

- 包名：`com.coffeexr.trailblazer`
- 原 Unity 默认包名已替换，避免触发 PICO 的模板包名签名校验。
- Keystore：`C:\Users\Everest\.pico-keystore\trailblazer\trailblazer.keystore`
- Alias：`trailblazer`
- 本地凭据：`C:\Users\Everest\.pico-keystore\trailblazer\credentials.json`
- 证书 SHA-256：`E4:95:E5:DD:6B:78:DA:8B:8E:2C:35:D7:7D:94:D9:67:8B:A4:E0:AA:55:98:8F:5F:6A:9E:E0:12:5A:39:2A:31`

## 安全要求

- Keystore、密码和 `credentials.json` 均位于仓库外，不得提交到 Git。
- `ProjectSettings.asset` 不得保存 keystore 或 alias 密码。
- 临时签名构建结束后已清除 Unity 中的密码和自定义 keystore 配置。

## 验证

`Build/PICO/Trailblazer.apk` 已使用该证书签名，并通过 `apksigner verify --print-certs` 验证。新包名已安装到 PICO Emulator，未再出现“版权校验不通过：非法签名”。

## 若其他设备安装仍报错

按 PICO 官方排查顺序检查：

1. 设备登录的是否为组织内开发者账号。
2. 新 APK 与 PICO 开发者管理平台已上传 APK 的包名和签名是否一致。
3. `PXR_PlatformSetting.asset` 中的 appID 是否与平台创建的应用一致。
4. 若相同签名关联的未上架 APK 数量达到上限，将 APK 上传至 PICO 开发者管理平台并保持“草稿”状态即可，无需提交审核。
