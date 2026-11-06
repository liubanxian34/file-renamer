using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Renamer
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<FileItem> Items { get; private set; }

        private readonly ObservableCollection<Rule> _rules = new ObservableCollection<Rule>();
        private List<RenameOp> _lastOps = new List<RenameOp>();
        private bool _ready;

        public MainWindow()
        {
            InitializeComponent();

            Items = new ObservableCollection<FileItem>();
            DataContext = this;
            RuleList.ItemsSource = _rules;

            _rules.CollectionChanged += (s, e) => Recompute();
            _ready = true;

            Loaded += (s, e) =>
            {
                CreateDir.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                Recompute();
                RebuildCreatePreview();
            };
        }

        // ------------------------------------------------------------ 添加文件
        private void AddFiles(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "选择要改名的文件" };
            if (dlg.ShowDialog(this) == true) AddPaths(dlg.FileNames);
        }

        private void AddFolders(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选一个文件夹（会加入里面的文件和子文件夹）" };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) AddPaths(new[] { dlg.SelectedPath });
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            int added = 0;
            foreach (var p in paths)
            {
                try
                {
                    if (Directory.Exists(p))
                    {
                        var di = new DirectoryInfo(p);
                        foreach (var f in di.GetFiles()) added += AddOne(FileItem.FromFile(f.FullName));
                        foreach (var d in di.GetDirectories()) added += AddOne(FileItem.FromDir(d.FullName));
                    }
                    else if (File.Exists(p))
                    {
                        added += AddOne(FileItem.FromFile(p));
                    }
                }
                catch { }
            }
            SortItems();
            Recompute();
            SetStatus("加入 " + added + " 项");
        }

        private int AddOne(FileItem item)
        {
            if (Items.Any(x => string.Equals(x.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase))) return 0;
            Items.Add(item);
            return 1;
        }

        private void ClearList(object sender, RoutedEventArgs e)
        {
            Items.Clear();
            Recompute();
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null) AddPaths(paths);
        }

        // ------------------------------------------------------------ 排序
        private void OnSortChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            SortItems();
            Recompute();
        }

        private void SortItems()
        {
            var list = Items.ToList();
            int mode = SortBox.SelectedIndex;
            var cmp = new NaturalComparer();
            switch (mode)
            {
                case 1: list = list.OrderBy(x => x.Modified).ThenBy(x => x.Name, cmp).ToList(); break;
                case 2: list = list.OrderBy(x => x.Size).ThenBy(x => x.Name, cmp).ToList(); break;
                case 3: list = list.OrderBy(x => x.FullPath, cmp).ToList(); break;
                default: list = list.OrderBy(x => x.Name, cmp).ToList(); break;
            }
            for (int i = 0; i < list.Count; i++)
            {
                int cur = Items.IndexOf(list[i]);
                if (cur != i) Items.Move(cur, i);
            }
        }

        // ------------------------------------------------------------ 规则
        private void AddRule(object sender, RoutedEventArgs e)
        {
            var kind = (RuleKind)Math.Max(0, KindBox.SelectedIndex);
            var rule = Rule.Create(kind);
            var win = new RuleWindow(rule) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _rules.Add(rule);
                Recompute();
            }
        }

        private void RuleRowMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var fe = sender as FrameworkElement;
            var rule = fe == null ? null : fe.DataContext as Rule;
            if (rule == null) return;
            var win = new RuleWindow(rule) { Owner = this };
            if (win.ShowDialog() == true) Recompute();
        }

        private void RuleUp(object sender, RoutedEventArgs e)
        {
            var rule = (sender as FrameworkElement).Tag as Rule
                       ?? (Rule)(sender as FrameworkElement).DataContext;
            int i = _rules.IndexOf(rule);
            if (i > 0) _rules.Move(i, i - 1);
        }

        private void RuleDown(object sender, RoutedEventArgs e)
        {
            var rule = (sender as FrameworkElement).Tag as Rule
                       ?? (Rule)(sender as FrameworkElement).DataContext;
            int i = _rules.IndexOf(rule);
            if (i >= 0 && i < _rules.Count - 1) _rules.Move(i, i + 1);
        }

        private void RuleDel(object sender, RoutedEventArgs e)
        {
            var rule = (sender as FrameworkElement).Tag as Rule
                       ?? (Rule)(sender as FrameworkElement).DataContext;
            _rules.Remove(rule);
        }

        private void RuleToggle(object sender, RoutedEventArgs e)
        {
            Recompute();
        }

        // ------------------------------------------------------------ 计算
        private void Recompute()
        {
            Engine.Recompute(Items.ToList(), _rules.ToList());

            int changed = Items.Count(x => x.Changed);
            int bad = Items.Count(x => x.Changed && x.Status != "");
            Summary.Text = "共 " + Items.Count + " 项，将改名 " + (changed - bad) + " 项" +
                           (bad > 0 ? "，有 " + bad + " 项存在问题" : "");
            if (bad > 0) Summary.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
            else Summary.Foreground = (Brush)FindResource("Sub");
        }

        private void CheckAll(object sender, RoutedEventArgs e)
        {
            foreach (var it in Items) it.IsChecked = true;
        }

        private void UncheckAll(object sender, RoutedEventArgs e)
        {
            foreach (var it in Items) it.IsChecked = false;
        }

        // ------------------------------------------------------------ 执行
        private void RunRename(object sender, RoutedEventArgs e)
        {
            var todo = Items.Where(x => x.IsChecked && x.Status == "" && x.Changed).ToList();
            if (todo.Count == 0)
            {
                SetStatus("没有需要改的名字");
                MessageBox.Show(this, "没有可执行的项目。先添加规则，或检查列表里的红色项。", "批量改名器");
                return;
            }
            int bad = Items.Count(x => x.IsChecked && x.Changed && x.Status != "");
            string msg = "将改名 " + todo.Count + " 项。";
            if (bad > 0) msg += "\n另有 " + bad + " 项有问题（重名 / 已存在等），会被跳过。";
            msg += "\n\n改完可以在「撤销上一批」里整体还原。";
            if (MessageBox.Show(this, msg, "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

            var result = Engine.Execute(todo);
            foreach (var op in result.Done)
            {
                var it = Items.FirstOrDefault(x => string.Equals(x.FullPath, op.From, StringComparison.OrdinalIgnoreCase));
                if (it != null)
                {
                    it.Name = Path.GetFileName(op.To);
                    it.FullPath = op.To;
                }
            }
            _lastOps = result.Done;
            Recompute();

            string s = "完成：" + result.Done.Count + " 项";
            if (result.Errors.Count > 0) s += "，失败 " + result.Errors.Count + " 项";
            SetStatus(s);
            if (result.Errors.Count > 0)
                MessageBox.Show(this, s + "\n\n" + string.Join("\n", result.Errors.Take(8)), "部分失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void UndoLast(object sender, RoutedEventArgs e)
        {
            if (_lastOps == null || _lastOps.Count == 0)
            {
                SetStatus("没有可撤销的记录");
                return;
            }
            if (MessageBox.Show(this, "把上一批改的 " + _lastOps.Count + " 项名字改回去？", "撤销", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;

            Engine.Undo(_lastOps);
            foreach (var op in _lastOps)
            {
                var it = Items.FirstOrDefault(x => string.Equals(x.FullPath, op.To, StringComparison.OrdinalIgnoreCase));
                if (it != null)
                {
                    it.Name = Path.GetFileName(op.From);
                    it.FullPath = op.From;
                }
            }
            _lastOps = new List<RenameOp>();
            Recompute();
            SetStatus("已撤销");
        }

        // ------------------------------------------------------------ 批量新建
        private void PickCreateDir(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选目标文件夹" };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CreateDir.Text = dlg.SelectedPath;
                RebuildCreatePreview();
            }
        }

        private void OnCreateKindChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            RebuildCreatePreview();
        }

        private void OnCreateChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RebuildCreatePreview();
        }

        private static int I(string s, int fallback)
        {
            int v;
            return int.TryParse((s ?? "").Trim(), out v) ? v : fallback;
        }

        private void RebuildCreatePreview()
        {
            if (CreatePreview == null) return;

            bool folder = CreateKind.SelectedIndex == 0;
            int mode = Math.Max(0, CreateMode.SelectedIndex);
            CreateExt.IsEnabled = !folder;
            ListLabel.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
            CreateList.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;

            string dir = (CreateDir.Text ?? "").Trim();
            if (dir.Length == 0) dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var plan = Creator.Plan(dir, folder, CreateExt.Text, mode,
                I(CreateStart.Text, 1), I(CreateStep.Text, 1), I(CreateDigits.Text, 3),
                I(CreateCount.Text, 10), CreatePrefix.Text, CreateSuffix.Text, CreateList.Text);

            CreatePreview.Items.Clear();
            foreach (var p in plan.Take(300)) CreatePreview.Items.Add(Path.GetFileName(p));
            CreateSummary.Text = "将创建 " + plan.Count + " 项" + (plan.Count > 300 ? "（这里只显示前 300）" : "");
        }

        private void RunCreate(object sender, RoutedEventArgs e)
        {
            bool folder = CreateKind.SelectedIndex == 0;
            int mode = Math.Max(0, CreateMode.SelectedIndex);
            string dir = (CreateDir.Text ?? "").Trim();
            if (dir.Length == 0 || !Directory.Exists(dir))
            {
                MessageBox.Show(this, "先选一个存在的目标文件夹。", "批量新建");
                return;
            }

            var plan = Creator.Plan(dir, folder, CreateExt.Text, mode,
                I(CreateStart.Text, 1), I(CreateStep.Text, 1), I(CreateDigits.Text, 3),
                I(CreateCount.Text, 10), CreatePrefix.Text, CreateSuffix.Text, CreateList.Text);
            if (plan.Count == 0)
            {
                MessageBox.Show(this, "没有要创建的名字。", "批量新建");
                return;
            }
            if (MessageBox.Show(this, "在\n" + dir + "\n下创建 " + plan.Count + " 个" + (folder ? "文件夹" : "文件") + "？",
                    "确认", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;

            List<string> errors;
            int ok = Creator.Run(plan, folder, out errors);
            SetStatus("新建完成：" + ok + " 项" + (errors.Count > 0 ? "，跳过 " + errors.Count + " 项" : ""));
            if (errors.Count > 0)
                MessageBox.Show(this, string.Join("\n", errors.Take(10)), "部分未创建", MessageBoxButton.OK, MessageBoxImage.Information);
            RebuildCreatePreview();
        }

        private void SetStatus(string text)
        {
            StatusText.Text = text;
        }
    }
}
