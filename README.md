# 三角洲行动 · 本机调优助手

一个**纯本地、单 exe、可逆**的 Windows 游戏体验调优工具。它会检查本机状态，用图形化开关修改少量低风险 Windows 设置。另有一个**默认关闭**的 ACE 小核亲和性实验功能；普通用户完全可以不使用它。

- 不修改游戏目录、ACE 文件、驱动或服务
- 不注入 DLL、不挂钩子、不结束/暂停 ACE 进程
- 不关闭 Defender / 内存完整性 / VBS
- 不联网、不安装服务、不设置开机启动
- 所有注册表/电源修改在写入前保存原始值，可逐项或一键恢复

## 文件

| 文件 | 说明 |
|---|---|
| `dist\DeltaForceTunerSetup.exe` | **安装包**，原生 C++ 编译，双击安装 |
| `dist\DeltaForceTuner.exe` | 主程序，单文件，Windows 10/11 x64 |
| `dist\使用说明.txt` | 给最终用户的简短说明 |
| `dist\preview.png` | 界面预览 |
| `dist\Setup-SHA256.txt` | 安装包哈希 |
| `src\` | 主程序 C# 源码（.NET Framework 4.8 / WinForms） |
| `installer-cpp\` | 安装器 C++ 源码（MinGW g++） |
| `build.ps1` / `build-cpp.ps1` | 主程序 / 安装包编译脚本 |

程序使用 Windows 自带的 .NET Framework 4.8，不需要安装 Python、Node 或 .NET 运行时。

## 本机检测结果（生成时）

- Windows 11 Pro 25H2，Build 26200.9168
- Intel Core i9-12900H：6 个 P 核（12 线程）+ 8 个 E 核（8 线程），共 20 线程
- Intel Iris Xe Graphics，驱动 32.0.101.7092
- 15.7 GB 内存，2520×1680 @ 60 Hz，已插电
- 《三角洲行动》已安装：`D:\APPs_D\Steam\steamapps\common\Delta Force`
- ACE 目录已识别；生成自检时 ACE 未运行

## 功能

### 1. 概览

显示 CPU 拓扑、显卡/驱动、内存、系统版本、显示分辨率/刷新率、电源计划、游戏与 ACE 状态，并给出推荐完成度。

### 2. 性能开关（全部可逆）

| 开关 | 推荐 | 作用 | 需要重启 |
|---|---|---|---|
| Windows 游戏模式 | 是 | 让 Windows 优先把资源给当前游戏 | 否 |
| 关闭后台录制 | 是 | 关闭 Game DVR 持续后台录制，减少编码/磁盘写入 | 否 |
| 多媒体与网络调度 | 是 | 关闭网络节流，游戏线程调度更积极 | 否 |
| 关闭鼠标加速 | 是 | FPS 瞄准更线性；会改变系统鼠标手感 | 否 |
| 电源模式：最佳性能 | 是（插电） | 插电时切到“最佳性能”，减少降频 | 否 |
| 硬件加速 GPU 调度（HAGS） | 可选 | 让 GPU 管理显存调度；部分核显/老驱动可能反而卡顿 | 是 |

“一键应用推荐”只应用标为“推荐”的项，不会应用 HAGS。

### 3. ACE（可选，默认关闭）

> 普通用户建议只使用上面的 Windows 设置。ACE 页默认关闭，不会自动修改任何 ACE 进程；只有你主动点“立即应用”才会尝试。

- 自动识别 Intel 大小核（`GetLogicalProcessorInformationEx`，EfficiencyClass 区分 P/E）。
- 只处理 `AntiCheatExpert` 目录下的用户态 ACE 进程，例如：
  - `SGuard64.exe`（ACE-Guard Client）
  - `SGuardSvc64.exe`（ACE-Guard Service）
  - `ACE-Service64.exe`、`ACE-Helper.exe`
- **不会**碰游戏主程序 `DeltaForceClient-Win64-Shipping.exe`，也不会碰驱动。
- 目标模式：
  - **E 核组（推荐）**：全部 E 核，避免多个 ACE 进程挤在一个核上
  - **单颗 E 核**：默认 E8 / CPU 19，最严格地把 ACE 塞进一颗小核
  - **自定义**：在 CPU 拓扑图上点任意 E 核
- “游戏启动后自动保持”默认关闭；即使打开，也需要你手动开启才会生效。
- 退出工具时默认恢复 ACE 的原始亲和性；可在页面里关闭这个行为。
- 如果 ACE 保护其进程，Windows 会返回“拒绝访问”，工具会如实显示，**不会绕过保护**。

#### 为什么默认是 E 核组而不是单核？

ACE 通常有多个进程。全部塞进一颗 E 核虽然最“干净”，但多个进程可能互相抢一个核，极端情况下影响反作弊响应。E 核组仍然完全不占用 P 核，同时更稳。需要严格单核时，点“单颗 E 核”即可。

#### 反作弊提示

只改 Windows 进程亲和性，不修改 ACE 文件、不注入、不结束进程。但部分反作弊版本可能把“外部修改其进程设置”视为异常。工具第一次应用前会弹出一次确认；如果游戏提示异常，请立即点“恢复原始”并重启游戏。请自行确认游戏规则。

## 安装包

双击 `dist\DeltaForceTunerSetup.exe`：

- 默认安装到当前用户目录 `%LocalAppData%\Programs\DeltaForceTuner`，**不需要管理员权限**
- 创建开始菜单快捷方式，可选创建桌面快捷方式
- 写入当前用户的“应用和功能”卸载信息
- 安装器只释放程序文件，不修改游戏文件、ACE 文件、驱动或服务
- 卸载时默认保留设置备份；可在卸载器里勾选一并删除

安装包是原生 C++（MinGW g++ 8.1，32 位）编译，主程序仍是 .NET Framework 4.8。安装包未做商业签名，SmartScreen 可能提示“未知发布者”。

## 使用

1. 双击 `DeltaForceTuner.exe`（或安装后使用开始菜单快捷方式）。
2. 普通权限即可查看检测结果；首次修改 HKLM 项时，工具会询问是否以管理员身份重启。
3. 概览页点“一键应用推荐”，然后按“游戏内建议”调整画质/分辨率。
4. 可以关闭窗口最小化到托盘；游戏结束后从托盘“退出工具”。

ACE 页是可选的，默认关闭；普通用户不需要进入。若确实要尝试，启动游戏进入大厅后，在“ACE（可选）”页选择“E 核组”或“单颗 E 核”，手动点“立即应用”。

## 安全与恢复

- 每个开关都有“恢复原始”；侧栏“恢复全部”会恢复本工具改过的所有设置，并恢复 ACE 亲和性。
- 备份文件（XML）只写到当前用户目录：
  `%LocalAppData%\DeltaForceTuner\state.xml`
  如果该目录不可写，会退回到 exe 同目录的 `data\state.xml`。
- 修改注册表前会先保存值是否存在、类型和原始数据；恢复时，原本不存在的值会被删除，原本存在的值会写回。
- 不删除注册表键，不递归改权限，不碰游戏目录。
- 电源模式通过 `powercfg /overlaysetactive` 切换，恢复时写回保存的原始 overlay GUID。
- 鼠标加速通过注册表 + `SystemParametersInfo(SPI_SETMOUSE)` 即时生效；恢复时写回原始值。
- HAGS 需要重启；其余开关即时生效。

## 构建

要求：Windows 10/11 x64，自带 .NET Framework 4.8。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
```

