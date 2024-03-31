namespace TeeTimeBot;

public class Executor
{

    private static DateTime startTime, endTime;
    
    public static void Main()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n====================================");
        Console.WriteLine("TEE TIME RETRIEVAL BOT");
        Console.WriteLine("Created by Nick Doxa for Richard Palka!");
        Console.WriteLine("====================================\n");
        Console.ForegroundColor = ConsoleColor.White;
        WaitForDesignated();
        startTime = DateTime.Now;
        var thread = new Thread(ThreadLoopStateCheck);
        thread.Start();
        while (!completed)
        {
            Thread.Sleep(10);
        }
        endTime = DateTime.Now;
        Console.WriteLine();
        Console.WriteLine("TOTAL TIME RUNNING: " + endTime.Subtract(startTime).TotalSeconds + " seconds");
        Console.ReadLine();
        Bot.GetAllBots().ForEach(bot => bot.GetDriver().Quit());
    }

    private const string prefix = "[MAIN THREAD] ";
    private static bool completed;
    private const int sleepDelay = 2500;
    private const int maxThreadsAtOnce = 5;
    private static int threadCount;
    private static readonly Dictionary<int, Thread?> threads = new();

    private static void WaitForDesignated()
    {
        Console.WriteLine(prefix + "Waiting for designated run time slot...");
        while (true)
        {
            var current = DateTime.Now;
            if (current.Hour == Bot.targetHour 
                && current.Minute == Bot.targetMinute 
                && current.Second == Bot.targetSecond) break;
            Thread.Sleep(10);
        }
    }
    
    private static void ThreadLoopStateCheck()
    {
        int currentID = -1;
        do
        {
            if (GetAllThreads().Count >= maxThreadsAtOnce) continue;
            Console.WriteLine(prefix + "Launching next thread...");
            currentID = LaunchBotThread();
            while (!CheckForNecessaryFiles())
            {
                Thread.Sleep(100);
            }
            Thread.Sleep(sleepDelay);
        } while (!completed);
        if (currentID != -1)
        {
            GetThreadById(currentID)?.Interrupt();
            threads.Remove(currentID);
        }
        var remainingThreads = GetAllThreads();
        remainingThreads.ForEach(EndThread);
    }
    
    private static int LaunchBotThread()
    {
        var thread = CreateExecutableThread(RunBot);
        if (thread == null) return -1;
        thread.Start();
        return thread.ManagedThreadId;
    }

    private static Thread? GetThreadById(int id)
    {
        return !threads.ContainsKey(id) ? null : threads[id];
    }

    private static List<Thread> GetAllThreads()
    {
        var output = new List<Thread>();
        foreach (var thread in threads.Values)
        {
            if (thread == null) continue;
            output.Add(thread);
        }
        return output;
    }

    private static void EndThread(Thread thread)
    {
        thread.Interrupt();
    }

    private static void RunBot()
    {
        completed = Bot.RunBot(threadCount);
    }

    private static Thread? CreateExecutableThread(ThreadStart method)
    {
        var thread = new Thread(method);
        var id = thread.ManagedThreadId;
        threads.Add(id, thread);
        ++threadCount;
        return thread;
    }

    private static bool CheckForNecessaryFiles()
    {
        if (!File.Exists(Bot.persistentPlayerDataPath)) return false;
        if (!File.Exists(Bot.persistentLoginDataPath)) return false;
        var playerCheck = int.Parse(File.ReadAllText(Bot.persistentPlayerDataPath));
        if (playerCheck is < 0 or > 5) return false;
        var loginCheck = File.ReadAllText(Bot.persistentLoginDataPath);
        if (string.IsNullOrEmpty(loginCheck)) return false;
        return true;
    }
    
}