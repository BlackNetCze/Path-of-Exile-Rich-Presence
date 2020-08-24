using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace Path_of_Exile_Rich_Presence
{
    internal static class Program
    {
        private static DiscordRpc.RichPresence _presence;
        static DiscordRpc.EventHandlers _handlers;

        private static readonly Timer Timer = new Timer(10000);
        private static readonly Timer TimerGetCurrentCharacter = new Timer(60000);
        private static readonly Timer TimerFindCharacterLocation = new Timer(5000);

        private static string _userName;
        private static PlayerCharacter _currentCharacter = new PlayerCharacter();

        private static List<PlayerCharacter> _characters = new List<PlayerCharacter>();
        private static List<Map> _maps = new List<Map>();

        private static FileInfo _poeClientFileInfo;

        private static void Main()
        {
            LoadConfig();

            using (StreamReader file = File.OpenText(Environment.CurrentDirectory + @"\maps.json"))
                _maps = JsonConvert.DeserializeObject<List<Map>>(file.ReadToEnd());

            _presence.details = "Level " + _currentCharacter.level + " " + _currentCharacter.ascendancy;
            _presence.state = "Currently in ";

            _presence.smallImageKey = "mainimage";
            _presence.smallImageText = "mainimageSmall";


            Timer.Elapsed += (sender, evt) => { UpdatePoePresence(); };
            TimerGetCurrentCharacter.Elapsed += (sender, evt) => { GetCurrentCharacter(); };
            TimerFindCharacterLocation.Elapsed += (sender, evt) => { FindCharacterLocation(); };

            Initialize("539417063912112139");

            GetCurrentCharacter();
            _presence.largeImageKey = _currentCharacter.ascendancy.ToLower();
            _presence.largeImageText = _currentCharacter.name;

            TimerGetCurrentCharacter.Start();
            TimerFindCharacterLocation.Start();

            try
            {

                var watcher = new FileSystemWatcher
                {
                    Path = Path.GetDirectoryName(_poeClientFileInfo.FullName) + "\\",
                    Filter = Path.GetFileName(_poeClientFileInfo.FullName),
                    EnableRaisingEvents = true
                };

                watcher.Changed += Watcher_Changed;

                UpdatePoePresence();
            }
            catch (Exception)
            {
                const string text = @"Path to Path of Exile is invalid. Example of path: C:\\SteamLibrary\\steamapps\\common\\Path of Exile";
                Log(text, ConsoleColor.Red);
                throw new Exception(text);
            }

            while (true)
            {
                Console.ReadKey();
                UpdatePoePresence();
            }
        }

        private static void LoadConfig()
        {
            if (File.Exists(Environment.CurrentDirectory + @"\configuration.json"))
            {
                using (StreamReader file = File.OpenText(Environment.CurrentDirectory + @"\configuration.json"))
                {
                    try
                    {
                        var configuration = JsonConvert.DeserializeObject<Configuration>(file.ReadToEnd());

                        _poeClientFileInfo = new FileInfo(configuration.path + @"\logs\Client.txt");
                        _userName = configuration.username;
                    }
                    catch (Exception)
                    {
                        var text = @"Path to Path of Exile is invalid. Please edit configuration.json. Example of path: C:\\SteamLibrary\\steamapps\\common\\Path of Exile";
                        Log(text, ConsoleColor.Red);
                        throw new Exception(text);
                    }
                }
            }
            else
            {
                File.Create(Environment.CurrentDirectory + @"\configuration.json").Close();
                File.WriteAllText(Environment.CurrentDirectory + @"\configuration.json", JsonConvert.SerializeObject(new Configuration()));
            }
        }

        private static void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            FindCharacterLocation();
        }

        private static void FindCharacterLocation()
        {
            try
            {
                var lastLine = ReadLines(_poeClientFileInfo.FullName).Last();

                if (lastLine.Contains("You have entered"))
                {
                    var result = Regex.Match(lastLine, @"(?<=You have entered ).*(?=.)");

                    var location = result.ToString();

                    // string newResult = location + "(" + maps.Find(x => x.name == location.ToString()).tier;
                    try
                    {
                        var currentMap = _maps.Find(x => x.name == location + " Map");
                        location = $"{location} ({ currentMap.tier} Map)";
                    }
                    catch
                    {
                        // ignored
                    }

                    if (UpdatePoePresenceText("In " + location))
                    {
                        Log($"LOG: Location has been changed. Text: {lastLine} | " + "Location: " + result, ConsoleColor.Green);
                        Log("Location: " + result, ConsoleColor.Green);
                    }
                }
            }
            catch
            {
                Log("EXCEPTION", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Updates presence with new text.
        /// </summary>
        /// <param name="newText">New text (string).</param>
        /// <returns>Returns if presence have been changed.</returns>
        private static bool UpdatePoePresenceText(string newText)
        {
            if (_presence.state != newText)
            {
                _presence.state = newText;
                UpdatePoePresence();
                return true;
            }
            return false;
        }

        private static IEnumerable<string> ReadLines(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0x1000, FileOptions.SequentialScan))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private static void GetCurrentCharacter()
        {
            using (var client = new HttpClient())
            {
                // HTTP POST
                client.BaseAddress = new Uri("https://www.pathofexile.com");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = client.GetAsync($"character-window/get-characters?accountName={_userName}").Result;
                string res;
                using (HttpContent content = response.Content)
                {
                    // ... Read the string.
                    Task<string> result = content.ReadAsStringAsync();
                    res = result.Result;
                }
                try
                {
                    _characters = JsonConvert.DeserializeObject<List<PlayerCharacter>>(res);

                    var lastPlayerCharacter = _characters.Find(x => x.lastActive == true);
                    _currentCharacter = lastPlayerCharacter;

                    _presence.details = "Level " + _currentCharacter.level + " " + _currentCharacter.ascendancy;
                    _presence.largeImageKey = _currentCharacter.ascendancy.ToLower();
                    _presence.largeImageText = _currentCharacter.name;
                    _presence.smallImageKey = "mainimage";
                    _presence.smallImageText = "mainimageSmall";

                    UpdatePoePresence();

                    Log($"Character updated from API: Level: {lastPlayerCharacter.level}, Name: {lastPlayerCharacter.name}, Class: {lastPlayerCharacter.ascendancy}", ConsoleColor.Blue);
                }
                catch (Exception)
                {
                    const string text = @"Wrong username or account is private. Please edit configuration.json";
                    Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine(text);
                    throw new Exception(text);
                }
            }
        }

        private static void UpdatePoePresence()
        {
            DiscordRpc.UpdatePresence(ref _presence);

            Console.WriteLine("Presence updated.");
        }

        private static void Initialize(string clientId)
        {
            _handlers = new DiscordRpc.EventHandlers { readyCallback = ReadyCallback };

            _handlers.disconnectedCallback += DisconnectedCallback;
            _handlers.errorCallback += ErrorCallback;

            DiscordRpc.Initialize(clientId, ref _handlers, true, null);

            Console.WriteLine("Initialized.");
        }

        private static void Shutdown()
        {
            DiscordRpc.Shutdown();
            Console.WriteLine("Shuted down.");
        }
        public static void Log(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }
        private static void ReadyCallback() => Console.WriteLine("Ready.");
        private static void DisconnectedCallback(int errorCode, string message) => Console.WriteLine($"Disconnect {errorCode}: {message}");
        private static void ErrorCallback(int errorCode, string message) => Console.WriteLine($"Error {errorCode}: {message}");
    }
}
