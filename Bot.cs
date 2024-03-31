using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace TeeTimeBot;

public class Bot
{

    private static readonly List<Bot> allBots = new();
    
    private readonly IWebDriver driver;
    private readonly string prefix;
    private readonly string url;
    private int playerAmount;
    private bool connected;
    private readonly string login_email;
    private readonly string login_password;
    private string? finalTime;
    private bool completed;
    private readonly int threadID;

    public const string persistentPlayerDataPath = "botdata.txt";
    public const string persistentLoginDataPath = "botlogin.txt";
    private const string persistentCrashPath = "_crashlog.txt";
    private const DayOfWeek targetDay = DayOfWeek.Wednesday;
    public const int targetHour = 19;
    public const int targetMinute = 00;
    public const int targetSecond = 00;
    
    private Bot(string url, int thread)
    {
        threadID = thread;
        prefix = "[Thread: " + threadID + "] ";
        var login = GetLoginCredentials();
        login_email = login[0];
        login_password = login[1];
        playerAmount = GetPlayerAmount();
        Console.WriteLine(prefix + "Player amount saved and set to: " + playerAmount);
        driver = new ChromeDriver();
        this.url = url;
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        Console.WriteLine(prefix + "Driver established.");
        allBots.Add(this);
    }

    public IWebDriver GetDriver()
    {
        return driver;
    }

    private void EstablishUplink()
    {
        try
        {
            driver.Navigate().GoToUrl(url);
            Console.WriteLine(prefix + "Bot uplink connected to url: " + url);
            connected = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(prefix + "Error connecting to host url: " + ex);
            connected = false;
        }
    }

    private void SetTimePreferences()
    {
        Console.WriteLine(prefix + "Attempting to get tee time...");
        var buttons =
            driver.FindElements(By.ClassName("btn-primary"));
        if (buttons == null || buttons.Count < 1)
        {
            Console.WriteLine(prefix + "Error finding primary buttons using cascading style sheets!");
            return;
        }
        var nonres = buttons.FirstOrDefault(btn => btn.Text.Equals("Resident"));
        if (nonres == null)
        {
            Console.WriteLine(prefix + "Error finding button name: \"Resident\"");
            return;
        }
        nonres.Click();
        try
        {
            var loginButton = driver.FindElement(By.ClassName("btn-lg"));
            if (loginButton == null)
            {
                Console.WriteLine(prefix + "No login button found!");
                throw new Exception();
            }
            if (loginButton.Text.ToLower().Contains("log")) loginButton.Click();
            else
            {
                Console.WriteLine(prefix + "Error finding login button!");
                throw new Exception();
            }
            var email = driver.FindElement(By.Name("email"));
            var password = driver.FindElement(By.Name("password"));
            if (email == null || password == null)
            {
                Console.WriteLine(prefix + "Error parsing email and password input forms!");
                throw new Exception();
            }
            email.SendKeys(login_email);
            password.SendKeys(login_password);
            var finalLogin = driver.FindElements(By.ClassName("col-xs-12"))
                .FirstOrDefault(obj => obj.Text == "Log In");
            if (finalLogin == null)
            {
                Console.WriteLine(prefix + "Could not submit login form!");
                throw new Exception();
            }
            if (finalLogin.Text.ToLower().Contains("log")) finalLogin.Click();
            else
            {
                Console.WriteLine(prefix + "Could not find login button!");
                throw new Exception();
            }
        }
        catch (Exception)
        {
            Console.WriteLine(
                prefix + "Login panel not completed... proceeding under logged in assumption, terminate if wrong!");
        }
        Console.WriteLine(prefix + "Login phase completed.");
        IWebElement schedule;
        try
        {
            schedule = driver.FindElement(By.Name("schedules"));
        }
        catch (Exception ex)
        {
            Console.WriteLine(prefix + "Could not find course selection! \nException code: " + ex);
            return;
        }
        var courseSelect = new SelectElement(schedule);
        courseSelect.SelectByText("Bethpage Yellow Course");
        Console.WriteLine(prefix + "Setting changed to Yellow Course.");
        var primaryButtons = driver.FindElements(By.ClassName("btn-primary"));
        if (primaryButtons == null || primaryButtons.Count < 1)
        {
            Console.WriteLine(prefix + "Could not find primary buttons on target site.");
            return;
        }
        var dateSelector = driver.FindElement(By.Name("date"));
        if (dateSelector == null)
        {
            Console.WriteLine(prefix + "Error locating date selector!");
            return;
        }
        string dateSend = "";
        var value = dateSelector.GetAttribute("value");
        Console.WriteLine(prefix + "Targeted deletion method, character count: " + value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            dateSend += Keys.Backspace;
        }
        dateSend += GetTargetDay(targetDay);
        dateSelector.SendKeys(dateSend);
        primaryButtons.Where(btn => btn.Text == "" + playerAmount)
            .ToList()
            .ForEach(btn => btn.Click());
        var timeButton = primaryButtons.FirstOrDefault(btn => btn.Text == "All");
        if (timeButton == null)
        {
            Console.WriteLine(prefix + "Error Locating time selection buttons!");
            return;
        }
        timeButton.Click();
        Console.WriteLine(prefix + "Time selection set, continuing to hole selection...");
        var holesButton = primaryButtons.FirstOrDefault(btn => btn.Text == "18");
        if (holesButton == null)
        {
            Console.WriteLine(prefix + "Error locating hole selection buttons!");
            return;
        }
        holesButton.Click();
        Console.WriteLine(prefix + "Hole selection set to 18, continuing...");
    }

