// ─────────────────────────────────────────────────────────────────
// ApplyTheme() – 完整範本（貼入 Form partial class 後呼叫）
// 在建構子最後一行加入：ApplyTheme();
// ─────────────────────────────────────────────────────────────────

private void ApplyTheme()
{
    // ── 1. Form 背景 ──
    BackColor = Color.FromArgb(240, 245, 250);  // IceSurface

    // ── 2. TabPage 背景 ──
    // 將 tabMain 換成你的 TabControl 變數名
    foreach (TabPage tp in tabMain.TabPages)
        tp.BackColor = Color.FromArgb(240, 245, 250);

    // ── 3. ListView 背景 ──
    // 列出所有 ListView 控制項
    foreach (var lv in new ListView[] { lvMessages, lvEquipStatus, lvAlarms /*, 其他... */ })
        lv.BackColor = Color.FromArgb(248, 251, 254);  // TableBg

    // ── 4. Log / Console RichTextBox ──
    // 一般 log（藍綠色）
    txtStatusLog.BackColor = Color.FromArgb(15, 28, 48);
    txtStatusLog.ForeColor = Color.FromArgb(120, 200, 240);

    // 資料屬性欄（青藍）
    // txtCJAttr.BackColor = Color.FromArgb(15, 28, 48);
    // txtCJAttr.ForeColor = Color.FromArgb(86, 204, 242);

    // 警報/事件欄（橙色）
    // txtPJAlert.BackColor = Color.FromArgb(15, 28, 48);
    // txtPJAlert.ForeColor = Color.FromArgb(255, 168, 100);

    // ── 5. Script 編輯區 ──
    // txtScript.BackColor = Color.FromArgb(22, 38, 60);
    // txtScript.ForeColor = Color.FromArgb(189, 215, 238);
    // txtScript.Font = new Font("Consolas", 10F);

    // ── 6. StatusStrip ──
    statusStrip.BackColor = Color.FromArgb(31, 78, 121);
    toolStripStatus.ForeColor = Color.FromArgb(189, 215, 238);

    // ── 7. GroupBox 內的 Label 文字色 ──
    foreach (Control c in grpConnectionSettings.Controls)
        if (c is Label lbl) lbl.ForeColor = Color.FromArgb(44, 62, 80);  // TextMid

    // ── 8. 各工具列面板按鈕（功能頁）──
    // 傳入包含工具列 Panel 的 TabPage 或 Panel
    ApplyPanelButtonTheme(tabRecipe);
    ApplyPanelButtonTheme(tabControlJob);
    ApplyPanelButtonTheme(tabProcessJob);
    ApplyPanelButtonTheme(tabScript);

    // ── 9. 危險按鈕覆寫 ──
    btnCJAbort.BackColor  = Color.FromArgb(160, 60, 60);  // DangerRed
    btnCJAbort.ForeColor  = Color.White;
    btnPJDelete.BackColor = Color.FromArgb(160, 60, 60);
    btnPJDelete.ForeColor = Color.White;
}

// ─── 遞迴將 container 底下第一層 Panel 內的所有 Button 套用藍色扁平風格 ───
private static void ApplyPanelButtonTheme(Control container)
{
    foreach (Control c in container.Controls)
    {
        if (c is Panel pnl)
        {
            pnl.BackColor = Color.FromArgb(31, 78, 121);  // NavyPanel
            foreach (Control child in pnl.Controls)
            {
                if (child is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.BackColor = Color.FromArgb(41, 128, 185);   // CobaltBlue
                    btn.ForeColor = Color.White;
                    btn.Font = new Font("Microsoft JhengHei UI", 8.5F);
                    btn.FlatAppearance.BorderColor = Color.FromArgb(31, 97, 141);  // DeepBlue
                }
            }
        }
    }
}

// ─── 頂部 Panel 所有按鈕套用主題（程式碼構建 UI 的 Form 使用）───
private static void ApplyTopPanelButtonTheme(Panel topPanel)
{
    foreach (Control c in topPanel.Controls)
    {
        if (c is not Button btn) continue;
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = Color.FromArgb(41, 128, 185);
        btn.ForeColor = Color.White;
        btn.Font = new Font("Microsoft JhengHei UI", 8.5F);
        btn.FlatAppearance.BorderColor = Color.FromArgb(31, 97, 141);
    }
    // 特殊角色按鈕呼叫後再覆寫：
    // btnConnect.BackColor        = Color.FromArgb(23, 105, 170);  // 深一點的藍
    // btnRunLifecycle.BackColor   = Color.FromArgb(16, 80, 130);   // 更深
}
