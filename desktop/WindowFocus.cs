using System;using System.Runtime.InteropServices;using System.Windows.Forms;
public static class WindowFocus {
    class Pulse {public bool Original;public Timer Timer;}
    static readonly System.Collections.Generic.Dictionary<Form,Pulse> pulses=new System.Collections.Generic.Dictionary<Form,Pulse>();
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
    public static void ShowFront(Form window) {
        if(window.IsDisposed)return;
        if(window.Owner!=null&&window.Owner.WindowState==FormWindowState.Minimized){window.Owner.WindowState=FormWindowState.Normal;ShowWindow(window.Owner.Handle,9);}
        if(window.WindowState==FormWindowState.Minimized)window.WindowState=FormWindowState.Normal;
        ShowWindow(window.Handle,window.WindowState==FormWindowState.Maximized?5:9);
        Pulse previous;bool wasTopMost=window.TopMost;
        if(pulses.TryGetValue(window,out previous)){wasTopMost=previous.Original;previous.Timer.Stop();previous.Timer.Dispose();}
        window.TopMost=true;window.BringToFront();SetForegroundWindow(window.Handle);window.Activate();
        var pulse=new Pulse {Original=wasTopMost,Timer=new Timer {Interval=1200}};pulses[window]=pulse;
        pulse.Timer.Tick+=(s,e)=>{pulse.Timer.Stop();Pulse current;if(pulses.TryGetValue(window,out current)&&Object.ReferenceEquals(current,pulse)){if(!window.IsDisposed)window.TopMost=pulse.Original;pulses.Remove(window);}pulse.Timer.Dispose();};pulse.Timer.Start();
    }
}
