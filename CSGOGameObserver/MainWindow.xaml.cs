﻿////////////////////
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
using CSGOGameObserverSDK;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CSGOGameObserver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const double BOMBTIME = 40.0;

        private Boolean bombPlanted;
        private DispatcherTimer bombTimer;
        public double timeLeft = BOMBTIME;
        private object Object1 = new object();
        private DateTime bombStartDateTime = DateTime.Now;

        public MainWindow()
        {
            if(!IsAdministrator())
                RestartAsAdmin();

            InitializeComponent();

            bombTimer = new DispatcherTimer();
            bombTimer.Tick += RefreshTimer;
            bombTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
        }

        //OnLoaded Run Server
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            ThreadStart serverThreadStart = new ThreadStart(RunServer);
            Thread serverThread = new Thread(serverThreadStart);
            serverThread.Start();
        }

        public void RunServer()
        {
            CSGOGameObserverServer csgoGameObserverServer = new CSGOGameObserverServer("http://127.0.0.1:3000/");
            csgoGameObserverServer.receivedCSGOServerMessage += OnReceivedCsgoServerMessage;
            csgoGameObserverServer.Start();
        }

        private void OnReceivedCsgoServerMessage(object sender, JObject gameData)
        {
            //Prevent Racing conditions, events might be multithreaded
            lock (Object1)
            {
                if (!bombPlanted)
                {
                    if (gameData["round"]?["bomb"] != null)
                    {
                        bombStartDateTime = DateTime.Now;
                        bombPlanted = true;
                        bombTimer.Start();
                    }
                }
                if (bombPlanted)
                {
                    if (gameData["round"]?["bomb"] == null || gameData["round"]["bomb"].ToString() == "defused")
                    {
                        bombPlanted = false;
                        bombTimer.Stop();

                        timeLeft = BOMBTIME;
                        BombStatusTextBlock.Dispatcher.BeginInvoke(
                            (Action)(() => BombStatusTextBlock.Text = $"Bomb not Planted."));
                        BombStatusTextBlock.Dispatcher.BeginInvoke(
                            (Action)(() => InfoTextBlock.Text = $"No Kit required."));
                    }
                }
            }
        }

        //This timer is started when the bomb is planted, it refreshes the Time left on the Bomb.
        private void RefreshTimer(object sender, EventArgs e)
        {
            // runs on UI thread
            BombStatusTextBlock.Dispatcher.BeginInvoke(
                (Action)(() => BombStatusTextBlock.Text = $"Bomb Planted! Time Left: {timeLeft:#0.00}s"));

            //If exactly 10 seconds are Left
            if (timeLeft > 10.0 && timeLeft < 10.3)
            {
                BombStatusTextBlock.Dispatcher.BeginInvoke(
                    (Action) (() => InfoTextBlock.Text = "Kit required!"));

                SystemSounds.Beep.Play();
            }
            //If exactly/less than 5 seconds is Left
            if ((timeLeft - 5.0) < 0.01)
            {
                BombStatusTextBlock.Dispatcher.BeginInvoke(
                    (Action) (() => InfoTextBlock.Text = "It's gonna Blow!"));
            }

            timeLeft = BOMBTIME - (DateTime.Now - bombStartDateTime).TotalSeconds;
        }

        static void RestartAsAdmin()
        {
            var startInfo = new ProcessStartInfo("CSGOGameObserver.exe") { Verb = "runas" };
            Process.Start(startInfo);
            Environment.Exit(0);
        }

        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                    .IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}