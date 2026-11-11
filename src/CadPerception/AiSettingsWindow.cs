using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UserControl = System.Windows.Controls.UserControl;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Window = System.Windows.Window;
using PasswordBox = System.Windows.Controls.PasswordBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Path = System.Windows.Shapes.Path;

namespace CadPerceptionPlugin
{
    /// <summary>
    /// AI 助理图形化配置窗口 (CAD 原版暗黑风格)
    /// </summary>
    public class AiSettingsWindow : Window
    {
        private static readonly Color CadBackground = Color.FromRgb(0x3B, 0x44, 0x53);   // 根底色 #3B4453
        private static readonly Color CadHeaderBg = Color.FromRgb(0x2E, 0x34, 0x40);     // 顶底栏色 #2E3440
        private static readonly Color CadInputBg = Color.FromRgb(0x27, 0x2F, 0x3A);      // 输入框卡片底色 #272F3A
        private static readonly Color CadBorder = Color.FromRgb(0x4E, 0x5A, 0x6E);       // 控件边框色 #4E5A6E
        private static readonly Color CadAccent = Color.FromRgb(0x06, 0x96, 0xD7);       // 主强调色 #0696D7
        private static readonly Color CadTextMain = Color.FromRgb(0xF5, 0xF5, 0xF5);     // 主文字色 #F5F5F5
        private static readonly Color CadTextDim = Color.FromRgb(0x96, 0x9A, 0xA0);      // 辅助灰字 #969AA0

        private readonly TextBox _chatKeyBox;
        private readonly TextBox _embedKeyBox;
        private readonly TextBlock _statusMsg;

        // 用量条与指标独立开关 (参照 ZCode 用量条规范)
        private readonly CheckBox _chkUsageBar;
        private readonly CheckBox _chkSpeed;
        private readonly CheckBox _chkCost;
        private readonly CheckBox _chkTokens;
        private readonly CheckBox _chkContextBar;

        public static event Action? OnSettingsSaved;

        // 路径自适应（2026-11-10）：配置读写件随实际用户目录解析
        private static readonly string ConfigPath = CadPaths.PresetsFile;

        public AiSettingsWindow()
        {
            Title = "AI 助理配置";
            Width = 440;
            Height = 490;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(CadBackground);
            Foreground = new SolidColorBrush(CadTextMain);
            FontFamily = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI");
            FontSize = 12;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Form Body
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // Footer Buttons

            // 1. 顶栏
            var header = new Border
            {
                Background = new SolidColorBrush(CadHeaderBg),
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x29, 0x33)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerText = new TextBlock
            {
                Text = "AI 伴随智能体配置中心",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CadTextMain)
            };
            header.Child = headerText;
            Grid.SetRow(header, 0);
            rootGrid.Children.Add(header);

            // 2. 表单内容区
            var bodyScroll = new ScrollViewer { Padding = new Thickness(16, 14, 16, 10) };
            var formStack = new StackPanel();

            // Chat / Vision model key
            formStack.Children.Add(CreateFieldLabel("对话模型 API Key（Chat / Vision）:"));
            _chatKeyBox = CreateStyledTextBox("sk-...");
            formStack.Children.Add(_chatKeyBox);

            // Embedding model key
            formStack.Children.Add(CreateFieldLabel("向量模型 API Key（Embedding，可留空）:"));
            _embedKeyBox = CreateStyledTextBox("sk-...");
            formStack.Children.Add(_embedKeyBox);


            // ---------------------------------------------------------
            // 用量监控条与独立开关组 (参照 ZCode 标准规范)
            // ---------------------------------------------------------
            formStack.Children.Add(CreateFieldLabel("Token 用量与性能监控栏 (独立开关):"));

            var switchPanel = new StackPanel
            {
                Margin = new Thickness(0, 2, 0, 10)
            };

            _chkUsageBar = CreateStyledCheckBox("启用输入框上方用量胶囊条 (全局总开关)", true);
            _chkSpeed = CreateStyledCheckBox("显示 Token 生成速度 (⚡ t/s)", true);
            _chkCost = CreateStyledCheckBox("显示 API 目录折算费用 (¥ 价格)", true);
            _chkTokens = CreateStyledCheckBox("显示 Tokens 总计与明细", true);
            _chkContextBar = CreateStyledCheckBox("显示 1M 上下文占比微进度条", true);

            switchPanel.Children.Add(_chkUsageBar);
            switchPanel.Children.Add(_chkSpeed);
            switchPanel.Children.Add(_chkCost);
            switchPanel.Children.Add(_chkTokens);
            switchPanel.Children.Add(_chkContextBar);

            formStack.Children.Add(switchPanel);

            // 提示状态
            _statusMsg = new TextBlock
            {
                Text = "配置保存在 cadsys/NET/ai_presets.json，即改即生效。",
                Foreground = new SolidColorBrush(CadTextDim),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            };
            formStack.Children.Add(_statusMsg);

