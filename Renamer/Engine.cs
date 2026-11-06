using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Renamer
{
    public class RenameOp
    {
        public string From { get; set; }
        public string To { get; set; }
        public bool IsDir { get; set; }
    }

    public class RunResult
    {
        public List<RenameOp> Done = new List<RenameOp>();
        public List<string> Errors = new List<string>();
        public int Skipped;
    }

    public static class Engine
    {
        /// <summary>把规则链套到每个名字上，再算出冲突状态</summary>
        public static void Recompute(IList<FileItem> items, IList<Rule> rules)
        {
            var active = rules.Where(r => r.Enabled).ToList();

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                string name = it.Name;
                foreach (var r in active)
                {
                    try { name = r.Apply(name, i, it.Modified, it.Created); }
                    catch { }
                }
                it.NewName = name;
            }

            // 名称合法性
            foreach (var it in items)
            {
                it.Status = Check(it);
            }

            // 同一目录内重名
            var groups = items.GroupBy(x => (x.Dir + "|" + x.NewName).ToLowerInvariant());
            foreach (var g in groups)
            {
                if (g.Count() > 1)
                    foreach (var it in g) if (it.Status == "") it.Status = "重名";
            }

            // 父文件夹也在这一批里改名的，子项先跳过（路径会失效，不能同时改）
            var movingDirs = items
                .Where(x => x.IsDir && x.Changed && x.Status == "")
                .Select(x => x.FullPath.TrimEnd('\\') + "\\")
                .ToList();
            if (movingDirs.Count > 0)
            {
                foreach (var it in items)
                {
                    if (it.Status != "") continue;
                    if (movingDirs.Any(d => it.FullPath.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
                        it.Status = "父目录也在改名";
                }
            }
        }

        private static string Check(FileItem it)
        {
            string n = it.NewName;
            if (string.IsNullOrWhiteSpace(n)) return "名称为空";
            if (n.Trim() != n) return "首尾有空格";
            if (n.EndsWith(".")) return "结尾是点";
            if (n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "含非法字符";

            string stem = Path.GetFileNameWithoutExtension(n).ToUpperInvariant();
            string[] reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (reserved.Contains(stem)) return "系统保留名";

            if (string.Equals(n, it.Name, StringComparison.Ordinal)) return ""; // 没改

            string target = Path.Combine(it.Dir, n);
            try
            {
                if (it.IsDir)
                {
                    if (Directory.Exists(target) && !string.Equals(target, it.FullPath, StringComparison.OrdinalIgnoreCase))
                        return "已存在";
                }
                else
                {
                    if (File.Exists(target) && !string.Equals(target, it.FullPath, StringComparison.OrdinalIgnoreCase))
                        return "已存在";
                }
                if (!string.IsNullOrEmpty(it.Dir) && !Directory.Exists(it.Dir)) return "目录不存在";
            }
            catch
            {
                return "路径有问题";
            }
            return "";
        }

        public static RunResult Execute(IList<FileItem> items)
        {
            var result = new RunResult();
            var ops = items
                .Where(x => x.IsChecked && x.Status == "" && x.Changed)
                .Select(x => new RenameOp
                {
                    From = x.FullPath,
                    To = Path.Combine(x.Dir, x.NewName),
                    IsDir = x.IsDir
                })
                .ToList();

            result.Skipped = items.Count(x => x.IsChecked && x.Changed && x.Status != "");
            if (ops.Count > 0) Apply(ops, result);

            Log(ops, result.Errors, "执行");
            return result;
        }

        public static void Undo(List<RenameOp> ops)
        {
            var back = ops.Select(o => new RenameOp { From = o.To, To = o.From, IsDir = o.IsDir }).ToList();
            var r = new RunResult();
            Apply(back, r);
            Log(back, r.Errors, "撤销");
        }

        /// <summary>
        /// 真正落盘。为了处理「A 改成 B，B 改成 C」这种连环改名，
        /// 只要目标撞上了本批的某个源，就先全部改成临时名再改到最终名。
        /// </summary>
        private static void Apply(List<RenameOp> ops, RunResult result)
        {
            var sources = new HashSet<string>(ops.Select(o => o.From), StringComparer.OrdinalIgnoreCase);
            bool needTemp = ops.Any(o => sources.Contains(o.To)) || ops.Any(o => OnlyCaseDiffers(o.From, o.To));

            if (!needTemp)
            {
                foreach (var op in ops)
                {
                    try
                    {
                        Move(op.From, op.To, op.IsDir);
                        result.Done.Add(op);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(op.From + " → " + ex.Message);
                    }
                }
                return;
            }

            var stage = new List<RenameOp>();
            foreach (var op in ops)
            {
                string tmp = TempName(op.From, op.IsDir);
                try
                {
                    Move(op.From, tmp, op.IsDir);
                    stage.Add(new RenameOp { From = tmp, To = op.To, IsDir = op.IsDir });
                }
                catch (Exception ex)
                {
                    result.Errors.Add(op.From + " → " + ex.Message);
                }
            }
            foreach (var op in stage)
            {
                try
                {
                    Move(op.From, op.To, op.IsDir);
                    result.Done.Add(new RenameOp { From = op.From, To = op.To, IsDir = op.IsDir });
                }
                catch (Exception ex)
                {
                    result.Errors.Add(op.From + " → " + ex.Message);
                }
            }
        }

        private static bool OnlyCaseDiffers(string a, string b)
        {
            return !string.Equals(a, b, StringComparison.Ordinal) &&
                   string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string TempName(string path, bool isDir)
        {
            string dir = isDir ? Path.GetDirectoryName(path) : Path.GetDirectoryName(path);
            string ext = isDir ? "" : Path.GetExtension(path);
            return Path.Combine(dir ?? "", "__rn_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext);
        }

        private static void Move(string from, string to, bool isDir)
        {
            if (isDir) Directory.Move(Pad(from), Pad(to));
            else File.Move(Pad(from), Pad(to));
        }

        /// <summary>超过 260 字符的路径加前缀，Win10 上能直接改</summary>
        private static string Pad(string p)
        {
            if (p.StartsWith(@"\\?\")) return p;
            if (p.Length > 240) return @"\\?\" + p;
            return p;
        }

        private static void Log(List<RenameOp> ops, List<string> errors, string action)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatchRenamer");
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine("=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + action + " " + ops.Count + " 项 ===");
                foreach (var op in ops) sb.AppendLine(op.From + "\t->\t" + op.To);
                foreach (var e in errors) sb.AppendLine("!! " + e);
                File.AppendAllText(Path.Combine(dir, "log.txt"), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    /// <summary>自然排序：2 排在 10 前面，Windows 资源管理器也是这个顺序</summary>
    public class NaturalComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null) return y == null ? 0 : -1;
            if (y == null) return 1;
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    long nx = 0, ny = 0;
                    while (i < x.Length && char.IsDigit(x[i])) { nx = nx * 10 + (x[i] - '0'); i++; }
                    while (j < y.Length && char.IsDigit(y[j])) { ny = ny * 10 + (y[j] - '0'); j++; }
                    if (nx != ny) return nx < ny ? -1 : 1;
                }
                else
                {
                    int c = char.ToLowerInvariant(x[i]).CompareTo(char.ToLowerInvariant(y[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return (x.Length - i).CompareTo(y.Length - j);
        }
    }
}
