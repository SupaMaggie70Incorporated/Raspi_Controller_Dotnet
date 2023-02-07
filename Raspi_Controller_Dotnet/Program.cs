

using Raspi_Controller_Dotnet;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Raspi_Controller_Dotnet
{
    public static class Program
    {
        public static FileManager FileManager;
        public static TerminalManager TerminalManager;
        public static TaskManager CommandManager;
        public static HttpManager HttpController;
        public static NetworkManager NetworkManager;
        public static UserManager UserManager;


        public static bool IsWindows;
        public static bool IsLinux;
        public static bool IsAdmin;


        /// <summary>
        /// Whether or not to start a local http server
        /// </summary>
        public static bool UseHttp = true;
        /// <summary>
        /// Whether or not to only allow connections from the local network
        /// </summary>
        public static bool FilterPrivateHttp = true;
        /// <summary>
        /// The path of the folder where html is stored
        /// </summary>
        public static string HtmlPath = "C:\\Users\\SupaM\\source\\repos\\Raspi_Controller_Html";
        /// <summary>
        /// The delay between ticks
        /// </summary>
        public static int TickDelay = 1000; // 10 ticks per second

        public static void Main(string[] args)
        {

            Log("Raspi Controller Launched");

            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            GetIsAdmin();

            Log($"Current OS: {RuntimeInformation.OSDescription}");
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--usehttp")
                {
                    UseHttp = bool.Parse(args[++i]);
                }
                else if (args[i] == "--filterhttp")
                {
                    FilterPrivateHttp = bool.Parse(args[++i]);
                }
                else if (args[i] == "-os")
                {
                    IsWindows = args[++i].ToLower().Contains("win");
                    IsLinux = args[i].ToLower().Contains("linux");
                }
                else if (args[i] == "--htmlpath")
                {
                    HtmlPath = args[++i];
                }
                else if (args[i] == "--tickdelay")
                {
                    TickDelay = int.Parse(args[++i]);
                }
            }
            if (!(IsWindows ^ IsLinux))
            {
                Quit("Not windows or linux, which will probably cause issues. If you are 100% sure you want to continue, run with the argument -os [os] where [os] is windows or linux.", false);
            }
            


            FileManager = new FileManager();
            TerminalManager = new TerminalManager();
            CommandManager = new TaskManager();
            HttpController = new HttpManager();
            NetworkManager = new NetworkManager();
            UserManager = new UserManager();

            DateTime previousTickTime = Process.GetCurrentProcess().StartTime;

            while(true)
            {
                // Wait until 100 plus last tick
                // In other words 
                int wait = TickDelay - (DateTime.Now - previousTickTime).Milliseconds;
                if (wait >= 0) Thread.Sleep(wait);
                NetworkManager.Update();
                TerminalManager.Update();
            }
        }

        private static void GetIsAdmin()
        {
            IsAdmin = false;
            Warn("Detecting if running as admin is not currently implemented. Many features may not work properly. Please check that you ran with administrator.");
        }
        public static void Log(string message)
        {
            Console.Write("[INFO] ");
            Console.WriteLine(message);
        }
        public static void Warn(string message)
        {
            Console.Write("[WARNING] ");
            Console.WriteLine(message);
        }
        public static void Error(string message)
        {
            Console.Write("[ERROR] ");
            Console.WriteLine(message);
        }
        public static void Quit(string message, bool closeapps)
        {
            Log(message);
            Error("Program Shutting Down");
            if (closeapps) TerminalManager.CloseTerminals();
            Environment.Exit(0);
        }
    }
}