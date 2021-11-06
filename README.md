# Monitoring A Directory

## Introduction

The subject of this article is a tad less modern than the previous two articles: [WCF over HTTPS](https://github.com/geersch/WcfOverHttps) & [MEF](https://github.com/geersch/ManagedExtensibiliyFramework). Instead of focusing on a modern technology such as WCF or the upcoming Managed Extensibility Framework, I have a craving to write an article about the good ol' [FileSystemWatcher Class](http://msdn.microsoft.com/en-us/library/system.io.filesystemwatcher.aspx).

It's been around since .NET 1.0 but the other day when I was composing the article about [MEF](https://github.com/geersch/ManagedExtensibiliyFramework) it was brought back to my attention. MEF contains a [DirectoryCatalog](http://mef.codeplex.com/Wiki/View.aspx?title=Using%20Catalogs&referringTitle=Home) type, which allows you to specify a directory that contains assemblies that in turn contain extensions (exports) for your application.

In an earlier preview of MEF the DirectoryCatalog type automatically detected new assemblies placed inside of this directory and refreshed the loaded extensions. However this functionality has been dropped in favor of a Refresh() method which you have to call yourself. In other words you are now responsible for detecting new additions to said directory.

The main focus of this article is on how to use the FileSystemWatcher class type effectively for monitoring a directory for new, incoming files. Enough introductionary chit-chat, let's get to the meat of it...

## Hello, World!

I usually build demo applications in order to get my point accross and this article is no different. Let us not ponder about MEF's DirectoryCatalog type but instead focus on a more "Hello, World!"-ish example.

Imagine an application which needs to monitor a directory for newly added text files. These text files needs to be processed and deleted afterwards .

In the real-world you would probably use a Windows service for this type of application as no real GUI is needed, but since this is a demo and some kind of output is preferable the .NET's universe black gold comes in handy. You guessed it, let's create another fancy console application.

For starters fire up Visual Studio and create a new blank solution called "FileSystemWatcher". Next add a new console application project named "ConsoleMonitor". We're now ready to lay the groundwork...

[Top of page](#top)

## The Basics

In order to setup a FileSystemWatcher for the purpose of monitoring a directory you need hook it up properly. The following properties and events relate to our demo:

**Properties**:

- **Path**: Identifies the path of the directory to watch
- **Filter**: Filter used to determine which files are monitored (e.g.: \*.txt, \*.xml...etc.)
- **EnableRaisingEvents**: The most important one of all. By default it is false, but you need to set it to true to enable the FileSystemWatcher instance. If you don't do this then it will not raise any events when files are created, deleted, changed...etc. In a Windows service you would set this property to true upon starting the service and to false when stopping it.

Another useful property is IncludeSubDirectories. By default it is set to false, but by setting it to true the FileSystemWatcher will also monitor the subdirectories found within the specified path.

**Events**:

- **Created**: This event is triggered whenever a file or directory in the specified path is created. By specifying a filter you opt to only receive notifications for the type of files you specified.

Similarly the events Changed, Deleted and Renamed are also available. Their name implies when they are triggered. Since the focus of our demo only deals with newly created files we don't need these. Feel free to explore [these events](http://msdn.microsoft.com/en-us/library/system.io.filesystemwatcher_members.aspx) on your own.

Let's setup the basics for our console application. Your code should resemble that of Listing 1:

**Listing 1** - Monitoring A Directory

```csharp
class Program
{
    private static FileSystemWatcher watcher;

    static void Main()
    {
        // Make the directory being monitored exists
        Assembly assembly = Assembly.GetCallingAssembly();
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
        // Process the file
    }
}
```

The code checks for a directory "Input" in the location of the executing assembly and creates if not available. Then a FileSystemWatcher instance is being setup to monitor this directory for any new text files. If a new text file is created within this directory then the file_Created(...) event handler will be triggered.

## Handling The Created Event

Being an event handler the file_Created(...) method receives two parameters, namely the Sender and an EventArgs descendant. The Sender is the FileSystemWatcher instance itself and for the EventArgs you receive a [FileSystemEventArgs](http://msdn.microsoft.com/en-us/library/system.io.filesystemeventargs.aspx) instance. It contains a few useful properties describing the newly created file.

- **ChangeType**: The type of directory event that occured (e.g.: Changed, Created, Deleted, Renamed). Is an enumeration of type [WatcherChangeTypes](http://msdn.microsoft.com/en-us/library/t6xf43e0.aspx).
- **FullPath**: The fully qualified path of the file.

We only need to concern ourselves with the FullPath property. At first sight you might process the files in the following manner:

**Listing 2** - Processing A File

```csharp
static void file_Created(object sender, FileSystemEventArgs e)
{
    // Process the file
    // ...

    // Delete the file after it has been processed
    File.Delete(e.FullPath);
}
```

This might sometimes work and on other occasions an IOException will be thrown indicating that the file is currently being used by another process.

This is caused by the fact that the FileSystemWatcher triggers the Created event immediately after the first byte of the new file has been written. It does not wait to trigger this event until the file has been completely created or uploaded.

Processing the files directly from within this event handler will eventually result in one or more IOExceptions being thrown because the creation process might still be busy creating the file and thus disallow another process from accessing it.

I have not found a proper solution to this problem and upon searching the net I found out that the following approach is usually implemented:

**Listing 3** - IsFileUploadComplete

```csharp
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
```

The above piece of code tries to open the file exclusively for reading. If it fails an IOException is thrown indicating that the file is still being used by another process. If you have a better approach please suggest it by posting a comment or [e-mailing me](mailto:geersch@gmail.com) directly.

The event handler for the Created event is then implemented as follows:

**Listing 4** - Revised file_Created(...) Event Handler

```csharp
{
    // Check if the file has been completely created / uploaded
    int maximumProcessRetries = 5;
    int delayBeforeRetry = 5000;

    int attempts = 0;

    while (true)
    {
        if (IsFileUploadComplete(e.FullPath))
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
    File.Delete(e.FullPath);
}
```

When a new file is created an attempt to exclusively access the file is made. If it fails the thread waits for a certain amount of time before trying again. This is repeated for a maximum number of X times. Feel free to adjust the code and perhaps make the number of retries or the delay configurable.

Not the nicest solution, but effective for most times the FileSystemWatcher is put to use...

## Large Volumes

Until now processing the files has been handled directly within the event handler attached to the FileSystemWatcher's Created event. This poses no problem if you only need to handle the occasional file, but can be problematic if you need to process large batches of files.

Each FileSystemWatcher allocates a buffer of 8 Kb that contains all the information about the files that cause events to be raised. When a large number of files are created in the directory being monitored and the processing of each file takes up a considerable amount of time then this buffer may fill up. Any events that arrive after the buffer has been filled up will be lost. No exceptions or notifications will be thrown for these missing events.

The most obvious to circumvent this issue (or at least postpone it) is to increase the size of the buffer. This can be done by setting the [InternalBufferSize property](http://msdn.microsoft.com/en-us/library/system.io.filesystemwatcher.internalbuffersize.aspx) of the FileSystemWatcher.

To quote MSDN: "_The file system changes stored in this buffer can use up to 16 bytes of memory, not including the file name. If there are many changes in a short time, the buffer can overflow. This caused the component to lose trackof changes in the directory._".

So simply increasing the buffer size might do, but it also has a ramification, namely that it is quite expensive as the memory is non paged and cannot be swapped out to disk. It is better to use a small buffer and filter out any notifications you do not require.

There is another possible solution to postpone a buffer overflow from occuring. By handling the events you monitor, the Created event in this case, as quickly as possibly you remove the information tied to the event from the buffer so that it can reclaim this space. Credit where credit is due as this is proposed by Rohan Warang, read [his article](http://csharp-codesamples.com/2009/02/file-system-watcher-and-large-file-volumes/) on [csharp-codesamples.com](http://csharp-codesamples.com/).

To implement this we need to asynchronously process the files. As soon as the event is triggered we queue the full path to the file in a Queue which in turn will be processed by a worker thread. The duty of the event handler tied to the Created event is reduced to adding a string (the file path) to a queue.

Let's realize this. Add a new class to the console application project named FileProcessor. Add the following code:

**Listing 5** - FileProcessor Class

```csharp
public class FileProcessor
{
    private readonly Queue<string> files = new Queue<string>();

    public void EnqueueFile(string path)
    {
        files.Enqueue(path);
    }
}
```

The code for the event handler attached to the created event can then be minimized to this:

**Listing 6** - Queuing The File

```csharp
//...
private static readonly FileProcessor fileProcessor = new FileProcessor();
//...
static void file_Created(object sender, FileSystemEventArgs e)
{
    fileProcessor.EnqueueFile(e.FullPath);
}
```

## Asynchronous Processing

How the files are actually asynchronously processed by a FileProcessor instance has little to do with the FileSystemWatcher but let's quickly cover it to get it out of the way.

Start by adding the following fields to the FileProcessor class.

**Listing 7** - FileProcessor Fields

```csharp
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
```

Obviously we need to declare a new queue which contains a list of String types (the paths to the files) and a new separate thread. Since this queue is accessed by multiple threads we need a lock object for synchronization purposes. The other fields waitHandle and shouldStop are used to determine if the second thread should continue with executing its worker method.

With these fields in place the EnqueueFile(...) method can be revised.

**Listing 8** - Enqueuing The File Revised

```csharp
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
```

The file is added to the queue. If it is the first file than the worker thread is created and started, if not then a check is performed to see if the waithandle should be signaled. The worker method is coupled to a worker method by using the ThreadStart delegate. This worker method aptly named Work() contains no parameters or return type and is implemented as follows:

**Listing 9** - The Method That Executes On The Worker Thread

```csharp
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
```

This method dequeues an item of the queue and hands it off to the ProcessFile(...) method for further processing. The ProcessFile(...) method basically contains the code shown in Listing 4. The IsFileUploadComplete(...) method (Listing 3) is also moved to the FileProcessor method as a private helper method. The volatile shouldStop boolean is checked wether or not the FileProcessor should continue to process the queue. This enables you to add a method called StopProcessing() which you can call to stop the processing. Enqueuing another file afterwards would restart it.

This basically sums up the internal workings of the FileProcessor type. Please consult the accompanying source code for a working example.

## Applied To MEF

Let's briefly bring up MEF again. When using a DirectoryCatalog to import all the exports available in the assemblies listed in the directory to which the catalog is tied, it is possible to use the FileSystemWatcher to monitor it. You only need to call the DirectoryCatalog's Refresh() method when new assemblies are added.

**Listing 10** - Refreshing A DirectoryCatalog

```csharp
static void file_Created(object sender, FileSystemEventArgs e)
{
    directoryCatalog.Refresh();
}
```

## Summary

Voila, that wraps up my article about the FileSystemWatcher. Though not the hottest or most modern topic I think it is still usefull today for various situations.

When using it you should be careful to make sure that the Created event is triggered as soon as the first byte of the file is written, not when the file has been completely created / uploaded. The internal buffer of the FileSystemWatcher can also overflow if your processing of the files takes up too much time while incoming notifications are still being generated.

I've tested this approach using 10.000 files and the default buffer size without any problems, which should be adequate enough for most the situations.
