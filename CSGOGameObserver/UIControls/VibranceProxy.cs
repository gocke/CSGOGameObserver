using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CSGOGameObserver.UIControls
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VIBRANCE_INFO
    {
        public bool isInitialized;
        public int activeOutput;
        public int defaultHandle;
        public int userVibranceSettingDefault;
        public int userVibranceSettingActive;
        public String szGpuName;
        public bool shouldRun;
        public bool keepActive;
        public int sleepInterval;
        public List<int> displayHandles;
    }
    
    public class VibranceProxy
    {
        #region DLL Imports

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?initializeLibrary@vibrance@vibranceDLL@@QAE_NXZ",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern bool initializeLibrary();

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?unloadLibrary@vibrance@vibranceDLL@@QAE_NXZ",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern bool unloadLibrary();

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?getActiveOutputs@vibrance@vibranceDLL@@QAEHQAPAH0@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern int getActiveOutputs([In, Out] int[] gpuHandles, [In, Out] int[] outputIds);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?enumeratePhsyicalGPUs@vibrance@vibranceDLL@@QAEXQAPAH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern void enumeratePhsyicalGPUs([In, Out] int[] gpuHandles);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?getGpuName@vibrance@vibranceDLL@@QAE_NQAPAHPAD@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        static extern bool getGpuName([In, Out] int[] gpuHandles, StringBuilder szName);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?enumerateNvidiaDisplayHandle@vibrance@vibranceDLL@@QAEHH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern int enumerateNvidiaDisplayHandle(int index);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?setDVCLevel@vibrance@vibranceDLL@@QAE_NHH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        public static extern bool setDVCLevel([In] int defaultHandle, [In] int level);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?isCsgoActive@vibrance@vibranceDLL@@QAE_NPAPAUHWND__@@@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        public static extern bool isCsgoActive(ref IntPtr hwnd);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?isCsgoStarted@vibrance@vibranceDLL@@QAE_NPAPAUHWND__@@@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        public static extern bool isCsgoStarted(ref IntPtr hwnd);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?getAssociatedNvidiaDisplayHandle@vibrance@vibranceDLL@@QAEHPBDH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        static extern int getAssociatedNvidiaDisplayHandle(string deviceName, [In] int length);

        #endregion

        public const int NVAPI_MAX_PHYSICAL_GPUS = 64;

        public const string NVAPI_ERROR_INIT_FAILED = "VibranceProxy failed to initialize! Read readme.txt for fix!";

        public VIBRANCE_INFO VibranceInfo;

        public VibranceProxy()
        {
            try
            {
                VibranceInfo = new VIBRANCE_INFO();
                bool ret = initializeLibrary();

                int[] gpuHandles = new int[NVAPI_MAX_PHYSICAL_GPUS];
                int[] outputIds = new int[NVAPI_MAX_PHYSICAL_GPUS];
                enumeratePhsyicalGPUs(gpuHandles);

                EnumerateDisplayHandles();
               
                VibranceInfo.activeOutput = getActiveOutputs(gpuHandles, outputIds);
                StringBuilder buffer = new StringBuilder(64);
                char[] sz = new char[64];
                getGpuName(gpuHandles, buffer);
                VibranceInfo.szGpuName = buffer.ToString();
                VibranceInfo.defaultHandle = enumerateNvidiaDisplayHandle(0);
                VibranceInfo.isInitialized = true;
            }
            catch (Exception)
            {
                MessageBox.Show(VibranceProxy.NVAPI_ERROR_INIT_FAILED);
            }

        }

        public int GetCsgoDisplayHandle()
        {
            IntPtr hwnd = IntPtr.Zero;
            if (isCsgoStarted(ref hwnd) && hwnd != IntPtr.Zero)
            {
                var primaryScreen = System.Windows.Forms.Screen.FromHandle(hwnd);

                string deviceName = primaryScreen.DeviceName;
                GCHandle handle = GCHandle.Alloc(deviceName, GCHandleType.Pinned);
                int id = getAssociatedNvidiaDisplayHandle(deviceName, deviceName.Length);
                handle.Free();

                return id;
            }

            return -1;
        }

        public bool UnloadLibraryEx()
        {
            return unloadLibrary();
        }

        private void EnumerateDisplayHandles()
        {
            int displayHandle = 0;
            for (int i = 0; displayHandle != -1; i++)
            {
                if (VibranceInfo.displayHandles == null)
                    VibranceInfo.displayHandles = new List<int>();

                displayHandle = enumerateNvidiaDisplayHandle(i);
                if (displayHandle != -1)
                    VibranceInfo.displayHandles.Add(displayHandle);
            }
        }
    }
}
