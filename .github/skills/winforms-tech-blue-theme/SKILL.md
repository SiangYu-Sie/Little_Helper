---
name: winforms-tech-blue-theme
description: "套用專業科技感 WinForms UI 風格（淡藍 + 淺灰色系）。Use when: 需要修改或建立 WinForms 介面風格、套用科技感配色、更新現有表單主題、新增按鈕或面板需要與系統風格一致、designing WinForms UI theme tech blue gray style。包含完整色碼、控制項設定、按鈕主題函式範本。"
argument-hint: "可輸入 scope 或檔案路徑，例如: l1-only / control-only / event-access-only / mainform-only / HostSimTester.App/Pages/L1InitialTestPage.cs"
---

# WinForms 專業科技感主題 (Tech Blue / Light Gray)

## 設計原則

- 主色調：**冰藍 + 海軍藍** — 傳遞專業、精密的科技感
- 輔助色：**淺灰白** — 乾淨不刺眼，降低視覺疲勞
- 危險操作：**低飽和紅** — 警示但不粗暴
- 控制台/Log區：**深海軍底色 + 淡青藍文字** — 仿終端機感，易讀

## L1 Initial Test 版型基準

以下版型以 `HostSimTester.App/Pages/L1InitialTestPage.cs` 為標準，新增或調整測試頁時優先沿用：

- 頁面容器：`TabControl`，三個頁籤
- 頁籤名稱：`Comm && Template`、`Control Mode`、`Event && Access`
- 每個頁籤主體：`TableLayoutPanel`（2 欄 1 列，50/50）
- 區塊容器：`GroupBox` + 內層 `FlowLayoutPanel(TopDown, WrapContents=false)`
- 區塊標題順序：
    - `2. Establish Communication Test`
    - `3. TSMC Excel File Template for "Define Event Report"`
    - `4.1 Equipment Control Mode Check`
    - `4.2 Host Control Mode Check`
    - `5. Define Event Report Test`
    - `6. Access Mode Check`
- 動作列樣式：每一列由「16x16 狀態燈 + 按鈕」組成，狀態燈預設灰色，執行中黃，成功綠，失敗紅

### 互動一致性

- 由 `BaseTestPage.AddActionTo(...)` 建立測試按鈕，避免各頁自訂按鈕樣式造成不一致
- 維持 `ActionPanel` 高度固定、`WrapContents=false`，避免視窗縮放時版面跳動
- 收到設備主動事件（如 S6F11）時，允許頁面主動更新對應測試燈號，不限於按鈕點擊後才更新

### 字型與尺寸建議（沿用現況）

- 頁面標題：`Microsoft JhengHei UI`, 10pt, Bold
- 一般按鈕高度：30
- 每列 item panel 高度：38
- 訊息區字型：`Consolas`, 9pt

### Scope 關鍵字（統一）

- `l1-only`
- `control-only`
- `event-access-only`
- `comm-template-only`
- `mainform-only`
- `status-log-only`

舊語句相容對照：

- `Control Mode only` -> `control-only`
- `Event & Access only` -> `event-access-only`
- `Comm & Template only` -> `comm-template-only`
- `MainForm only` -> `mainform-only`

### 建議輸入範例

- `l1-only`
- `control-only`
- `event-access-only`
- `comm-template-only`
- `mainform-only`
- `HostSimTester.App/Pages/L1InitialTestPage.cs`
- `HostSimTester.App/MainForm.cs + l1-only`

---

## 色彩系統

完整色票請參考 [references/color-palette.md](./references/color-palette.md)。

| 用途 | Color Name | HEX / RGB |
|------|-----------|-----------|
| 表單背景 | IceSurface | `#F0F5FA` = `RGB(240,245,250)` |
| 工具列 / 頂部面板 | NavyPanel | `#1F4E79` = `RGB(31,78,121)` |
| 主要按鈕 | CobaltBlue | `#2980B9` = `RGB(41,128,185)` |
| 按鈕邊框 | DeepBlue | `#1F619D` = `RGB(31,97,141)` |
| 危險按鈕 | DangerRed | `#A03C3C` = `RGB(160,60,60)` |
| 表格/清單背景 | TableBg | `#F8FBFE` = `RGB(248,251,254)` |
| 群組框/標題背景 | GroupHeaderBg | `#D6E8F8` = `RGB(214,232,248)` |
| 標籤文字（深） | TextDark | `#1F4E79` = `RGB(31,78,121)` |
| 標籤文字（中） | TextMid | `#2C3E50` = `RGB(44,62,80)` |
| 標籤文字（淺） | SubText | `#506070` = `RGB(80,96,112)` |
| Log 背景 | LogBg | `#0F1C30` = `RGB(15,28,48)` |
| Log 文字（一般） | LogText | `#78C8F0` = `RGB(120,200,240)` |
| Log 文字（警示） | LogAlert | `#FFA864` = `RGB(255,168,100)` |
| Log 文字（資料） | LogData | `#56CCF2` = `RGB(86,204,242)` |
| 狀態列背景 | StatusBg | `#1F4E79` |
| 狀態列文字 | StatusText | `#BDD7EE` = `RGB(189,215,238)` |
| TreeView 背景 | TreeBg | `#F2F8FD` = `RGB(242,248,253)` |
| TreeView 群組節點底色 | TreeGroupBg | `#C8E0F3` = `RGB(200,224,243)` |