    private void RequestTeeTime()
    {
        var all_tiles = driver.FindElements(By.ClassName("time-tile"));
        var tiles = new List<IWebElement>();
        for (int i = 0; i < all_tiles.Count; i++)
        {
            var tile = all_tiles[i];
            var outerSpan = tile.FindElement(By.ClassName("js-booking-slot-players"));
            var span = outerSpan.FindElement(By.TagName("span"));
            if (span.Equals(outerSpan))
            {
                Console.WriteLine(prefix + "No span wrapper found, removing broken availability option.");
                continue;
            }
            if (int.Parse(span.Text) > playerAmount)
            {
                Console.WriteLine(prefix + "Removing invalid slot option, player availability was: " + span.Text + ", " +
                                  "needed amount: " + playerAmount);
                continue;
            }
            tiles.Add(tile);
            Console.WriteLine(prefix + "Added valid time slot with acceptable player registration slots of: " + span.Text);
        }
        if (tiles.Count < 1)
        {
            Console.WriteLine(prefix + "No times found with the current given date and player count on the Yellow Course!");
            return;
        }
        Console.WriteLine(prefix + "Available Time Slots:");
        int options = 0;
        var dictionary = new Dictionary<string, IWebElement>();
        foreach (var tile in tiles)
        {
            try
            {
                var time = tile.FindElement(By.ClassName("booking-start-time-label"));
                var timeText = time.Text;
                dictionary.Add(timeText, tile);
                Console.WriteLine(++options + ". " + timeText);
            }
            catch (Exception)
            {
                Console.WriteLine(prefix + "Error processing time tile slots! aborting program...");
                return;
            }
        }
        Console.WriteLine(prefix + "Ordering time slots to desired preferences...");
        var slots = OrderTimeSlots(dictionary);
        Console.WriteLine(prefix + "Ordered Web Elements:");
        var webElements = slots.ToList();
        webElements
            .ToList()
            .ForEach(element =>
                Console.WriteLine(element.FindElement(By.ClassName("booking-start-time-label")).Text));
        webElements.First().Click();
        finalTime = webElements.First().FindElement(By.ClassName("booking-start-time-label")).Text;
    }

