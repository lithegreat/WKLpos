# AI Agents & Developer Guidelines (万客隆 POS 系统开发与维护指南)

本文件为参与本工程维护、重构与扩展的 **AI Agents** 及开发人员提供统一的技术规范、架构原则与约束指南。

---

## 1. 架构总则与设计原则

本工程严格遵循 **Clean Architecture (清晰分层架构)** 与 **MVVM 模式**：

```
[WanKePos.WinUI] (WinUI 3 表现层) --> [WanKePos.Infrastructure] --> [WanKePos.Domain]
```

### 分层约束：
1. **`WanKePos.Domain` (领域层)**：
   - 目标框架：`net10.0`（纯 C# 类库，**绝对不依赖任何 UI 框架、EF Core 或外部第三方 IO 库**）。
   - 包含：实体模型 (`Entities`)、业务枚举 (`Enums`)、仓储接口契约 (`Interfaces`)。
   - 所有实体主键默认采用 `int Id` 自增，业务单号（如 `OrderNo`、`PurchaseOrderNo`）采用业务唯一编号规则。

2. **`WanKePos.Infrastructure` (基础设施层)**：
   - 目标框架：`net10.0`。
   - 包含：`PosDbContext`、仓储实现 (`Repositories`)、Excel 导入导出 (`Import/Export`，基于 ClosedXML)、硬件打印通信 (`Hardware/ReceiptPrinter.cs`)、网络同步适配 (`Sync/ApiSyncService.cs`)。
   - 必须通过接口向外部暴露能力，所有数据库读写均使用 `async/await` 异步方法。

3. **表现层 (`WanKePos.WinUI`)**：
   - 采用 Windows App SDK 1.6 原生 WinUI 3 现代设计（Mica 材质、原生控件与流畅交互）。
   - 页面与 ViewModel 必须遵循 MVVM 解耦原则，使用 `CommunityToolkit.Mvvm` 库（`[ObservableProperty]`, `[RelayCommand]`）。
   - **禁止在表现层直接操作数据库连接或执行原生 SQL 拼接**，必须经由仓储接口（如 `IProductRepository`, `IPurchaseOrderRepository`）进行操作。

---

## 2. 性能与响应式规范

1. **瞬时切换要求 (Zero-Latency Tab Switching)**：
   - 所有页面 (`Page`/`View`) 与 ViewModel 在 DI 容器中必须注册为 **Singleton (单例)**。
   - ViewModel 内部应维护 `_isInitialized` 状态守卫，避免每次切换选项卡重复从 SQLite 或磁盘重复加载大数据集。
2. **UI 虚拟化 (UI Virtualization)**：
   - 所有商品列表、会员列表、采购单明细列表及销售流水必须启用原生虚拟化 (`ListView` 或带 `VirtualizingPanel.IsVirtualizing="True"` 的 DataGrid)，保证百万级数据列表秒级渲染与极低内存占用。

---

## 3. WinUI 3 特别注意事项

1. **免打包模式 (Unpackaged Mode)**：
   - `WanKePos.WinUI.csproj` 必须保持 `<WindowsPackageType>None</WindowsPackageType>` 与 `<EnableMsixTooling>true</EnableMsixTooling>`，以便生成独立免安装 `.exe`。
2. **XAML 编译规则**：
   - WinUI 3 原生 XAML 不支持 WPF 的 `StringFormat` 语法属性。格式化应使用 ViewModel 格式化属性或标准 Converter。
   - 所有在 `Views/` 与 `Dialogs/` 下新建的 XAML 页面必须显式或隐式正确纳入 `<Page Include="..." />`，以保证 `XamlCompiler` 生成对应的 `g.i.cs`。
3. **窗口句柄与文件选择器**：
   - 在 WinUI 3 中调用 `FileOpenPicker` 或 `FileSavePicker` 时，必须使用 `WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd)` 绑定主窗口句柄，否则会抛出 COM 异常。

---

## 4. 数据库与数据一致性规范

1. **SQLite 字段精度**：
   - `PosDbContext` 已在 `OnModelCreating` 中全局配置所有 `decimal` 类型字段的精度为 `(18, 2)`。
2. **库存变更必须事务化**：
   - 结账扣减库存 (`CashierViewModel`) 与采购单入库增加库存 (`PurchaseOrderRepository.StockInAsync`) 必须在同一事务/上下文内完成，确保数据绝对一致。
3. **软状态与单据锁定**：
   - 已入库的采购单 (`PurchaseOrderStatus.Received`) 必须锁定，禁止重复触发入库或修改商品数量。

---

## 5. 持续交付、自动打包与即时运行规范 (CI/CD, Packaging & Auto-Run)

**核心约束与强制要求 (Mandatory Rule for AI Agents)**：
- **每次代码或界面修改验证通过后，必须自动执行打包发布**。
- 禁止仅执行 `dotnet build` 就结束任务，必须确保生成最终可交付的独立发布文件与 Windows 安装包，保证 `output_installer\` 下始终为最新版本。
- **每次做完更改并生成 Setup 程序后，必须自动运行安装程序并打开**，以便即时进行端到端体验与功能验收。
- 执行打包发布与自动运行安装程序命令：
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\installer\build_installer.ps1
  ```
  该脚本已内置全流程自动化逻辑：
  1. 自动终止正在运行的 `WanKePos.WinUI` 进程，防止文件占用锁定；
  2. 执行 WinUI 3 独立免依赖编译发布 (`dotnet publish -c Release -r win-x64 --self-contained true -o publish_winui`)；
  3. 清理 `publish_winui\` 中的临时锁与日志文件 (`*.db-shm`, `*.db-wal`, `*.log`)；
  4. 自动定位 Inno Setup 编译器 (或自动安装)，生成单文件安装包 `output_installer\WanKePos_Setup_v0.1.0.exe`；
  5. **自动启动运行生成的 Setup 安装程序并打开应用** (`Start-Process`)。

---

## 6. 常用命令与操作指南

### 编译验证
```powershell
dotnet build WanKePos.sln
```

### 独立发布、生成安装包并自动运行打开
```powershell
# 一键自动发布、打包 Windows 安装包并自动运行安装程序打开 (更改后必须执行)
powershell -ExecutionPolicy Bypass -File .\installer\build_installer.ps1

# 手动发布 WinUI 3 独立程序 (如需单独发布)
dotnet publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained true -o publish_winui
```

### 进程占用排查
在重新构建或发布前，确保已停止运行中的程序进程：
```powershell
Stop-Process -Name "WanKePos.WinUI" -Force -ErrorAction SilentlyContinue
```

### Git 分支与提交规范
- **默认分支保护（强制约束）**：**严禁直接向默认分支（如 `main` / `master`）提交代码**。所有改动必须在特性/修复分支（如 `awesome_ui`、`feat/*`、`fix/*` 等）进行提交，再通过 Pull Request 或 Merge 合并。
- **Git 提交信息约定**：
  - `feat:` 新增业务功能（如采购单、会员功能等）
  - `fix:` 修复 Bug 或界面缺陷
  - `refactor:` 架构重构或分层解耦
  - `perf:` 渲染性能与响应时间优化
  - `docs:` 文档更新（如 README、AGENTS）

