using System;
using System.IO;
using System.Text;
using JungleBooze.Core.Save;

namespace JungleBooze.Services.Save
{
    /// <summary>
    /// Save file in a folder (the game uses <c>Application.persistentDataPath</c>). Atomic writes (ARCHITECTURE §9):
    /// the text goes to <c>save.json.tmp</c>, is flushed, then replaces <c>save.json</c>; the previous file becomes
    /// <c>save.json.bak</c>. A crash between steps always leaves a readable file. Allocates; load/save only.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        public const string DefaultFileName = "save.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _directory;

        public FileSaveStorage(string directory, string fileName = DefaultFileName)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("A save folder is required.", nameof(directory));
            }

            _directory = directory;
            PrimaryPath = Path.Combine(directory, string.IsNullOrEmpty(fileName) ? DefaultFileName : fileName);
            TempPath = PrimaryPath + ".tmp";
            BackupPath = PrimaryPath + ".bak";
        }

        public string PrimaryPath { get; }

        public string TempPath { get; }

        public string BackupPath { get; }

        public string Description => PrimaryPath;

        public string ReadPrimary()
        {
            return ReadOrNull(PrimaryPath);
        }

        public string ReadBackup()
        {
            return ReadOrNull(BackupPath);
        }

        public void Write(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Directory.CreateDirectory(_directory);
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Utf8NoBom.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(PrimaryPath))
            {
                try
                {
                    File.Replace(TempPath, PrimaryPath, BackupPath, true);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                }
                catch (IOException)
                {
                }

                File.Copy(PrimaryPath, BackupPath, true);
                File.Delete(PrimaryPath);
            }

            File.Move(TempPath, PrimaryPath);
        }

        private static string ReadOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Utf8NoBom) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