    private void SetPlayersAndBook()
    {
        var firstSet = driver
            .FindElement(By.ClassName("js-booking-players-row"));
        var playerButton = firstSet
            .FindElements(By.ClassName("btn-primary"))
            .FirstOrDefault(btn => btn.Text == playerAmount switch
            {
                1 => "1",
                2 => "2",
                3 => "3",
                4 => "4",
                _ => "1"
            });
        if (playerButton == null)
        {
            Console.WriteLine(prefix + "Error locating player amount buttons!");
            return;
        }
        playerButton.Click();
        Console.WriteLine(prefix + "Player button set to value of button: " + playerButton.Text);
        Console.WriteLine();
        Console.WriteLine(prefix + "Finishing booking...");
        var finalButton = driver.FindElement(By.ClassName("btn-success"));
        if (finalButton == null)
        {
            Console.WriteLine(prefix + "Error finding and clicking final button!");
            return;
        }
        finalButton.Click();
        Console.WriteLine(prefix + "Clicking book button...");
        Console.WriteLine(prefix + "Complete! Check email: " + login_email + ", for tee time confirmation!");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n====================================");
        Console.WriteLine("Final Booking Info:");
        Console.WriteLine("Time: " + finalTime);
        Console.WriteLine("Found on thread: " + threadID);
        Console.WriteLine("====================================\n");
        Console.ForegroundColor = ConsoleColor.White;
        completed = true;
    }

    private IEnumerable<IWebElement> OrderTimeSlots(Dictionary<string, IWebElement> dict)
    {
        var list = new List<int>();
        var output = new Dictionary<int, IWebElement>();
        foreach (var key in dict.Keys)
        {
            //TRUE FOR a.m. FALSE FOR p.m.
            bool amOrPM = key.Contains("am");
            string nums = "";
            key.ToCharArray()
                .Where(str => int.TryParse("" + str, out _))
                .ToList()
                .ForEach(num => nums += num);
            var newTime = int.Parse(nums);
            if (newTime is > 1199 and < 1300) newTime -= 1200;
            if (!amOrPM) newTime += 1000;
            list.Add(newTime);
            output.Add(newTime, dict[key]);
            Console.WriteLine(prefix + "Adjusting time: " + key + ", to script readable time: " + newTime);
        }
        list.Sort((x,y) => x > y ? x : y);
        return list.Select(t => output[t]).ToList();
    }
    
    private bool IsConnected()
    {
        return connected;
    }

    private static void SetPlayerAmount(int players)
    {
        string insert = "" + players;
        File.WriteAllText(persistentPlayerDataPath, insert);
    }

    private int GetPlayerAmount()
    {
        int output;
        if (!File.Exists(persistentPlayerDataPath)) InvokePlayerAmountSetup();
        try
        {
            var contents = File.ReadAllText(persistentPlayerDataPath);
            output = int.Parse(contents);
        }
        catch (Exception ex)
        {
            Console.WriteLine(prefix + "Error in player value file storing! \nError Code: " + ex);
            output = -1;
            SetPlayerAmount(-1);
        }
        return output;
    }

    private void InvokePlayerAmountSetup()
    {
        if (File.Exists(persistentPlayerDataPath))
        {
            playerAmount = GetPlayerAmount();
            if (playerAmount is > 0 and < 5) return;
        }
        do
        {
            Console.Write("How many players? (Type a single digit number 1 through 4): ");
            var inp = Console.ReadLine();
            try
            {
                if (string.IsNullOrEmpty(inp)) continue;
                playerAmount = int.Parse(inp);
                SetPlayerAmount(playerAmount);
                break;
            }
            catch (Exception)
            {
                Console.WriteLine("Invalid entry, please enter only a single digit number from 1 to 4!");
            }
        } while (true);
    }

