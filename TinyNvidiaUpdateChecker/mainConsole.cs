﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using HtmlAgilityPack;


namespace TinyNvidiaUpdateChecker
{

    /*
    TinyNvidiaUpdateChecker - Check for NVIDIA GPU drivers, GeForce Experience replacer
    Copyright (C) 2016 Hawaii_Beach

    This program Is free software: you can redistribute it And/Or modify
    it under the terms Of the GNU General Public License As published by
    the Free Software Foundation, either version 3 Of the License, Or
    (at your option) any later version.

    This program Is distributed In the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty Of
    MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License For more details.

    You should have received a copy Of the GNU General Public License
    along with this program.  If Not, see <http://www.gnu.org/licenses/>.
    */

    class mainConsole
    {

        /// <summary>
        /// Server adress
        /// </summary>
        private readonly static string serverURL = "https://elpumpo.github.io/TinyNvidiaUpdateChecker/";

        /// <summary>
        /// Current client version
        /// </summary>
        private static string offlineVer = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        
        /// <summary>
        /// Remote client version
        /// </summary>
        private static int onlineVer;

        /// <summary>
        /// Current GPU driver version
        /// </summary>
        private static int offlineGPUDriverVersion;

        /// <summary>
        /// Remote GPU driver version
        /// </summary>
        private static int onlineGPUDriverVersion;

        /// <summary>
        /// Langauge ID for GPU driver download
        /// </summary>
        private static int langID;

        private static string finalURL;
        private static string driverURL;

        /// <summary>
        /// Local Windows version
        /// </summary>
        private static string winVer;

        /// <summary>
        /// OS ID for GPU driver download
        /// </summary>
        private static int osID;

        /// <summary>
        /// Show UI or go quiet | 1: show | 0: quiet
        /// </summary>
        private static int showUI = 1;

        /// <summary>
        /// Enable extended information
        /// </summary>
        private static Boolean debug = false;

        /// <summary>
        /// Direction for configuration folder
        /// </summary>
        private static string dirToConfig = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Hawaii_Beach\TinyNvidiaUpdateChecker\";

        private static string fullConfig = dirToConfig + "app.config";


        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeConsole();

