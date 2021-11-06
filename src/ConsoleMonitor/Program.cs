using System;
using System.IO;
using System.Reflection;

namespace ConsoleMonitor
{
    class Program
    {
        private static FileSystemWatcher watcher;
        private static readonly FileProcessor fileProcessor = new FileProcessor();

        static void Main()
        {
            // Make the directory being monitored exists
            Assembly assembly = Assembly.GetExecutingAssembly();
            string path = Path.Combine(Path.GetDirectoryName(assembly.Location), "input");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            watcher = new FileSystemWatcher {Path = path, Filter = "*.txt"};

            watcher.Created += file_Created;
            watcher.EnableRaisingEvents = true;
            
            Console.WriteLine("Press any key to close this application...");
            Console.ReadLine();
        }

        static void file_Created(object sender, FileSystemEventArgs e)
        {
            fileProcessor.EnqueueFile(e.FullPath);
        }
    }
}
