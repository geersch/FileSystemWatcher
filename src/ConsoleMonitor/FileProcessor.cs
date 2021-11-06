using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ConsoleMonitor
{
    public class FileProcessor
    {
        #region Fields

        private readonly Queue<string> files = new Queue<string>();
        private Thread thread;
        private readonly EventWaitHandle waitHandle = new AutoResetEvent(true);
        private static readonly object lockObject = new object();

        // Volatile is used as hint to the compiler that this data
        // member will be accessed by multiple threads.
        // http://msdn.microsoft.com/en-us/library/7a2f3ay4(VS.80).aspx
        private volatile bool shouldStop = false;

        #endregion
        
        #region Helper methods

        private static bool IsFileUploadComplete(string path)
        {
            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void ProcessFile(string path)
        {
            // Check if the file has been completely created / uploaded
            int maximumProcessRetries = 5;
            int delayBeforeRetry = 5000;

            int attempts = 0;

            while (true)
            {
                if (IsFileUploadComplete(path))
                {
                    // Process the file
                    // ...

                    break;
                }
                attempts += 1;
                if (attempts >= maximumProcessRetries)
                {
                    // Log the error and send out notifications
                    /// ...etc.
                    break;
                }
                Thread.Sleep(delayBeforeRetry);
            }

            // Delete the file after it has been processed
            File.Delete(path);
        }

        private void Work()
        {
            while (!shouldStop)
            {
                string path = String.Empty;
                lock (lockObject)
                {
                    if (files.Count > 0)
                    {
                        path = files.Dequeue();
                    }
                }

                if (!String.IsNullOrEmpty(path))
                {
                    // Process the file
                    ProcessFile(path);
                }
                else
                {
                    // If no files are left to process then wait
                    waitHandle.WaitOne();
                }
            }
        }

        #endregion

        #region Methods

        public void EnqueueFile(string path)
        {
            // Queue the file
            lock (lockObject)
            {
                files.Enqueue(path);
            }

            // Initialize and start the worker thread when the first file is queued
            // or when it has been stopped and thus terminated.
            if (thread == null || shouldStop)
            {
                thread = new Thread(new ThreadStart(Work));
                thread.Start();
            }
            // If the thread is waiting then start it
            else if (thread.ThreadState == ThreadState.WaitSleepJoin)
            {
                waitHandle.Set();
            }
        }

        public void StopProcessing()
        {
            shouldStop = true;
        }

        #endregion
    }
}
