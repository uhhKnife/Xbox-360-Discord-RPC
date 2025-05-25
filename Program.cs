using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using JRPC_Client;
using XDevkit;
using DiscordRPC;

class Program
{
    static IXboxConsole Jtag;
    static Dictionary<uint, string> titleIdMap = new Dictionary<uint, string>();
    static DiscordRpcClient discord;
    static DateTime gameStartTime;
    static bool isConnected = false;

    static void Main()
    {
        try
        {
            discord = new DiscordRpcClient("1376213828366368908");
            discord.Initialize();

            loadCsv("titles.csv");

            string lastTitle = null;
            gameStartTime = DateTime.Now;

            while (true)
            {
                try
                {
                    if (!isConnected)
                    {
                        Console.Clear();
                        Console.WriteLine("Waiting for Xbox connection...");
                        Console.WriteLine("Please connect your Xbox and ensure it's powered on");
                        Console.WriteLine("\nPress Ctrl+C to exit");

                        if (Jtag.Connect(out Jtag))
                        {
                            isConnected = true;
                            Console.Clear();
                            Console.WriteLine("Connected to Xbox!");
                            Jtag.XNotify("XboxRPC Connected!");
                            gameStartTime = DateTime.Now;
                            lastTitle = null;
                        }
                        Thread.Sleep(2000);
                        continue;
                    }

                    try
                    {
                        uint titleId = (uint)Jtag.XamGetCurrentTitleId();
                        string titleName = titleIdMap.ContainsKey(titleId) ? titleIdMap[titleId] : $"Unknown ({titleId:X8})";

                        if (titleName != lastTitle)
                        {
                            gameStartTime = DateTime.Now;
                            lastTitle = titleName;
                        }

                        TimeSpan elapsed = DateTime.Now - gameStartTime;
                        string elapsedTime = $"Playing for {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

                        Console.Clear();
                        Console.WriteLine($"Current Game: {titleName}");
                        Console.WriteLine($"Title ID: {titleId:X8}");
                        Console.WriteLine(elapsedTime);
                        Console.WriteLine("\nPress Ctrl+C to exit");

                        discord.SetPresence(new RichPresence
                        {
                            Details = $"Playing: {titleName}",
                            State = $"Title ID: {titleId:X8}",
                            Assets = new Assets
                            {
                                LargeImageKey = "xbox",
                                LargeImageText = "Xbox 360"
                            }
                        });
                    }
                    catch (Exception)
                    {
                        isConnected = false;
                        Console.Clear();
                        Console.WriteLine("Xbox connection lost. Waiting for reconnection...");
                        discord.ClearPresence();
                        continue;
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Thread.Sleep(5000);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        finally
        {
            discord?.Dispose();
        }
    }

    static void loadCsv(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Warning: CSV file not found at {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2) return;

        string[] headers = lines[0].Split(',');
        int titleIdIndex = Array.IndexOf(headers, "Title ID");
        int gameNameIndex = Array.IndexOf(headers, "Game Name");

        if (titleIdIndex == -1 || gameNameIndex == -1)
        {
            Console.WriteLine("Error: Could not find required columns in CSV file");
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(',');
            if (values.Length <= Math.Max(titleIdIndex, gameNameIndex)) continue;

            try
            {
                string titleIdStr = values[titleIdIndex].Trim();
                if (string.IsNullOrEmpty(titleIdStr)) continue;

                titleIdStr = new string(titleIdStr.Where(c => "0123456789ABCDEFabcdef".Contains(c)).ToArray());
                if (string.IsNullOrEmpty(titleIdStr)) continue;

                titleIdStr = titleIdStr.PadLeft(8, '0');

                uint id = Convert.ToUInt32(titleIdStr, 16);
                string gameName = values[gameNameIndex].Trim();

                if (!string.IsNullOrEmpty(gameName))
                {
                    titleIdMap[id] = gameName;
                    Console.WriteLine($"Loaded: {gameName} ({id:X8})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not process row {i}: {ex.Message}");
                Console.WriteLine($"Row content: {lines[i]}");
            }
        }

        Console.WriteLine($"Loaded {titleIdMap.Count} unique titles from CSV");
    }
}
