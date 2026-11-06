using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Renamer
{
    /// <summary>批量新建：按规则先算出名字，再一次性建文件夹或空文件</summary>
    public static class Creator
    {
        public static List<string> Plan(string dir, bool folder, string ext, int mode,
            int start, int step, int digits, int count, string prefix, string suffix, string listText)
        {
            var names = new List<string>();
            if (mode == 1)
            {
                foreach (var line in (listText ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string n = line.Trim();
                    if (n.Length > 0) names.Add(n);
                }
            }
            else
            {
                for (int i = 0; i < Math.Max(0, count); i++)
                {
                    long n = start + (long)i * step;
                    string core = n.ToString().PadLeft(Math.Max(1, digits), '0');
                    if (mode == 2) core = DateTime.Now.ToString("yyyyMMdd") + "_" + core;
                    names.Add(core);
                }
            }

            var full = new List<string>();
            foreach (var n in names)
            {
                string name = (prefix ?? "") + n + (suffix ?? "");
                if (!folder)
                {
                    string e = (ext ?? "").Trim();
                    if (e.Length > 0 && !e.StartsWith(".")) e = "." + e;
                    name += e;
                }
                full.Add(Path.Combine(dir, name));
            }
            return full;
        }

        public static int Run(List<string> paths, bool folder, out List<string> errors)
        {
            errors = new List<string>();
            int ok = 0;
            foreach (var p in paths)
            {
                try
                {
                    if (folder)
                    {
                        if (Directory.Exists(p)) { errors.Add("已存在：" + Path.GetFileName(p)); continue; }
                        Directory.CreateDirectory(p);
                    }
                    else
                    {
                        if (File.Exists(p)) { errors.Add("已存在：" + Path.GetFileName(p)); continue; }
                        string parent = Path.GetDirectoryName(p);
                        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                        using (File.Create(p)) { }
                    }
                    ok++;
                }
                catch (Exception ex)
                {
                    errors.Add(Path.GetFileName(p) + "：" + ex.Message);
                }
            }
            return ok;
        }
    }
}
