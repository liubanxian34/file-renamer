using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Renamer
{
    public enum RuleKind { Replace, Insert, Delete, Number, Date, Case, Clean, Ext }

    public enum InsertAt { Start, End, AfterPos, ReplaceAll }

    public enum DateSource { Modified, Created, Now }

    public enum CaseMode { Upper, Lower, Title }

    /// <summary>
    /// 一条改名规则。所有规则串成一条链，按顺序作用在文件名上。
    /// 字段比较多是有意的：一个类型的参数全放这里，规则编辑和界面绑定都简单。
    /// </summary>
    public class Rule
    {
        public RuleKind Kind { get; set; }
        public bool Enabled { get; set; }

        // 查找替换
        public string Find { get; set; }
        public string Replace { get; set; }
        public bool RegexOn { get; set; }
        public bool IgnoreCase { get; set; }
        public bool NameOnly { get; set; }

        // 插入 / 位置类
        public string Text { get; set; }
        public InsertAt At { get; set; }
        public int Pos { get; set; }

        // 删除
        public int From { get; set; }
        public int Count { get; set; }
        public bool BySeparator { get; set; }
        public string Separator { get; set; }
        public int Segment { get; set; }

        // 序号
        public int Start { get; set; }
        public int Step { get; set; }
        public int Digits { get; set; }
        public bool ReplaceName { get; set; }

        // 日期
        public DateSource DateFrom { get; set; }
        public string DateFormat { get; set; }

        // 大小写
        public CaseMode Case { get; set; }

        // 清理
        public bool TrimSpace { get; set; }
        public bool CollapseSpace { get; set; }
        public bool RemoveBrackets { get; set; }
        public bool RemoveSpecial { get; set; }
        public bool FullWidth { get; set; }

        // 扩展名
        public string NewExt { get; set; }

        public Rule()
        {
            Enabled = true;
            IgnoreCase = true;
            NameOnly = true;
            Step = 1;
            Digits = 2;
            Start = 1;
            At = InsertAt.Start;
            NumAt = InsertAt.End;
            From = 1;
            Count = 0;
            Segment = 1;
            Separator = "-";
            IgnoreCase = true;
            DateFormat = "yyyyMMdd";
            DateAt = InsertAt.Start;
            Case = CaseMode.Lower;
            TrimSpace = true;
            CollapseSpace = true;
        }

        public InsertAt NumAt { get; set; } = InsertAt.End;
        public InsertAt DateAt { get; set; } = InsertAt.Start;

        public static Rule Create(RuleKind kind)
        {
            var r = new Rule { Kind = kind };
            switch (kind)
            {
                case RuleKind.Replace:
                    r.Find = ""; r.Replace = ""; break;
                case RuleKind.Insert:
                    r.Text = ""; r.At = InsertAt.Start; break;
                case RuleKind.Delete:
                    r.From = 1; r.Count = 0; break;
                case RuleKind.Number:
                    r.Start = 1; r.Step = 1; r.Digits = 2; r.NumAt = InsertAt.End; break;
                case RuleKind.Date:
                    r.DateFrom = DateSource.Modified; r.DateFormat = "yyyyMMdd"; break;
                case RuleKind.Case:
                    r.Case = CaseMode.Lower; break;
                case RuleKind.Ext:
                    r.NewExt = ""; break;
            }
            return r;
        }

        public string Title
        {
            get
            {
                switch (Kind)
                {
                    case RuleKind.Replace: return "查找替换";
                    case RuleKind.Insert: return "插入文字";
                    case RuleKind.Delete: return "删除 / 截取";
                    case RuleKind.Number: return "序号";
                    case RuleKind.Date: return "日期";
                    case RuleKind.Case: return "大小写";
                    case RuleKind.Clean: return "清理字符";
                    case RuleKind.Ext: return "扩展名";
                }
                return "规则";
            }
        }

        public string Detail
        {
            get
            {
                switch (Kind)
                {
                    case RuleKind.Replace:
                        return (RegexOn ? "正则 " : "") + "「" + Find + "」→「" + Replace + "」" + (NameOnly ? "（不含扩展名）" : "");
                    case RuleKind.Insert:
                        return "插入「" + Text + "」" + AtText(At, Pos);
                    case RuleKind.Delete:
                        return BySeparator
                            ? "按「" + Separator + "」分段，删掉第 " + Segment + " 段"
                            : "从第 " + From + " 个字符删 " + (Count == 0 ? "到末尾" : Count + " 个");
                    case RuleKind.Number:
                        return "序号 " + Start + " 起，步长 " + Step + "，" + Digits + " 位" +
                               (ReplaceName ? "，替换整个名称" : "，" + AtText(NumAt, Pos));
                    case RuleKind.Date:
                        return DateName + "，格式 " + DateFormat + "，" + AtText(DateAt, Pos);
                    case RuleKind.Case:
                        return Case == CaseMode.Upper ? "全部大写" : Case == CaseMode.Lower ? "全部小写" : "首字母大写";
                    case RuleKind.Clean:
                        {
                            var sb = new StringBuilder();
                            if (TrimSpace) sb.Append("去首尾空格 ");
                            if (CollapseSpace) sb.Append("合并空格 ");
                            if (RemoveBrackets) sb.Append("去括号内容 ");
                            if (RemoveSpecial) sb.Append("去特殊符号 ");
                            if (FullWidth) sb.Append("全角转半角 ");
                            return sb.Length == 0 ? "没有勾选任何项" : sb.ToString().Trim();
                        }
                    case RuleKind.Ext:
                        return string.IsNullOrWhiteSpace(NewExt) ? "去掉扩展名" : "扩展名改成 " + NewExt;
                }
                return "";
            }
        }

        private string DateName
        {
            get { return DateFrom == DateSource.Created ? "创建时间" : DateFrom == DateSource.Now ? "当前时间" : "修改时间"; }
        }

        private static string AtText(InsertAt at, int pos)
        {
            switch (at)
            {
                case InsertAt.Start: return "加到开头";
                case InsertAt.End: return "加到结尾";
                case InsertAt.AfterPos: return "插到第 " + pos + " 个字符后";
                case InsertAt.ReplaceAll: return "替换整个名称";
            }
            return "";
        }

        /// <summary>把规则作用到一个完整文件名上（含扩展名）</summary>
        public string Apply(string fullName, int index, DateTime modified, DateTime created)
        {
            if (!Enabled || string.IsNullOrEmpty(fullName)) return fullName;

            string ext = Path.GetExtension(fullName) ?? "";
            string stem = fullName.Substring(0, fullName.Length - ext.Length);

            switch (Kind)
            {
                case RuleKind.Replace:
                    if (!NameOnly) return DoReplace(fullName);
                    return DoReplace(stem) + ext;

                case RuleKind.Insert:
                    return Put(stem, Text ?? "", At, Pos) + ext;

                case RuleKind.Delete:
                    return Cut(stem) + ext;

                case RuleKind.Number:
                    {
                        long n = Start + (long)index * Step;
                        string num = n.ToString().PadLeft(Math.Max(1, Digits), '0');
                        if (ReplaceName) return num + ext;
                        return Put(stem, num, NumAt, Pos) + ext;
                    }

                case RuleKind.Date:
                    {
                        DateTime d = DateFrom == DateSource.Created ? created
                                   : DateFrom == DateSource.Now ? DateTime.Now : modified;
                        string t;
                        try { t = d.ToString(string.IsNullOrWhiteSpace(DateFormat) ? "yyyyMMdd" : DateFormat); }
                        catch { t = d.ToString("yyyyMMdd"); }
                        return Put(stem, t, DateAt, Pos) + ext;
                    }

                case RuleKind.Case:
                    if (Case == CaseMode.Upper) return stem.ToUpper() + ext;
                    if (Case == CaseMode.Lower) return stem.ToLower() + ext;
                    return TitleCase(stem) + ext;

                case RuleKind.Clean:
                    return Clean(stem) + ext;

                case RuleKind.Ext:
                    {
                        string e = (NewExt ?? "").Trim();
                        if (e.Length == 0) return stem;
                        if (!e.StartsWith(".")) e = "." + e;
                        return stem + e;
                    }
            }
            return fullName;
        }

        private string DoReplace(string s)
        {
            if (string.IsNullOrEmpty(Find)) return s;
            try
            {
                if (RegexOn)
                {
                    var opt = RegexOptions.None;
                    if (IgnoreCase) opt |= RegexOptions.IgnoreCase;
                    return Regex.Replace(s, Find, Replace ?? "", opt);
                }
                if (IgnoreCase)
                    return Regex.Replace(s, Regex.Escape(Find), (Replace ?? "").Replace("$", "$$"), RegexOptions.IgnoreCase);
                return s.Replace(Find, Replace ?? "");
            }
            catch
            {
                return s;
            }
        }

        private static string Put(string s, string text, InsertAt at, int pos)
        {
            if (string.IsNullOrEmpty(text)) return s;
            switch (at)
            {
                case InsertAt.Start: return text + s;
                case InsertAt.End: return s + text;
                case InsertAt.AfterPos:
                    {
                        int p = pos;
                        if (p < 0) p = 0;
                        if (p > s.Length) p = s.Length;
                        return s.Insert(p, text);
                    }
                case InsertAt.ReplaceAll: return text;
            }
            return s;
        }

        private string Cut(string s)
        {
            if (BySeparator)
            {
                string sep = string.IsNullOrEmpty(Separator) ? "-" : Separator;
                var parts = s.Split(new[] { sep }, StringSplitOptions.None).ToList();
                int i = Segment - 1;
                if (i >= 0 && i < parts.Count) parts.RemoveAt(i);
                return string.Join(sep, parts).Trim();
            }
            if (From < 1) return s;
            int start = From - 1;
            if (start >= s.Length) return s;
            int len = Count <= 0 ? s.Length - start : Math.Min(Count, s.Length - start);
            return s.Remove(start, len);
        }

        private string Clean(string s)
        {
            if (TrimSpace) s = s.Trim();
            if (CollapseSpace) s = Regex.Replace(s, @"\s+", " ");
            if (RemoveBrackets) s = Regex.Replace(s, @"[\(（\[【][^\)）\]】]*[\)）\]】]", "");
            if (RemoveSpecial)
            {
                const string junk = "!@#$%^&*+=|\\:;\"'<>,?/`~·—、,。！？【】《》「」“”‘’";
                var sb = new StringBuilder();
                foreach (char c in s)
                {
                    if (junk.IndexOf(c) >= 0) continue;
                    if (char.IsControl(c)) continue;
                    if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0) continue;
                    sb.Append(c);
                }
                s = sb.ToString();
            }
            if (FullWidth) s = ToHalfWidth(s);
            return s.Trim();
        }

        private static string ToHalfWidth(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == '\u3000') sb.Append(' ');
                else if (c >= '\uFF01' && c <= '\uFF5E') sb.Append((char)(c - 0xFEE0));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string TitleCase(string s)
        {
            var chars = s.ToCharArray();
            bool up = true;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetter(c))
                {
                    chars[i] = up ? char.ToUpper(c) : char.ToLower(c);
                    up = false;
                }
                else
                {
                    up = c == ' ' || c == '-' || c == '_';
                }
            }
            return new string(chars);
        }

        public Rule Clone()
        {
            return (Rule)MemberwiseClone();
        }

        /// <summary>把另一条规则的所有参数抄过来（编辑对话框点确定时用）</summary>
        public void CopyFrom(Rule other)
        {
            if (other == null) return;
            foreach (var p in typeof(Rule).GetProperties())
            {
                if (!p.CanWrite) continue;
                var v = p.GetValue(other, null);
                p.SetValue(this, v, null);
            }
        }
    }
}