主程序用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 编译；安装包用 RedPanda-Cpp 的 MinGW g++ 8.1 编译：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-cpp.ps1
```

输出：

- `dist\DeltaForceTuner.exe`（主程序）
- `dist\DeltaForceTunerSetup.exe`（原生 C++ 安装包）
- `build-cpp\Uninstall.exe`（安装包内嵌的卸载器）

自检：

```powershell
.\dist\DeltaForceTuner.exe --selftest=selftest.txt
```

自检只读系统/游戏信息；亲和性测试只短暂限制自身进程并立即恢复。

## 验证记录

- 只读自检通过：CPU 拓扑识别为 6P+8E，游戏与 ACE 目录识别正确，亲和性 API 冒烟测试通过。
- `--render` 已生成四个页面的截图，布局检查通过。
- 生成自检时 ACE 未运行，因此**没有**在实际 ACE 进程上验证修改；ACE 功能默认关闭，仅作为可选实验保留。
- 启动游戏后，ACE 页会显示每个进程是“已限制”还是“受保护/拒绝访问”；不点“立即应用”就不会改动。
- 沙箱内运行前后对游戏主程序、`ACE-Service64.exe`、`SGuard64.exe`、`SGuardSvc64.exe` 做 SHA256 对比，均未变化（见 `evidence\game-hashes-before.txt` 与 `after`）。2026-11-14 真机再次比对：三个 ACE 文件仍与基线一致，游戏主程序则已变化——2026-11-08 的 Steam 更新（514154112 → 524423208 字节），与本工具无关。
- 源码中没有 HTTP/WebSocket/DNS/上传逻辑；没有安装服务或写开机启动项。
- 原生 C++ 安装包早先完成过“静默安装到临时目录 → 安装后自检 → 静默卸载 → 卸载器自删除”的沙箱内测试，详见 `evidence\installer-test.txt`。
- 2026-11-14 在真机（Windows 11 Pro 25H2，普通用户，未提权）补完了此前无法验证的部分，详见 `evidence\installer-acceptance-2026-11-14.txt`：
  - 静默安装退出码 0，安装目录 6 个文件齐全；安装器日志完整可读（本次顺带修正了日志格式串 `%s` → `%ls` 的截断缺陷）。
  - 开始菜单快捷方式、桌面快捷方式均创建成功，目标/工作目录/图标正确（本机 Desktop 已知文件夹被重定向，已按真实路径核对）。
  - HKCU 卸载项写入正确（DisplayName、版本 1.0.1、EstimatedSize、NoModify/NoRepair）。
  - 安装版主程序 `--selftest` 输出 `RESULT=PASS warnings=0`。
  - 按注册表登记的 `QuietUninstallString` 静默卸载退出码 0，程序目录/开始菜单/桌面快捷方式/注册表项全部清除，`%LocalAppData%\DeltaForceTuner` 设置备份按设计保留。
- 真机比对游戏文件：三个 ACE 文件哈希与基线完全一致；游戏主程序 `DeltaForceClient-Win64-Shipping.exe` 有变化（2026-11-08 被 Steam 更新，+10MB），与本工具无关，细节见上述验收记录。
- 2026-11-14 修复「鼠标加速」开关的一个真实缺陷：`SystemParametersInfo(SPI_SETMOUSE)` 的参数数组顺序应为 `{threshold1, threshold2, acceleration}`，旧代码却按 `{speed, threshold1, threshold2}` 写入。关闭加速时三个值都是 0，问题看不出来；但「停用 / 恢复原始」会把 `MouseSpeed` 写成 `MouseThreshold2` 的值（默认 10），等于把指针加速拉到最大，手感就是“点了反而更飘”。已改为正确顺序，并在真机往返验证：apply → `0/0/0`，disable → `1/6/10`（即原值）。详见 `evidence\bugfix-mouse-accel-2026-11-14.txt`。

## 已知限制

- 目前只支持 Windows 10/11 x64。
- 电源“最佳性能”覆盖开关只在 Windows 11（Build ≥ 22000）显示；Windows 10 上该项显示“不可用”。
- ACE 若以受保护进程运行，外部进程无法修改其亲和性；这是反作弊的合法行为，工具不会尝试绕过。
- 只调整用户态 ACE 进程；ACE 的内核驱动线程不受影响。
- exe 未做商业代码签名，首次运行可能出现 SmartScreen“未知发布者”提示；可核对 SHA256。
- 本仓库所在的 `deepseek-harness` 工作区目录带完整性标签（Mandatory Label\Low Mandatory Level），在该目录里直接运行安装包会静默失败（退出码 1、不装文件、不写日志），因为它继承低完整性级别、写不了 `%LocalAppData%` 和 `%TEMP%`。这是目录环境问题，不是安装包缺陷：把 `DeltaForceTunerSetup.exe` 复制到桌面或任意普通目录再运行即可（真机验收就是这么做的）。
- HAGS 是否有效取决于显卡/驱动，标为“可选”，不会默认开启。

## 从源码构建

仓库只收源码，`dist\*.exe`、`dist\*.zip`、`assets\app.ico`、`dist\preview.png`、`evidence\*.png` 均被 `.gitignore` 排除（见该文件末尾说明）。因此首次构建前需要先把图标和预览图生成出来：

```powershell
# 1) 编译主程序（assets\app.ico 缺失时会自动调用 assets\make-icon.ps1 生成）
.\build.ps1
# 2) 生成安装包要嵌入的界面预览图
.\dist\DeltaForceTuner.exe --render dist\preview.png
# 3) 打包安装程序（内嵌主程序、说明、预览图、哈希）
.\build-cpp.ps1
```
由豆姐和大肥鱼维护（豆姐用的token少啊）
测试版由dsh编写，上传版本由豆包工作编写
工具链依赖：

- `csc`：`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（.NET Framework 4.8，系统自带）
- `g++` / `windres`：MinGW（脚本顶部默认指向 RedPanda-CPP 自带的 MinGW，路径不同需自行修改）

