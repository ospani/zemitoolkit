using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Zemi.IO
{
    public static class ZemiIO
    {
        class LogMessage
        {
            public LogMessage(string filepath, string text)
            {
                Filepath = filepath;
                Text = text;
            }

            public string Filepath { get; set; }
            public string Text { get; set; }
        }
        static readonly BlockingCollection<LogMessage> _logMessages = new BlockingCollection<LogMessage>();

        private static bool isConcurrentWritingEnabled = false;
        public static void EnableConcurrentWriting()
        {
            ThreadPool.QueueUserWorkItem((Action) =>
            {
                foreach (var msg in _logMessages.GetConsumingEnumerable())
                { 
                    try { File.AppendAllText(msg.Filepath, msg.Text+"\r\n"); }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
            }, null);
            isConcurrentWritingEnabled = true;
        }
        public static void TryAppendFile(string text, string fileName)
        {
            if(isConcurrentWritingEnabled)
            {
                _logMessages.Add(new LogMessage(fileName, text));
            }
            else
            {
                try { File.AppendAllLines(fileName, new string[] { text+"\r\n" }); }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
        }

        public static void CreateDirectoryIfNotExists(string path, string directoryName)
        {
            if (Directory.Exists(path))
            {
                string expectedDirectoryPath = Path.Combine(path, directoryName);
                CreateDirectoryIfNotExists(expectedDirectoryPath);
            }
        }

        public static void CreateDirectoryIfNotExists(string fullPath)
        {
                if (!Directory.Exists(fullPath))
                {
                    try { Directory.CreateDirectory(fullPath); }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
        }

        public static void CreateFileIfNotexists(string path, string fileName)
        {
            if (Directory.Exists(path))
            {
                string expectedFilePath = Path.Combine(path, fileName);
                if (!File.Exists(expectedFilePath))
                {
                    try { File.Create(expectedFilePath); }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
            }
        }
    }
}