        [STAThread]
        private static void Main(string[] args)
        {
            Console.Title = "TinyNvidiaUpdateChecker v" + offlineVer;
            string[] parms = Environment.GetCommandLineArgs();
            int isSet = 0;
            if (parms.Length > 1)
            {
                
                // go quiet mode
                if(Array.IndexOf(parms, "--quiet") != -1) //@todo fix broken function
                {
                    FreeConsole();
                    showUI = 0;
                    isSet = 1;
                }

                // erase config
                if (Array.IndexOf(parms, "--eraseConfig") != -1)
                {
                    isSet = 1;
                    if (File.Exists(fullConfig))
                    {
                        File.Delete(fullConfig);
                    }
                }

                // enable debug
                if (Array.IndexOf(parms, "--debug") != -1)
                {
                    isSet = 1;
                    debug = true;
                }

                // help menu
                if(Array.IndexOf(parms, "--help") != -1)
                {
                    isSet = 1;
                    introMessage();
                    Console.WriteLine("Usage: " + Path.GetFileName(Assembly.GetEntryAssembly().Location) + " [--quiet] [--eraseConfig] [--debug] [--help]");
                    Console.WriteLine();
                    Console.WriteLine("--quiet        Run application quiet.");
                    Console.WriteLine("--eraseConfig  Erase local configuration file.");
                    Console.WriteLine("--debug        Enable debugging for extended information.");
                    Console.WriteLine("--help         Displays this message.");
                    Environment.Exit(0);
                }

                if(isSet == 0)
                {
                    introMessage();
                    Console.WriteLine("Unknown command, type --help for help.");
                    Environment.Exit(1);
                }

            }
            if(showUI == 1) AllocConsole();

            introMessage();

            checkDll();

            configSetup(); // read & write configuration file

            checkWinVer(); // get current windows version

            getLanguage(); // get current langauge

            if(readValue("Check for Updates") == "true")
            {
                searchForUpdates();
            }

            gpuInfo();

            if(onlineGPUDriverVersion == offlineGPUDriverVersion)
            {
                Console.WriteLine("GPU drivers are up-to-date!");
            } else {
                if (offlineGPUDriverVersion > onlineGPUDriverVersion)
                {
                    Console.WriteLine("Current GPU driver is newer than remote!");
                }

                if(onlineGPUDriverVersion < offlineGPUDriverVersion)
                {
                    Console.WriteLine("GPU drivers are up-to-date!");
                } else {
                    Console.WriteLine("There are new drivers to download!");
                    DialogResult dialog = MessageBox.Show("There's a new update available to download, do you want to download the update now?", "TinyNvidiaUpdateChecker", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                    if(dialog == DialogResult.Yes)
                    {
                        Process.Start(driverURL);
                    }
                }
            }

            if(debug == true)
            {
                Console.WriteLine("offlineGPUDriverVersion: " + offlineGPUDriverVersion);
                Console.WriteLine("onlineGPUDriverVersion:  " + onlineGPUDriverVersion);
            }

            Console.WriteLine();
            Console.WriteLine("Job done! Press any key to exit.");
            if(showUI == 1) Console.ReadKey();
            Environment.Exit(0);
        }

        private static void configSetup()
        {

            // set config dir
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", fullConfig);

            if(debug == true)
            {
                Console.WriteLine("Current configuration file is located at: " + AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
            }

            // create config file
            if(!File.Exists(fullConfig))
            {
                Console.WriteLine("Generating configuration file, this only happenes once.");
                Console.WriteLine("The configuration file is located at: " + dirToConfig);

                setupValue("Check for Updates");
                setupValue("GPU Type");
            }

            Console.WriteLine();

        } // configuration files

        private static void searchForUpdates()
        {
            Console.Write("Searching for Updates . . . ");
            int error = 0;
            try
            {
                HtmlWeb webClient = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument htmlDocument = webClient.Load(serverURL);

                // get version
                HtmlNode tdVer = htmlDocument.DocumentNode.Descendants().SingleOrDefault(x => x.Id == "currentVersion");
                onlineVer = Convert.ToInt32(tdVer.InnerText.Replace(".", string.Empty));
            }

            catch (Exception ex)
            {
                error = 1;
                Console.Write("ERROR!");
                Console.WriteLine();
                Console.WriteLine(ex.StackTrace);
            }
            if(error == 0)
            {
                Console.Write("OK!");
                Console.WriteLine();
            }
            int iOfflineVer = Convert.ToInt32(offlineVer.Replace(".", string.Empty));
            if (onlineVer > iOfflineVer)
            {
                Console.WriteLine("There is a update available for TinyNvidiaUpdateChecker!");
                DialogResult dialog = MessageBox.Show("There's a new client update available to download, do you want to be navigate to the page?", "TinyNvidiaUpdateChecker", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if(dialog == DialogResult.Yes)
                {
                    Process.Start("https://github.com/ElPumpo/TinyNvidiaUpdateChecker/releases");
                }
            }

            if(debug == true)
            {
                Console.WriteLine("iOfflineVer: " + iOfflineVer);
                Console.WriteLine("onlineVer:   " + onlineVer);
            }
            Console.WriteLine();
        } // checks for application updates

        private static void checkWinVer()
        {
            string verOrg = Environment.OSVersion.Version.ToString();

            // Windows 10
            if (verOrg.Contains("10.0"))
            {
                winVer = "10";
                if(Environment.Is64BitOperatingSystem == true)
                {
                    osID = 57;
                } else {
                    osID = 56;
                }
            }
            // Windows 8.1
            else if (verOrg.Contains("6.3"))
            {
                winVer = "8.1";
                if(Environment.Is64BitOperatingSystem == true)
                {
                    osID = 41;
                } else {
                    osID = 40;
                }
            }
            // Windows 8
            else if (verOrg.Contains("6.2"))
            {
                winVer = "8";
                if(Environment.Is64BitOperatingSystem == true)
                {
                    osID = 41;
                } else {
                    osID = 40;
                }
            }
            // Windows 7
            else if (verOrg.Contains("6.1"))
            {
                winVer = "7";
                if(Environment.Is64BitOperatingSystem == true)
                {
                    osID = 41;
                } else {
                    osID = 40;
                }

            } else {
                winVer = "Unknown";
                Console.WriteLine("You're running a non-supported version of Windows; the application will determine itself.");
                Console.WriteLine("OS: " + verOrg);
                if(showUI == 1) Console.ReadKey();
                Environment.Exit(1);
            }

            if(debug == true)
            {
                Console.WriteLine("winVer: " + winVer);
                Console.WriteLine("osID: " + osID.ToString());
                Console.WriteLine("verOrg: " + verOrg);
                Console.WriteLine();
            }
            
            

        } // get local Windows version

        private static void getLanguage()
        {
            string cultName = CultureInfo.CurrentCulture.ToString(); // https://msdn.microsoft.com/en-us/library/ee825488(v=cs.20).aspx - http://www.lingoes.net/en/translator/langcode.htm

            switch (cultName)
            {
                case "en-US":
                    langID = 1;
                    break;
                case "en-GB":
                    langID = 2;
                    break;
                case "zh-CHS":
                    langID = 5;
                    break;
                case "zh-CHT":
                    langID = 6;
                    break;
                case "ja-JP":
                    langID = 7;
                    break;
                case "ko-KR":
                    langID = 8;
                    break;
                case "de-DE":
                    langID = 9;
                    break;
                case "es-ES":
                    langID = 10;
                    break;
                case "fr-FR":
                    langID = 12;
                    break;
                case "it-IT":
                    langID = 13;
                    break;
                case "pl-PL":
                    langID = 14;
                    break;
                case "pt-BR":
                    langID = 15;
                    break;
                case "ru-RU":
                    langID = 16;
                    break;
                case "tr-TR":
                    langID = 19;
                    break;
                default:
                    // intl
                    langID = 17;
                    break;
            }

            if(debug == true)
            {
                Console.WriteLine("langID: " + langID);
                Console.WriteLine("cultName: " + cultName);
                Console.WriteLine();
            }
        } // decide driver langauge 

        private static void gpuInfo()
        {
            Console.Write("Looking up GPU information . . . ");
            int error = 0;

            // query local driver version
            try
            {
                FileVersionInfo nvvsvcExe = FileVersionInfo.GetVersionInfo(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\System32\nvvsvc.exe"); // Sysnative?
                offlineGPUDriverVersion = Convert.ToInt32(nvvsvcExe.FileDescription.Substring(38).Trim().Replace(".", string.Empty));
            }
            catch (Exception ex)
            {
                error = 1;
                Console.Write("ERROR!");
                Console.WriteLine();
                Console.WriteLine(ex.StackTrace);
            }

            int psID = 0;
            int pfID = 0;

            // get correct gpu drivers
            if(readValue("GPU Type") == "desktop") {
                psID = 98;
                pfID = 756;
            }else if(readValue("GPU Type") == "mobile") {
                psID = 99;
                pfID = 757;
            }

            // finish request
            try
            {
                string processURL = "https://www.nvidia.com/Download/processDriver.aspx?psid=" + psID.ToString() + "&pfid=" + pfID.ToString() + "&rpf=1&osid=" + osID.ToString() + "&lid=" + langID.ToString() + "&ctk=0";

                WebClient client = new WebClient();
                Stream stream = client.OpenRead(processURL);
                StreamReader reader = new StreamReader(stream);
                finalURL = reader.ReadToEnd();
                reader.Close();
                stream.Close();
            }
            catch (Exception ex)
            {
                if(error == 0)
                {
                    Console.Write("ERROR!");
                    Console.WriteLine();
                    error = 1;
                }
                Console.WriteLine(ex.StackTrace);
            }

            try
            {
                // HTMLAgilityPack
                // thanks to http://www.codeproject.com/Articles/691119/Html-Agility-Pack-Massive-information-extraction-f for a great article

                HtmlWeb webClient = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument htmlDocument = webClient.Load(finalURL);

                // get version
                HtmlNode tdVer = htmlDocument.DocumentNode.Descendants().SingleOrDefault(x => x.Id == "tdVersion");
                onlineGPUDriverVersion = Convert.ToInt32(tdVer.InnerHtml.Trim().Substring(0, 6).Replace(".", string.Empty));

                // get driver URL
                IEnumerable<HtmlNode> links = htmlDocument.DocumentNode.Descendants("a").Where(x => x.Attributes.Contains("href"));
                foreach (var link in links)
                {
                    if(link.Attributes["href"].Value.Contains("/content/DriverDownload-March2009/"))
                    {
                        driverURL = "https://www.nvidia.com" + link.Attributes["href"].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                if (error == 0)
                {
                    Console.Write("ERROR!");
                    Console.WriteLine();
                    error = 1;
                }
                Console.WriteLine(ex.StackTrace);
            }

            if(error == 0)
            {
                Console.Write("OK!");
                Console.WriteLine();
            }

            if(debug == true)
            {
                Console.WriteLine("psid: " + psID);
                Console.WriteLine("pfid: " + pfID);
            }

        } // get local and remote GPU driver version

        private static void checkDll()
        {
            if(!File.Exists("HtmlAgilityPack.dll"))
            {
                Console.WriteLine("The required binary cannot be found and the application will determinate itself. It must be put in the same folder as this executable.");
                if(showUI == 1) Console.ReadKey();
                Environment.Exit(2);
            }
        } // check necessary dll

        private static void introMessage()
        {
            Console.WriteLine("TinyNvidiaUpdateChecker v" + offlineVer);
            Console.WriteLine();
            Console.WriteLine("Copyright (C) 2016 Hawaii_Beach");
            Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY");
            Console.WriteLine("This is free software, and you are welcome to redistribute it");
            Console.WriteLine("under certain conditions. Licensed under GPLv3.");
            Console.WriteLine();
        } // show legal message

        private static string readValue(string key)
        {
            string result = null;

            try
            {
                var appSettings = ConfigurationManager.AppSettings[key];

                if(appSettings != null)
                {
                    result = appSettings;
                } else {

                    // error reading key
                    Console.WriteLine();
                    Console.WriteLine("Error reading configuration file, attempting to repair key '" + key + "' . . .");
                    setupValue(key);

                    result = ConfigurationManager.AppSettings[key]; //refresh var
                }
            }
            catch (ConfigurationErrorsException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine();
            }

            return result;
        } // read key from config

        private static void setValue(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine();
            }
        } // create / update key in config

        private static void setupValue(string key)
        {
            string message = null;
            string[] value = null;

            switch (key) {

                // check for update
                case "Check for Updates":
                    message = "Do you want to search for client updates?";
                    value = new string[] { "true", "false" };
                    break;

                // gpu
                case "GPU Type":
                    message = "If you're running a desktop GPU select Yes, if you're running a mobile GPU select No.";
                    value = new string[] { "desktop", "mobile" };
                    break;

                default:
                    message = "Unknown";
                    value = null;
                    break;

            }

            DialogResult dialogUpdates = MessageBox.Show(message, "TinyNvidiaUpdateChecker", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if(dialogUpdates == DialogResult.Yes) {
                setValue(key, value[0]);
            } else {
                setValue(key, value[1]);
            }

        } // setup for config values
    }
}