---

## 套用步驟

### 1. 表單基底

```csharp
// MainForm constructor 或 InitializeComponent() 之後
BackColor = Color.FromArgb(240, 245, 250);   // IceSurface
Font = new Font("Microsoft JhengHei UI", 9F);
```

### 2. 頂部工具列 / 按鈕面板

```csharp
var topPanel = new Panel
{
    Dock = DockStyle.Top,
    Height = 48,
    BackColor = Color.FromArgb(31, 78, 121)  // NavyPanel
};
```

### 3. 按鈕統一主題（呼叫輔助方法）

將下列靜態方法加入 Form 類別，在建構後呼叫 `ApplyButtonTheme(topPanel)`：

```csharp
private static void ApplyButtonTheme(Panel panel)
{
    foreach (Control c in panel.Controls)
    {
        if (c is not Button btn) continue;
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = Color.FromArgb(41, 128, 185);   // CobaltBlue
        btn.ForeColor = Color.White;
        btn.Font = new Font("Microsoft JhengHei UI", 8.5F);
        btn.FlatAppearance.BorderColor = Color.FromArgb(31, 97, 141);  // DeepBlue
    }
}
```

> ⚠ 危險按鈕（ABORT / DELETE）在呼叫後需覆寫：
> ```csharp
> btnAbort.BackColor = Color.FromArgb(160, 60, 60);  // DangerRed
> btnAbort.ForeColor = Color.White;
> ```

### 4. TabControl 各頁面

```csharp
foreach (TabPage tp in tabMain.TabPages)
    tp.BackColor = Color.FromArgb(240, 245, 250);  // IceSurface
```

### 5. ListView（表格清單）

```csharp
listView.BackColor = Color.FromArgb(248, 251, 254);  // TableBg
listView.Font = new Font("Consolas", 8.5F);           // 等寬方便對齊
```

### 6. 控制台 / Log RichTextBox

```csharp
txtLog.BackColor = Color.FromArgb(15, 28, 48);    // LogBg
txtLog.ForeColor = Color.FromArgb(120, 200, 240); // LogText
txtLog.Font = new Font("Consolas", 9F);
```

### 7. Script / 編輯用 RichTextBox

```csharp
txtScript.BackColor = Color.FromArgb(22, 38, 60);    // 稍亮的深藍
txtScript.ForeColor = Color.FromArgb(189, 215, 238); // StatusText
txtScript.Font = new Font("Consolas", 10F);
```

### 8. TreeView（指令樹）

```csharp
tvCommands.BackColor = Color.FromArgb(242, 248, 253);  // TreeBg
tvCommands.ForeColor = Color.FromArgb(20, 50, 80);     // TextDark
tvCommands.BorderStyle = BorderStyle.None;

// 節點建立時：
var group = new TreeNode("S1")
{
    ForeColor = Color.FromArgb(31, 78, 121),
    NodeFont = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
    BackColor = Color.FromArgb(200, 224, 243)  // TreeGroupBg
};
var child = new TreeNode("S1F1 AreYouThere")
{
    ForeColor = Color.FromArgb(20, 50, 80),
    Tag = cmdObject
};
```

### 9. GroupBox / Label

```csharp
// GroupBox 本身不需設 BackColor，繼承頁面
foreach (Control c in grpBox.Controls)
    if (c is Label lbl) lbl.ForeColor = Color.FromArgb(44, 62, 80);  // TextMid
```

### 10. StatusStrip

```csharp
statusStrip.BackColor = Color.FromArgb(31, 78, 121);          // NavyPanel
toolStripLabel.ForeColor = Color.FromArgb(189, 215, 238);     // StatusText
```

### 11. 連線狀態標籤

```csharp
// 連線中
lblStatus.ForeColor = Color.FromArgb(120, 200, 240);  // LogText
lblStatus.Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold);
// 斷線
lblStatus.ForeColor = Color.FromArgb(160, 170, 180);
```

---

## ApplyTheme() 完整範本

完整的 `ApplyTheme()` 函式範本（可直接複製到 Form 類別）請參考：
[assets/apply-theme-template.cs](./assets/apply-theme-template.cs)

---

## 注意事項

1. **`ApplyButtonTheme` 需在控制項加入 Panel 之後呼叫**，否則找不到子控制項。
2. 若使用 `Designer.cs`（partial class），在 `InitializeComponent()` 中直接設定顏色；若是程式碼建構，在 `BuildUi()` 最後呼叫 `ApplyTheme()`。
3. **`FlatStyle.Flat`** 搭配 `FlatAppearance.BorderColor` 才能呈現正確邊框。若 `FlatStyle = FlatStyle.System`，`FlatAppearance` 屬性無效。
4. Log/Console 區塊刻意使用深底淺字仿終端機，要維持此風格避免改為淺底。
