# 验证记录

生成时间：2026-10-24

## 构建

- 源文件：`src\*.cs`
- 编译器：`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
- 目标：`dist\DeltaForceTuner.exe`，x64，WinForms，.NET Framework 4.8
- SHA256：`43C14A5F290DC2DD2765305319BB3C2DEE334F668BDA5C4C2BC71A67C4376DA5`
- 版本资源：`三角洲行动 · 本机调优助手` / `1.0.0.0`

## 只读自检

命令（同一套源码的 console 构建）：

```text
DeltaForceTunerConsole.exe --selftest=selftest.txt
```

结果：`RESULT=PASS warnings=0`

关键输出：

- Windows 11 Pro 25H2 Build 26200.9168
- i9-12900H，6 个 P 核 + 8 个 E 核，20 线程
- E 核掩码：E1=0x1000 … E8=0x80000（CPU 12–19）
- 游戏目录：`D:\APPs_D\Steam\steamapps\common\Delta Force`
- ACE 目录：`...\Game\DeltaForce\Binaries\Win64\AntiCheatExpert`
- 亲和性 API 冒烟测试：当前进程可绑定到 CPU 19，并已恢复
- 自检时 ACE 未运行

完整输出：`evidence\selftest.txt`。

## 游戏文件未被修改

运行自检/渲染前后，以下文件 SHA256 完全一致：

- `Game\DeltaForce\Binaries\Win64\DeltaForceClient-Win64-Shipping.exe`
- `...\AntiCheatExpert\ACE-Service64.exe`
- `...\AntiCheatExpert\SGuard\x64\SGuard64.exe`
- `...\AntiCheatExpert\SGuard\x64\SGuardSvc64.exe`

对比文件：`evidence\game-hashes-before.txt` 与 `evidence\game-hashes-after.txt`。

## 注册表状态未被测试意外修改

自检后复核：

- `GameDVR_Enabled = 1`
- `AppCaptureEnabled = 1`
- `NetworkThrottlingIndex = 10`
- `SystemResponsiveness = 20`
- `MouseSpeed = 1`
- `HwSchMode` 未设置
- `ActiveOverlayAcPowerScheme = ded574b5-45a0-4f42-8737-46345c09c238`
- `ActiveOverlayDcPowerScheme = 961cc777-2547-4f9d-8174-7d86181b8a7a`
- `HKCU\Software\Microsoft\GameBar` 下没有测试写入的 `AutoGameModeEnabled`

沙箱环境禁止 HKCU/HKLM 写入，因此本环境没有实际执行开关写入测试；代码路径为“先备份、再写值、写后读回校验、失败即报告”。在正常桌面环境（管理员）下才可真正写入 HKLM/ACE。

## 界面

`--render` 已生成四个页面：

- `evidence\overview.png`
- `evidence\settings.png`
- `evidence\ace.png`
- `evidence\help.png`

`dist\preview.png` 是默认 1200×820 窗口的概览页预览。

## 静态检查

- 源码中没有 `HttpClient` / `WebClient` / `Socket` / DNS / HTTP URL / 上传逻辑。
- `Process.Start` 只用于：
  - `Elevation.Relaunch`（以管理员身份重启自身）
  - `powercfg`（切换/恢复电源模式）
- `SetProcessAffinityMask` 只作用于 ACE 用户态进程或自检中的当前进程。
- 不注入 DLL、不结束/暂停 ACE、不安装服务、不写开机启动项、不修改游戏目录。

## 未能在本环境验证的部分

- ACE 实际运行时的亲和性修改：自检时 ACE 未运行；且沙箱阻止对子进程的 `OpenProcess`。正常桌面 + 管理员启动游戏后，ACE 页会显示每个进程是“已限制”还是“受保护/拒绝访问”。
- UAC 提权、电源 overlay 写入、HKCU/HKLM 开关写入：沙箱禁止写注册表/需要交互式 UAC，无法在本环境执行。
- 高 DPI 多显示器、不同 Windows 版本上的最终视觉细节。
