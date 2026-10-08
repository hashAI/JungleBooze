using System;
using System.IO;
using System.Text;

namespace JungleBooze.Services.Persistence
{
    /// <summary>
    /// Save file in a folder (the game uses <c>Application.persistentDataPath</c>). Writes are atomic: the new text
    /// goes to <c>save.json.tmp</c>, is flushed to disk, then renamed over <c>save.json</c>; the old file is kept
    /// as <c>save.json.bak</c>. If the rename is not supported, the fallback copies the old file to the backup
    /// first, so a crash between steps still leaves a readable backup. Allocates; call only on load and save.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        public const string DefaultFileName = "save.json";
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _directory;

        public FileSaveStorage(string directory, string fileName = DefaultFileName)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("A save folder is required.", nameof(directory));
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("A save file name is required.", nameof(fileName));
            }

            _directory = directory;
            PrimaryPath = Path.Combine(directory, fileName);
            TempPath = PrimaryPath + TempSuffix;
            BackupPath = PrimaryPath + BackupSuffix;
        }

        public string PrimaryPath { get; }

        public string TempPath { get; }

        public string BackupPath { get; }

        public string Description => PrimaryPath;

        public bool PrimaryExists => File.Exists(PrimaryPath);

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

            if (!File.Exists(PrimaryPath))
            {
                File.Move(TempPath, PrimaryPath);
                return;
            }

            try
            {
                // One rename on iOS/macOS (POSIX rename is atomic); the old primary becomes the backup.
                File.Replace(TempPath, PrimaryPath, BackupPath, true);
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is NotSupportedException || e is NotImplementedException)
            {
                File.Copy(PrimaryPath, BackupPath, true);
                File.Delete(PrimaryPath);
                File.Move(TempPath, PrimaryPath);
            }
        }

        private static string ReadOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
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
