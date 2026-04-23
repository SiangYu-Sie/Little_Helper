# 完整色票參考

## 主色系

| Token | HEX | RGB | 用途 |
|-------|-----|-----|------|
| `IceSurface` | `#F0F5FA` | `240,245,250` | Form 背景、TabPage 背景 |
| `NavyPanel` | `#1F4E79` | `31,78,121` | 頂部工具列面板、StatusStrip、深色 Panel |
| `CobaltBlue` | `#2980B9` | `41,128,185` | 一般按鈕 BackColor |
| `DeepBlue` | `#1F619D` | `31,97,141` | 按鈕 FlatAppearance.BorderColor |
| `NavyDark` | `#143250` | `20,50,80` | 重要按鈕（Connect、Lifecycle）|

## 輔助色

| Token | HEX | RGB | 用途 |
|-------|-----|-----|------|
| `TableBg` | `#F8FBFE` | `248,251,254` | ListView BackColor |
| `GroupHeaderBg` | `#D6E8F8` | `214,232,248` | 標題 Label 背景、輸入框底色 |
| `SectionBg` | `#EBF5FD` | `235,245,253` | 次要說明標籤背景 |
| `WorkAreaBg` | `#F8FBFE` | `248,251,254` | Panel/Panel2 作業區 |
| `TreeBg` | `#F2F8FD` | `242,248,253` | TreeView 背景 |
| `TreeGroupBg` | `#C8E0F3` | `200,224,243` | TreeView 群組節點底色 |

## 文字色

| Token | HEX | RGB | 用途 |
|-------|-----|-----|------|
| `TextDark` | `#1F4E79` | `31,78,121` | 主要標題、群組節點文字 |
| `TextMid` | `#2C3E50` | `44,62,80` | 一般 Label |
| `SubText` | `#506070` | `80,96,112` | 說明文字、次要資訊 |
| `TreeNodeText` | `#143250` | `20,50,80` | TreeView 葉節點 |
| `StatusText` | `#BDD7EE` | `189,215,238` | StatusStrip、深底面板上的 Label |
| `InputLabel` | `#BDD7EE` | `189,215,238` | 深色 Panel 上的輸入框 Label |

## 危險 / 警示色

| Token | HEX | RGB | 用途 |
|-------|-----|-----|------|
| `DangerRed` | `#A03C3C` | `160,60,60` | ABORT / DELETE 按鈕 |
| `LogAlert` | `#FFA864` | `255,168,100` | Alert / Warning log 文字 |

## 控制台 (Log / Console)

| Token | HEX | RGB | 用途 |
|-------|-----|-----|------|
| `LogBg` | `#0F1C30` | `15,28,48` | Log RichTextBox 背景 |
| `LogText` | `#78C8F0` | `120,200,240` | 一般 log 訊息 |
| `LogData` | `#56CCF2` | `86,204,242` | 資料屬性顯示（CJ/PJ Attr） |
| `LogAlert` | `#FFA864` | `255,168,100` | 警報、錯誤訊息 |
| `ScriptBg` | `#16263C` | `22,38,60` | Script 編輯區背景 |
| `ScriptText` | `#BDD7EE` | `189,215,238` | Script 編輯區文字 |

## C# Color.FromArgb 速查

```csharp
// 複製所需行貼上即可
Color IceSurface    = Color.FromArgb(240, 245, 250);
Color NavyPanel     = Color.FromArgb(31,  78,  121);
Color CobaltBlue    = Color.FromArgb(41,  128, 185);
Color DeepBlue      = Color.FromArgb(31,  97,  141);
Color NavyDark      = Color.FromArgb(20,  50,  80);
Color TableBg       = Color.FromArgb(248, 251, 254);
Color GroupHeaderBg = Color.FromArgb(214, 232, 248);
Color TreeBg        = Color.FromArgb(242, 248, 253);
Color TreeGroupBg   = Color.FromArgb(200, 224, 243);
Color TextDark      = Color.FromArgb(31,  78,  121);
Color TextMid       = Color.FromArgb(44,  62,  80);
Color SubText       = Color.FromArgb(80,  96,  112);
Color StatusText    = Color.FromArgb(189, 215, 238);
Color DangerRed     = Color.FromArgb(160, 60,  60);
Color LogBg         = Color.FromArgb(15,  28,  48);
Color LogText       = Color.FromArgb(120, 200, 240);
Color LogData       = Color.FromArgb(86,  204, 242);
Color LogAlert      = Color.FromArgb(255, 168, 100);
```
