# 万客隆 POS 智能收银与进销存管理系统 (WanKePos)

一套专为美发用品专卖、商超零售店量身定制的现代化 Windows 原生 POS 收银与进销存系统。基于 **.NET 8** 构建，遵循 **清晰分层架构 (Clean Architecture)**，同时提供 **WinUI 3 (Windows App SDK 1.6)** 原生现代化界面与 **WPF Fluent Design** 双前端支持。

---

## 🌟 核心功能特性

### 1. 现代化智能收银台 (Cashier)
- **扫码枪即时扫码**：输入框自动对焦与回车响应，支持连续高频扫码加购；
- **分类导航与快捷加购**：左侧多级品类流式导航，触控友好大按钮设计；
- **会员身份联动**：支持手机号秒查会员，自动切换为会员专享价，实时显示积分与储值余额；
- **灵活结算与找零**：支持现金支付（带实收与找零即时计算）与会员储值余额扣减；
- **小票自动打印**：交易完成后通过 ESC/POS 驱动指令自动出票、切割与积分累加。

### 2. 采购进货与一键入库 (Purchase & Stock-in)
- **快速制作采购单**：直接检索商品库添加拟采购商品，自定义采购进价与采购件数；
- **导出供货商订货单**：基于 ClosedXML 自动生成格式标准、包含单号、供货商、规格、单位、进价、数量、合计金额与签字栏的 Excel 订货单，方便通过微信/邮件直接发送给供货商；
- **货到一键入库**：货物送达验收后，点击“一键入库”，系统通过数据库事务自动将采购件数累加至商品库实际库存，并锁定单据防止重复入库。

### 3. 商品与库存管理 (Product Management)
- **深度对接 SaaS 格式**：完整兼容 32 个商品字段（条码、名称、规格、售价、进价、会员价、库存、品类、供应商等）；
- **Excel 批量导入**：支持一键从 Excel 文件极速导入与更新商品库；
- **库存监控与流式搜索**：支持条码、简码与商品名称多条件模糊检索。

### 4. 会员体系与储值管理 (Member Management)
- **20 维会员画像**：完整支持会员编号、手机号、积分、余额、累计消费、导购归属、会员等级等；
- **Excel 批量导入**：支持现有会员资料一键导入与同步；
- **积分自动规则**：消费时根据后台配置的每元积分比例自动积分。

### 5. 销售记录与营收报表 (Order History & Analytics)
- **今日营收看板**：实时计算并展示今日销售总额、订单总笔数、预估总毛利；
- **历史流水查阅**：支持按日期范围检索历史订单明细、支付方式与积分变动记录。

### 6. 硬件与系统设置 (Hardware & Settings)
- **ESC/POS 热敏小票机**：支持 COM 串口与波特率设置、发送测试打印指令；
- **小票版面自定义**：自由定制小票头部欢迎词与尾部感谢语；
- **单据与规则配置**：灵活配置每元积分规则与门店基本信息。

### 7. 预备云端 SaaS 联网同步 (Cloud Sync Ready)
- 抽象标准 `ISyncService` 接口与 `ApiSyncService` 同步通道，随时对接云端 REST API 实现多门店、云端会员和商品云端统一下发。

---

## 🏗️ 架构设计 (Clean Architecture)

```
c:\Users\WKL\POS\
├── src\
│   ├── WanKePos.Domain\            # 领域层 (Entities, Enums, 仓储接口) - 100% 共享复用
│   ├── WanKePos.Infrastructure\    # 基础设施层 (EF Core SQLite, ClosedXML 导入导出, ESC/POS 硬件驱动, API 同步)
│   ├── WanKePos.WinUI\             # WinUI 3 原生表现层 (Windows App SDK 1.6, Mica 材质, 极致性能)
│   └── WanKePos.App\               # WPF 表现层 (WPF-UI Fluent 界面)
├── publish_winui\                  # WinUI 3 独立分发可执行程序
├── publish\                        # WPF 单文件独立分发可执行程序
└── WanKePos.sln                    # 完整解决方案
```

---

## 🛠️ 技术选型

- **开发框架**: .NET 8 (`net8.0`, `net8.0-windows10.0.19041.0`)
- **现代前端 1**: Windows App SDK 1.6 (WinUI 3 原生 XAML + Unpackaged 独立运行)
- **现代前端 2**: WPF-UI (Fluent Design System)
- **MVVM 框架**: CommunityToolkit.Mvvm 8.3.2
- **依赖注入**: Microsoft.Extensions.DependencyInjection + Microsoft.Extensions.Hosting
- **本地数据库**: Entity Framework Core 8.0 + SQLite (`pos.db`)
- **Excel 引擎**: ClosedXML 0.104.2
- **硬件通信**: System.IO.Ports (ESC/POS 指令集)

---

## 🚀 快速开始与编译运行

### 运行环境要求
- Windows 10 (Build 19041+) 或 Windows 11
- .NET 8.0 SDK

### 1. 克隆或打开工程
```bash
cd c:\Users\WKL\POS
```

### 2. 编译整个解决方案
```powershell
dotnet build WanKePos.sln
```

### 3. 运行 WinUI 3 现代版本
```powershell
dotnet run --project src\WanKePos.WinUI\WanKePos.WinUI.csproj
```

### 4. 运行 WPF 版本
```powershell
dotnet run --project src\WanKePos.App\WanKePos.App.csproj
```

---

## 📦 独立发布与部署 (Standalone Executable)

系统已配置免安装独立运行包（无需目标机器预装 .NET 运行库或 MSIX 证书）：

- **WinUI 3 独立程序**: [`publish_winui\WanKePos.WinUI.exe`](file:///c:/Users/WKL/POS/publish_winui/WanKePos.WinUI.exe)
- **WPF 单文件独立程序**: [`publish\WanKePos.exe`](file:///c:/Users/WKL/POS/publish/WanKePos.exe)

若需重新发布：
```powershell
# 发布 WinUI 3 独立版本
dotnet publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained true -o publish_winui

# 发布 WPF 单文件独立版本
dotnet publish src\WanKePos.App\WanKePos.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

---

## ⌨️ 常用快捷键

| 按键 | 功能 |
| :--- | :--- |
| **F1** | 快速切换至 **收银台** |
| **F2** | 快速切换至 **商品管理** |
| **F3** | 快速切换至 **会员管理** |
| **F4** | 快速切换至 **销售记录** |
| **F6** | 快速切换至 **采购进货** |
| **F5** | 快速切换至 **系统设置** |
| **Enter** | 扫码框/搜索框触发回车搜索或加购 |

---

## 📄 开源与协议
本项目采用专有零售业务授权，所有源代码与数据结构由万客隆 POS 系统统一维护。
