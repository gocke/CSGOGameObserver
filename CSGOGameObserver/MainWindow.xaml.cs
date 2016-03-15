////////////////////
// 
// This Code Listens to the CSGO GameState API and provides feedback
// 
// Author: Johannes Gocke, johannes_gocke@hotmail.de
// 
///////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CSGOGameObserver.Helpers;
using CSGOGameObserverSDK;
using CSGOGameObserverSDK.GameDataTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CSGOGameObserver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const double BOMBTIME = 36.0;

        private Boolean bombPlanted;
        private DispatcherTimer bombTimer;
        private double timeLeft = BOMBTIME;
        private object Object1 = new object();
        private DateTime bombStartDateTime = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();

            bombTimer = new DispatcherTimer();
            bombTimer.Tick += RefreshTimer;
            bombTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);

            //If CSGO is started we want to change Audio/Vibrance settings
            ThreadStart monitorCSGOThreadStart = MonitorCSGO;
            Thread monitorCSGOThread = new Thread(monitorCSGOThreadStart);
            monitorCSGOThread.Start();
        }

        //OnLoaded Run Server for Gamestate
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            ThreadStart serverThreadStart = new ThreadStart(RunServer);
            Thread serverThread = new Thread(serverThreadStart);
            serverThread.Start();
        }

        #region CSGOStatus Control

        //Monitors CSGO, activates Audi/Vibrance settings
        private void MonitorCSGO()
        {
            bool wasRunning = false;

            while (true)
            {
                if (Process.GetProcessesByName("csgo").Length != 0)
                {
                    wasRunning = true;
                    VibranceAndAudioUserControlInstance.CSGOIsRunning();
                }

                if (Process.GetProcessesByName("csgo").Length == 0 && wasRunning)
                {
                    wasRunning = false;
                    VibranceAndAudioUserControlInstance.CSGOWasRunning();
                }

                Thread.Sleep(5000);
            }
        }

        #endregion

        #region GameState 

        public void RunServer()
        {
            CSGOGameObserverServer csgoGameObserverServer = new CSGOGameObserverServer("http://localhost:3000/");
            csgoGameObserverServer.receivedCSGOServerMessage += OnReceivedCsgoServerMessage;
            csgoGameObserverServer.Start();
        }

        private void OnReceivedCsgoServerMessage(object sender, JObject gameData)
        {
            //Transform JSON to CSGOGameState
            CSGOGameState csgoGameState = new CSGOGameState(gameData);

            //Prevent Racing conditions, events might be multithreaded
            lock (Object1)
            {
                if (!bombPlanted)
                {
                    if (csgoGameState.Round.Bomb != null && csgoGameState.Provider.Timestamp != null)
                    {
                        long currentTime = (long) csgoGameState.Provider.Timestamp;
                        bombStartDateTime = TimeHelper.UnixTimeStampInSecondsToDateTime(currentTime);

                        bombPlanted = true;
                        bombTimer.Start();
                    }
                }
                if (bombPlanted)
                {
                    if (gameData["round"]?["bomb"] == null || gameData["round"].Value<String>("defused") == "defused")
                    {
                        bombPlanted = false;
                        bombTimer.Stop();

                        timeLeft = BOMBTIME;
                        BombStatusTextBlock.Dispatcher.BeginInvoke(
                            (Action)(() => BombStatusTextBlock.Text = $"Bomb not Planted."));
                        BombStatusTextBlock.Dispatcher.BeginInvoke(
                            (Action)(() => BombStatusTextBlock.Foreground = new SolidColorBrush(Colors.Black)));
                        InfoTextBlock.Dispatcher.BeginInvoke(
                            (Action)(() => InfoTextBlock.Text = $"No Kit required."));
                        InfoTextBlock.Dispatcher.BeginInvoke(
                            (Action)(() => InfoTextBlock.Foreground = new SolidColorBrush(Colors.Black)));
                    }
                }
            }
        }

        //This timer is started when the bomb is planted, it refreshes the Time left on the Bomb.
        private void RefreshTimer(object sender, EventArgs e)
        {
            // runs on UI thread
            BombStatusTextBlock.Dispatcher.BeginInvoke(
                (Action)(() => BombStatusTextBlock.Text = $"Bomb Planted! \n Time Left: {timeLeft:#0.00}s"));

            BombStatusTextBlock.Dispatcher.BeginInvoke(
                (Action) (() => BombStatusTextBlock.Foreground = new SolidColorBrush(Colors.Tomato)));

            //If exactly 10 seconds are Left
            if (timeLeft > 10.0 && timeLeft < 10.3)
            {
                InfoTextBlock.Dispatcher.BeginInvoke(
                    (Action) (() => InfoTextBlock.Text = "Kit required!"));
                InfoTextBlock.Dispatcher.BeginInvoke(
                    (Action)(() => InfoTextBlock.Foreground = new SolidColorBrush(Colors.Tomato)));

                //SystemSounds.Beep.Play();
            }
            //If exactly/less than 5 seconds is Left
            if ((timeLeft - 5.0) < 0.01)
            {
                InfoTextBlock.Dispatcher.BeginInvoke(
                    (Action) (() => InfoTextBlock.Text = "It's gonna Blow!"));
            }

            timeLeft = BOMBTIME - (DateTime.UtcNow - bombStartDateTime).TotalSeconds;
        }

        #endregion

        #region Events

        //Show Settings
        private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            SettingGrid.Visibility = SettingGrid.Visibility == Visibility.Hidden ? Visibility.Visible : Visibility.Hidden;
        }

        //Saving Settings, disabeling running Vibrance
        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            VibranceAndAudioUserControlInstance.CSGOWasRunning();
            Environment.Exit(0);
        }

        #endregion
    }
}
