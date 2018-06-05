using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using VkNet;
using VkNet.Enums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace MonteceVkBot
{
    class Program
    {
        static VkApi vkapi = new VkApi();
        static long userID = 0;
        static ulong? Ts;
        static ulong? Pts;
        static bool IsActive;
        static Timer WatchTimer = null;
        static byte MaxSleepSteps = 3;
        static int StepSleepTime = 333;
        static byte CurrentSleepSteps = 1;
        delegate void MessagesRecievedDelegate(VkApi owner, ReadOnlyCollection<Message> messages);
        static event MessagesRecievedDelegate NewMessages;
        static Random _Random = new Random();
        static string CommandsPath = "";

        static void Main(string[] args)
        {
            string KEY = "13b47b7e3145e07700571bf78b25c33525b09fde14ee170842f964a1f2b04698fd7f5752344d00affc1bf";
            ConsoleStyle();
            /*
            Console.Write("Введите token авторизации: ");
            KEY = ulong.Parse(Console.ReadLine());                
            */
            Console.WriteLine("Попытка авторизации...");

            if (Auth(KEY))
            {
                CommandsPath = Environment.CurrentDirectory + @"\Commands";
                if (!Directory.Exists(CommandsPath) || !File.Exists(CommandsPath + @"\Commands.txt"))
                {
                    Directory.CreateDirectory(CommandsPath);
                    File.Create(CommandsPath + @"\Commands.txt");
                    Restart();
                }
                ColorMessage("Директория сообщений создана успешно загружена.", ConsoleColor.Green);
                ColorMessage("Авторизация успешно завершена.", ConsoleColor.Green);
                Console.WriteLine("Запросов в секунду доступно: " + vkapi.RequestsPerSecond);
                Eye();
                ColorMessage("Слежение за сообщениями активировано.", ConsoleColor.Green);
            }
            else
            {
                ColorMessage("Не удалось произвести авторизацию!", ConsoleColor.Red);
            }

            Console.WriteLine("Нажмите ENTER чтобы выйти...");
            Console.ReadLine();
        }

        static Func<string> DoubleCode = () =>
        {
            Console.Write("Введите двухэтапный аутификато (если нет, игнорируется): ");
            return Console.ReadLine();
        };

       

        public static string News(string url)
        {
            WebClient webClient = new WebClient();
            string result = webClient.DownloadString(url);
            XDocument document = XDocument.Parse(result);

            List<RssNews> a = (from descendant in document.Descendants("item")
                               select new RssNews()
                               {
                                   Description = descendant.Element("description").Value,
                                   Title = descendant.Element("title").Value,
                                   PublicationDate = descendant.Element("pubDate").Value
                               }).ToList();
            string News = "";
            if (a != null)
            {
                int i = _Random.Next(0, a.Count - 1);
                News = a[i].Title + Environment.NewLine + "------------------" + Environment.NewLine + a[i].Description;
                byte[] bytes = Encoding.Default.GetBytes(News);
                News = Encoding.UTF8.GetString(bytes);
                return News;
            }
            else return "";
        }

        static string Rand(int Min, int Max)
        {
            return _Random.Next(Min, Max).ToString();
        }

        static void ConsoleStyle()
        {
            Console.Title = "Car-Searcher Bot";
            ColorMessage("Car-Searcher Bot", ConsoleColor.DarkYellow);
        }

        static void ColorMessage(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        static bool Auth(string GroupID)
        {
            try
            {
                vkapi.Authorize(new ApiAuthParams { AccessToken = GroupID });
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        static string[] Commands = { "помощь",  "случайное число <Мин> <Макс>", "новости", "учи~<Сообщение>~<Ответ>" };

        static void Command(string Message)
        {
            Message = Message.ToLower();
            string a = CheckCommand(Message);
            if (a != "") SendMessage(a);
            else
            {
                if (Message == "помощь")
                {
                    string msg = "";
                    for (int j = 0; j < Commands.Length; j++) msg += Commands[j] + ", ";
                    SendMessage(msg);
                }
                else if (Message == "привет" | Message == "приветик" | Message == "хай")
                {
                    SendMessage("Приветик!!!)))");
                }
                
                else if (Message.Contains("случайное число "))
                {
                    string Numbers = Message.Substring(Message.IndexOf("число") + 6);
                    int Min = int.Parse(Numbers.Substring(0, Numbers.IndexOf(' ')));
                    int Max = int.Parse(Numbers.Substring(Numbers.IndexOf(' '), Numbers.Length - Numbers.IndexOf(' ')));
                    SendMessage(Rand(Min, Max));
                }
                else if (Message == "неа" || Message == "нет" || Message == "нит")
                {
                    SendMessage("Отрицание - показатель неуверенности");
                }
                else if (Message == "новости")
                {
                    SendMessage(News("https://lenta.ru/rss/top7"));
                }
                else if (Message.Contains("учи~"))
                {
                    try
                    {
                        SendMessage(Learn(Message.Substring(4, Message.Length - 4)));
                    }
                    catch
                    {

                    }
                }
                else
                {
                    SendMessage("Неизвестная команда. Напишите 'Помощь' для получения списка команд.");
                }
                /*else if (Message == Commands[2])
                {
                    SendMessage("MESSAGETEXT");
                }*/
            }
        }

        static string CheckCommand(string Command)
        {
            foreach (string Line in File.ReadAllLines(CommandsPath + @"\Commands.txt"))
            {
                if (Line.Substring(0, Line.IndexOf('~')) == Command)
                {
                    return Line.Substring(Line.IndexOf('~') + 1);
                }
            }
            return "";
        }

        static string Learn(string MSG)
        {
            try
            {
                File.AppendAllText(CommandsPath + @"\Commands.txt", MSG + Environment.NewLine);
                return "Команда добавлена)";
            }
            catch
            {
                return "Команда не была добавлена(";
            }
        }

        static void SendMessage(string Body)
        {
            try
            {
                vkapi.Messages.Send(new MessagesSendParams
                {
                    UserId = userID,
                    Message = Body
                });
            }
            catch (Exception e)
            {
                ColorMessage("Ошибка! " + e.Message, ConsoleColor.Red);
            }

        }

        static void Eye()
        {
            LongPollServerResponse Pool = vkapi.Messages.GetLongPollServer(true);
            StartAsync(Pool.Ts, Pool.Pts);
            NewMessages += Watcher_NewMessages;
        }

        static void Watcher_NewMessages(VkApi owner, ReadOnlyCollection<Message> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Type != MessageType.Sended)
                {
                    User Sender = vkapi.Users.Get(messages[i].UserId.Value);
                    Console.WriteLine("Новое сообщение: {0} {1}: {2}", Sender.FirstName, Sender.LastName, messages[i].Body);
                    userID = messages[i].UserId.Value;
                    Console.Beep();
                    Command(messages[i].Body);
                }
            }
        }

        static LongPollServerResponse GetLongPoolServer(ulong? lastPts = null)
        {
            LongPollServerResponse response = vkapi.Messages.GetLongPollServer(false, lastPts == null);
            Ts = response.Ts;
            Pts = Pts == null ? response.Pts : lastPts;
            return response;
        }

        static Task<LongPollServerResponse> GetLongPoolServerAsync(ulong? lastPts = null)
        {
            return Task.Run(() =>
            {
                return GetLongPoolServer(lastPts);
            });
        }

        static LongPollHistoryResponse GetLongPoolHistory()
        {
            if (!Ts.HasValue) GetLongPoolServer(null);
            MessagesGetLongPollHistoryParams rp = new MessagesGetLongPollHistoryParams();
            rp.Ts = Ts.Value;
            rp.Pts = Pts;
            int i = 0;
            LongPollHistoryResponse history = null;
            string errorLog = "";

            while (i < 5 && history == null)
            {
                i++;
                try
                {
                    history = vkapi.Messages.GetLongPollHistory(rp);
                }
                catch (TooManyRequestsException)
                {
                    Thread.Sleep(150);
                    i--;
                }
                catch (Exception ex)
                {
                    errorLog += string.Format("{0} - {1}{2}", i, ex.Message, Environment.NewLine);
                }
            }

            if (history != null)
            {
                Pts = history.NewPts;
                foreach (var m in history.Messages)
                {
                    m.FromId = m.Type == MessageType.Sended ? vkapi.UserId : m.UserId;
                }
            }
            else ColorMessage(errorLog, ConsoleColor.Red);
            return history;
        }

        static Task<LongPollHistoryResponse> GetLongPoolHistoryAsync()
        {
            return Task.Run(() => { return GetLongPoolHistory(); });
        }

        static async void WatchAsync(object state)
        {
            LongPollHistoryResponse history = await GetLongPoolHistoryAsync();
            if (history.Messages.Count > 0)
            {
                CurrentSleepSteps = 1;
                NewMessages?.Invoke(vkapi, history.Messages);
            }
            else if (CurrentSleepSteps < MaxSleepSteps) CurrentSleepSteps++;
            WatchTimer.Change(CurrentSleepSteps * StepSleepTime, Timeout.Infinite);
        }

        static async void StartAsync(ulong? lastTs = null, ulong? lastPts = null)
        {
            if (IsActive) ColorMessage("Messages for {0} already watching", ConsoleColor.Red);
            IsActive = true;
            await GetLongPoolServerAsync(lastPts);
            WatchTimer = new Timer(new TimerCallback(WatchAsync), null, 0, Timeout.Infinite);
        }

        static void Stop()
        {
            if (WatchTimer != null) WatchTimer.Dispose();
            IsActive = false;
            WatchTimer = null;
        }

        public static void Restart()
        {
            Process.Start((Process.GetCurrentProcess()).ProcessName);
            Environment.Exit(0);
        }
    }
}

public class RssNews
{
    public string Title;
    public string PublicationDate;
    public string Description;
}