    private static void InvokeLoginSetup()
    {
        string email_output;
        string password_output;
        if (File.Exists(persistentLoginDataPath)) return;
        do
        {
            Console.WriteLine("I will need to store your login information to access the resident section of the tee" +
                              "time website.");
            Console.Write("Email: ");
            var email = Console.ReadLine();
            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Please type in an email address!");
                continue;
            }
            Console.WriteLine("You entered your email as: " + email);
            Console.Write("If this is correct, type 'Y', if not type 'N': ");
            var emailCorrect = Console.ReadLine()?.ToUpper().ToCharArray()[0];
            if (emailCorrect == null)
            {
                Console.WriteLine("By not typing 'Y' you have indicated that you wish to change your email!");
                continue;
            }

            if (emailCorrect == 'Y')
            {
                email_output = email;
                break;
            }
            Console.WriteLine("Good job, now I need your account password " +
                              "(it will be stored locally so it won't get stolen!)");
        } while (true);

        do
        {
            Console.Write("Password: ");
            var password = Console.ReadLine();
            if (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Please type in a password!");
                continue;
            }
            Console.WriteLine("You entered your password as: " + password);
            Console.Write("If this is correct, type 'Y', if not type 'N': ");
            var passwordCorrect = Console.ReadLine()?.ToUpper().ToCharArray()[0];
            if (passwordCorrect == null)
            {
                Console.WriteLine("By not typing 'Y' you have indicated that you wish to change your password!");
                continue;
            }

            if (passwordCorrect != 'Y') continue;
            password_output = password;
            break;
        } while (true);
        Console.WriteLine("Email and Password saved to local text file! Saved as:");
        var output = email_output + "\n" + password_output;
        Console.WriteLine(output);
        File.WriteAllText(persistentLoginDataPath, output);
    }

    private static string[] GetLoginCredentials()
    {
        if (!File.Exists(persistentLoginDataPath)) InvokeLoginSetup();
        var lines = File.ReadLines(persistentLoginDataPath).ToList();
        var output = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            output[i] = lines[i];
        }
        return output;
    }

    private static string GetTargetDay(DayOfWeek day)
    {
        var current = DateTime.Now;
        var array = new int[7];
        int i = current.DayOfWeek switch
        {
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0
        };
        for (int j = 0; j < 7; j++)
        {
            if (i > 6) i = 0;
            array[i] = 7 - j;
            i++;
        }
        var date = 7 - array[day switch
        {
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0
        }];
        var nextDate = current.AddDays(date > 0 ? date : 7);
        return nextDate.ToShortDateString();
    }

    private static string SaveCrashLog(string? exception)
    {
        var date = DateTime.Now;
        var crashName = date.Day + "-" + date.Month + "-" + date.Year + persistentCrashPath;
        if (string.IsNullOrEmpty(exception)) exception = "Unhandled, missing stack trace!";
        File.WriteAllText(crashName, exception);
        return crashName;
    }

    public static bool RunBot(int threadID)
    {
        var bot = new Bot("https://foreupsoftware.com/index.php/booking/19765/2431#teetimes", threadID);
        try
        {
            bot.EstablishUplink();
            if (!bot.IsConnected()) return false;
            bot.SetTimePreferences();
            bot.RequestTeeTime();
            bot.SetPlayersAndBook();
            Console.WriteLine("Exited at end of program. closing thread: " + threadID);
            return bot.IsComplete();
        }
        catch (ThreadInterruptedException)
        {
            Console.WriteLine("Thread ended by executor! Thread ID: " + threadID);
            return false;
        }
        catch (Exception ex)
        {
            var log = SaveCrashLog(ex.StackTrace);
            Console.WriteLine("Exited due to unexpected exception, check log at: " + log);
            return false;
        }
        finally
        {
            if (!bot.IsComplete()) bot.GetDriver().Quit();
        }
    }

    private bool IsComplete()
    {
        return completed;
    }

    public static List<Bot> GetAllBots()
    {
        List<Bot> output = new();
        foreach (var bot in allBots)
        {
            if (bot == null) continue;
            output.Add(bot);
        }
        return output;
    }
}