            bodyScroll.Content = formStack;
            Grid.SetRow(bodyScroll, 1);
            rootGrid.Children.Add(bodyScroll);

            // 3. 底部操作按钮
            var footer = new Border
            {
                Background = new SolidColorBrush(CadHeaderBg),
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x29, 0x33)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerDock = new DockPanel();

            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 72,
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(0x45, 0x4F, 0x61)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelBtn.Click += (s, e) => Close();

            var saveBtn = new Button
            {
                Content = "保存配置",
                Width = 84,
                Height = 28,
                Background = new SolidColorBrush(CadAccent),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            saveBtn.Click += (s, e) => SaveConfig();

            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            btnStack.Children.Add(cancelBtn);
            btnStack.Children.Add(saveBtn);
            footerDock.Children.Add(btnStack);
            footer.Child = footerDock;

            Grid.SetRow(footer, 2);
            rootGrid.Children.Add(footer);

            Content = rootGrid;

            // 加载当前已有配置
            LoadConfig();
        }

        private static CheckBox CreateStyledCheckBox(string text, bool defaultChecked)
        {
            return new CheckBox
            {
                Content = text,
                IsChecked = defaultChecked,
                Foreground = new SolidColorBrush(CadTextMain),
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = Cursors.Hand
            };
        }

        private static TextBlock CreateFieldLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(CadTextDim),
                FontSize = 11,
                Margin = new Thickness(0, 6, 0, 4)
            };
        }

        private static TextBox CreateStyledTextBox(string defaultText)
        {
            return new TextBox
            {
                Text = defaultText,
                Height = 30,
                Background = new SolidColorBrush(CadInputBg),
                BorderBrush = new SolidColorBrush(CadBorder),
                BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(CadTextMain),
                CaretBrush = new SolidColorBrush(CadTextMain),
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12
            };
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var node = JsonNode.Parse(json);
                    if (node != null)
                    {
                        // 实际调用链读取的是 presets[].apiKey（2026-11-10 对齐：配置窗与调用链同一份口径）
                        if (node["presets"] is JsonArray presetArr)
                        {
                            foreach (var p in presetArr)
                            {
                                string k = p?["apiKey"]?.ToString();
                                if (string.IsNullOrEmpty(k)) continue;
                                bool isEmb = p?["isEmbeddingModel"]?.GetValue<bool>() ?? false;
                                if (isEmb) _embedKeyBox.Text = k;
                                else _chatKeyBox.Text = k;
                            }
                        }

                        // 读取用量条配置
                        var usageSettings = node["usage_bar_settings"];
                        if (usageSettings != null)
                        {
                            _chkUsageBar.IsChecked = usageSettings["enabled"]?.GetValue<bool>() ?? true;
                            _chkSpeed.IsChecked = usageSettings["show_speed"]?.GetValue<bool>() ?? true;
                            _chkCost.IsChecked = usageSettings["show_cost"]?.GetValue<bool>() ?? true;
                            _chkTokens.IsChecked = usageSettings["show_tokens"]?.GetValue<bool>() ?? true;
                            _chkContextBar.IsChecked = usageSettings["show_context_bar"]?.GetValue<bool>() ?? true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _statusMsg.Text = "加载现有配置失败: " + ex.Message;
                _statusMsg.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }
        }

        private void SaveConfig()
        {
            try
            {
                JsonNode node = File.Exists(ConfigPath)
                    ? JsonNode.Parse(File.ReadAllText(ConfigPath)) ?? new JsonObject()
                    : new JsonObject();

                // 保存用量条与独立开关设置
                var usageSettings = new JsonObject
                {
                    ["enabled"] = _chkUsageBar.IsChecked ?? true,
                    ["show_speed"] = _chkSpeed.IsChecked ?? true,
                    ["show_cost"] = _chkCost.IsChecked ?? true,
                    ["show_tokens"] = _chkTokens.IsChecked ?? true,
                    ["show_context_bar"] = _chkContextBar.IsChecked ?? true
                };
                node["usage_bar_settings"] = usageSettings;

                // 同步写回 presets[]：聊天调用链只认 presets[].apiKey（只写 providers 等于改了不生效）
                if (node["presets"] is JsonArray presetArr2)
                {
                    string chatKey = _chatKeyBox.Text.Trim();
                    string embedKey = _embedKeyBox.Text.Trim();
                    foreach (var p in presetArr2)
                    {
                        bool isEmb = p?["isEmbeddingModel"]?.GetValue<bool>() ?? false;
                        p!["apiKey"] = isEmb ? embedKey : chatKey;
                    }
                }

                CadPaths.EnsureNetDir();
                File.WriteAllText(ConfigPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                OnSettingsSaved?.Invoke();
                MessageBox.Show("配置已成功保存并即时生效！", "AI 助理配置", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                _statusMsg.Text = "保存失败: " + ex.Message;
                _statusMsg.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }
        }
    }
}
