using System;
using System.ComponentModel;
using System.IO;

namespace Renamer
{
    public class FileItem : INotifyPropertyChanged
    {
        public string FullPath { get; set; }
        public string Dir { get; set; }
        public string Name { get; set; }
        public bool IsDir { get; set; }
        public DateTime Modified { get; set; }
        public DateTime Created { get; set; }
        public long Size { get; set; }

        private bool _checked = true;
        public bool IsChecked
        {
            get { return _checked; }
            set { _checked = value; Raise("IsChecked"); }
        }

        private string _newName;
        public string NewName
        {
            get { return string.IsNullOrEmpty(_newName) ? Name : _newName; }
            set { _newName = value; Raise("NewName"); Raise("Changed"); }
        }

        private string _status = "";
        public string Status
        {
            get { return _status; }
            set { _status = value; Raise("Status"); }
        }

        public bool Changed
        {
            get { return !string.Equals(Name, NewName, StringComparison.Ordinal); }
        }

        public string SizeText
        {
            get
            {
                if (IsDir) return "文件夹";
                double b = Size;
                string[] u = { "B", "KB", "MB", "GB", "TB" };
                int i = 0;
                while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
                return i == 0 ? ((long)b) + " B" : b.ToString("0.#") + " " + u[i];
            }
        }

        public static FileItem FromFile(string path)
        {
            var fi = new FileInfo(path);
            return new FileItem
            {
                FullPath = fi.FullName,
                Dir = fi.DirectoryName,
                Name = fi.Name,
                IsDir = false,
                Modified = fi.LastWriteTime,
                Created = fi.CreationTime,
                Size = fi.Length
            };
        }

        public static FileItem FromDir(string path)
        {
            var di = new DirectoryInfo(path);
            return new FileItem
            {
                FullPath = di.FullName.TrimEnd(Path.DirectorySeparatorChar),
                Dir = di.Parent != null ? di.Parent.FullName : di.FullName,
                Name = di.Name,
                IsDir = true,
                Modified = di.LastWriteTime,
                Created = di.CreationTime,
                Size = 0
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string p)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(p));
        }
    }
}