产物：`dist\DeltaForceTuner.exe`（便携版）、`dist\DeltaForceTunerSetup.exe`（安装包），哈希分别写入 `dist\SHA256.txt` 与 `dist\Setup-SHA256.txt`。

## 更新记录

- 1.0.1（2026-11-14）
  - 修复「关闭鼠标加速」的参数错位：`SPI_SETMOUSE` 要求 `{threshold1, threshold2, acceleration}`，旧代码按 `{speed, threshold1, threshold2}` 写，导致「停用 / 恢复原始」把 `MouseSpeed` 写成 `MouseThreshold2`（默认 10），等效把指针加速拉满、手感变飘。
  - 修复安装器日志格式串（`%s` → `%ls`）：此前每行日志只输出首字符，`OK ...` 变 `O`。
  - 真机补完安装 / 卸载验收：开始菜单与桌面快捷方式、HKCU 卸载项、静默卸载与清理（此前只在沙箱内验证）。
  - `build.ps1` 现在自动生成 `dist\SHA256.txt`，不再需要手工维护。
- 1.0.0（2026-10-24）：首个版本。

## SHA256

```
68C631B8392FC01243C3978F8B0277290741B9EEC7BF7D25069DE6C819795AFE  DeltaForceTuner.exe
695F456AB7D164EF9D26538A2B42A150607AC20DCF7867793F4BD4C6AC1973B2  DeltaForceTunerSetup.exe
```

构建时间：2026-11-14（版本 1.0.1：主程序修复「鼠标加速」开关的 SPI_SETMOUSE 参数顺序缺陷，安装器修复日志截断）。安装包哈希同时写在 `dist\Setup-SHA256.txt`，主程序哈希在 `dist\SHA256.txt`。

注意：本 README 会被嵌进安装包，因此安装包内那份 README 的哈希行可能落后一个构建版本；以 `dist\Setup-SHA256.txt` 为准。
