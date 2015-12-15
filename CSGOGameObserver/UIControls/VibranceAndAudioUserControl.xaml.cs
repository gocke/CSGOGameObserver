using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CSGOGameObserver.Annotations;
using CSGOGameObserver.AudioDeviceSwitcher;

namespace CSGOGameObserver.UIControls
{
    /// <summary>
    /// Interaktionslogik für VibranceAndAudioUserControl.xaml
    /// </summary>
    public partial class VibranceAndAudioUserControl : UserControl
    {
        private VibranceProxy vibranceProxy;
        private List<AudioDevice> audioDeviceList = new List<AudioDevice>();
        private bool stillRunning = false;
        private DispatcherTimer refreshDispatcherTimer = new DispatcherTimer();
        private readonly Object Object1 = new Object();
        private bool isActive;

        public VibranceAndAudioUserControl()
        {
            InitializeComponent();

            refreshDispatcherTimer.Tick += CheckIfCSGOActive;
        }

        private void VibranceAndAudioUserControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                ThreadStart audioDevicesThreadStart = PopulateAudioDeviceCombobox;
                Thread audioDeviceThread = new Thread(audioDevicesThreadStart);
                audioDeviceThread.Start();
            }

            System.Runtime.InteropServices.Marshal.PrelinkAll(typeof(VibranceProxy));
        }

        public void CSGOIsRunning()
        {
            if (!stillRunning)
            {
                stillRunning = true;
                isActive = true;

                #region AquireData

                //Extract Data from the UI Thread              
                bool isAudioEnabled = false;
                bool isVibranceEnabled = false;
                int refreshRate = 0;
                int inGameVibranceLevel = 0;
                int windowsVibranceLevel = 0;

                Dispatcher.Invoke(new Action(() =>
                {                 
                    isAudioEnabled = UseAudioSettingsCheckBox.IsChecked == true;
                    isVibranceEnabled = UseVibranceSettingsCheckBox.IsChecked == true;
                    inGameVibranceLevel = (int) InGameVibranceLevelSlider.Value;
                    windowsVibranceLevel = (int) WindowsVibranceLevelSlider.Value;

                    //Making sure the User doesn't DDOS himself
                    if (int.TryParse(RefreshRateTextBox.Text, out refreshRate))
                        refreshRate = refreshRate < 100 ? refreshRate : 100;
                }));

                #endregion

                #region Audio

                //If Audio is enabled we want to switch the Audio Device when CSGO is running
                if (isAudioEnabled)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        if (InGameAudioDeviceComboBox.SelectedItem != null)
                        {
                            int audioDevice = ((AudioDevice) InGameAudioDeviceComboBox.SelectedItem).DeviceID;
                            AudioDeviceController.SetAudioDevice(audioDevice);
                        }
                    }));
                }

                #endregion

                #region Vibrance

                //If Vibrance is enabled we want to set the Vibrance when go is running
                if (isVibranceEnabled && windowsVibranceLevel != inGameVibranceLevel)
                {
                    vibranceProxy = new VibranceProxy();

                    if (vibranceProxy.VibranceInfo.isInitialized)
                    {
                        int csgoHandle = vibranceProxy.GetCsgoDisplayHandle();
                        if (csgoHandle != -1)
                        {
                            vibranceProxy.VibranceInfo.defaultHandle = csgoHandle;
                        }

                        VibranceProxy.setDVCLevel(vibranceProxy.VibranceInfo.defaultHandle, getNVIDIAValue(inGameVibranceLevel));

                        refreshDispatcherTimer.Interval = new TimeSpan(0,0,0,0, refreshRate);
                        refreshDispatcherTimer.Start();
                    }
                }
                #endregion
            }
        }

        public void CSGOWasRunning()
        {
            lock (Object1)
            {
                isActive = false;
                //disable DispatcherTimer
                refreshDispatcherTimer.Stop();

                #region AquireData

                //Extract Data from the UI Thread           
                bool isAudioEnabled = false;
                bool isVibranceEnabled = false;
                int inGameVibranceLevel = 0;
                int windowsVibranceLevel = 0;

                Dispatcher.Invoke(new Action(() =>
                {
                    isAudioEnabled = UseAudioSettingsCheckBox.IsChecked == true;
                    isVibranceEnabled = UseVibranceSettingsCheckBox.IsChecked == true;
                    inGameVibranceLevel = (int) InGameVibranceLevelSlider.Value;
                    windowsVibranceLevel = (int) WindowsVibranceLevelSlider.Value;
                }));
                #endregion

                #region Audio

                if (isAudioEnabled)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        if (WindowsAudioDeviceComboBox.SelectedItem != null)
                        {
                            int audioDevice = ((AudioDevice) WindowsAudioDeviceComboBox.SelectedItem).DeviceID;
                            AudioDeviceController.SetAudioDevice(audioDevice);
                        }
                    }));
                }

                #endregion

                #region Vibrance

                //If Vibrance is enabled we want to set the Vibrance when go is running
                if (isVibranceEnabled && windowsVibranceLevel != inGameVibranceLevel)
                {
                    if (vibranceProxy != null && vibranceProxy.VibranceInfo.isInitialized)
                    {
                        //vibranceProxy.VibranceInfo.displayHandles.ForEach(handle => VibranceProxy.setDVCLevel(handle, 0));
                        VibranceProxy.setDVCLevel(vibranceProxy.VibranceInfo.defaultHandle, getNVIDIAValue(windowsVibranceLevel));

                        //vibranceProxy.UnloadLibraryEx();
                        stillRunning = false;
                    }
                }

                #endregion
            }
        }

        //Checks if the Window is minimized and changes Digital Vibrance accordingly
        private void CheckIfCSGOActive(object sender, EventArgs eventArgs)
        {
            lock (Object1)
            {
                IntPtr hwnd = IntPtr.Zero;
                if (stillRunning && VibranceProxy.isCsgoStarted(ref hwnd) && !VibranceProxy.isCsgoActive(ref hwnd) && isActive)
                {
                    isActive = false;
                   
                    int windowsVibranceLevel = 0;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        windowsVibranceLevel = (int)WindowsVibranceLevelSlider.Value;
                    }));

                    VibranceProxy.setDVCLevel(vibranceProxy.VibranceInfo.defaultHandle, getNVIDIAValue(windowsVibranceLevel));
                }
                if (stillRunning && VibranceProxy.isCsgoStarted(ref hwnd) && VibranceProxy.isCsgoActive(ref hwnd) && !isActive)
                {
                    isActive = true;

                    int inGameVibranceLevel = 0;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        inGameVibranceLevel = (int)InGameVibranceLevelSlider.Value;
                    }));

                    VibranceProxy.setDVCLevel(vibranceProxy.VibranceInfo.defaultHandle, getNVIDIAValue(inGameVibranceLevel));
                }
            }
        }

        //Fills the audiodevices Combobox
        private void PopulateAudioDeviceCombobox()
        {
            audioDeviceList = AudioDeviceController.GetAudioDevices();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                WindowsAudioDeviceComboBox.ItemsSource = audioDeviceList;
                InGameAudioDeviceComboBox.ItemsSource = audioDeviceList;
            }));
        }

        //Transforms 0-100 to 0-63 with negativ values
        public int getNVIDIAValue(int value)
        {
            int NVIDIADEFAULT = 50;
            int NVIDIARANGE = 63;

            double onePercent = NVIDIARANGE/50.0;

            int returnValue;

            if (value >= NVIDIADEFAULT)
                returnValue = (int) (onePercent*(value - NVIDIADEFAULT));
            else
                returnValue = (int) (onePercent*(NVIDIADEFAULT - value)*-1);

            return returnValue;
        }
    }
}
