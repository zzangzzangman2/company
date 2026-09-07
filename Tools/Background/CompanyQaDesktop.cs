using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// Own-process desktop placement, NOT an input/GPU sandbox. The launcher itself does not
// switch desktops or inject input; a presented child still requires explicit authorization.
public sealed class CompanyQaDesktop : IDisposable
{
    public Process Process { get; private set; }
    public string DesktopName { get; private set; }
    public string InteractiveDesktopAtStart { get; private set; }
    IntPtr desktop, job;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo {
        public int cb; public string reserved, lpDesktop, title;
        public int x,y,xSize,ySize,xChars,yChars,fill,flags;
        public short show, reservedBytes; public IntPtr reserved2,stdin,stdout,stderr;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr process, thread; public uint pid, tid; }
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
        public long processTime, jobTime; public uint flags; public UIntPtr minWorking,maxWorking;
        public uint activeProcesses; public UIntPtr affinity; public uint priority, scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong a,b,c,d,e,f; }
    [StructLayout(LayoutKind.Sequential)] struct JobLimits {
        public BasicLimits basic; public IoCounters io; public UIntPtr processMemory,jobMemory,peakProcess,peakJob;
    }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError=true)] static extern bool CloseDesktop(IntPtr value);
    [DllImport("user32.dll", SetLastError=true)] static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool GetUserObjectInformation(IntPtr value,int index,StringBuilder text,int size,out int needed);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool CreateProcess(string exe,StringBuilder args,IntPtr ps,IntPtr ts,bool inherit,uint flags,
        IntPtr environment,string cwd,ref StartupInfo startup,out ProcessInfo info);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr security,string name);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int kind,ref JobLimits limits,uint size);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool TerminateProcess(IntPtr process,uint exit);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr value);

    public static string ReadInteractiveDesktopName() {
        IntPtr value=OpenInputDesktop(0,false,1);
        if(value==IntPtr.Zero) throw new Win32Exception();
        try { var name=new StringBuilder(256); int needed;
            if(!GetUserObjectInformation(value,2,name,512,out needed)) throw new Win32Exception();
            return name.ToString();
        } finally { CloseDesktop(value); }
    }
    public static void AssertLaunchAllowed(string arguments, bool allowGraphicsQa) {
        // Check before any Win32 call or process creation. Quoted path substrings are not flags.
        var flags = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match token in Regex.Matches(arguments ?? "", "\"[^\"]*\"|[^\\s\"]+")) {
            if (!token.Value.StartsWith("\"", StringComparison.Ordinal)) flags.Add(token.Value);
        }
        if (flags.Contains("-familyCompanyCaptureStock") ||
            flags.Contains("-familyCompanyOfficeBuildNativePointerQa") ||
            flags.Contains("-familyCompanyOfficeBuildPreviewAlignmentQa"))
            throw new InvalidOperationException("NATIVE_INPUT_QA_BLOCKED: private desktops do not isolate native mouse input.");
        if (!allowGraphicsQa && !(flags.Contains("-batchmode") && flags.Contains("-nographics")))
            throw new InvalidOperationException("GRAPHICS_QA_REQUIRES_EXPLICIT_AUTHORIZATION: private-desktop rendering is not headless. Obtain user authorization before passing -AllowGraphicsQa.");
    }
    public static CompanyQaDesktop Start(string exe,string arguments,string cwd) {
        return Start(exe, arguments, cwd, false);
    }
    public static CompanyQaDesktop Start(string exe,string arguments,string cwd,bool allowGraphicsQa) {
        AssertLaunchAllowed(arguments, allowGraphicsQa);
        exe=System.IO.Path.GetFullPath(exe); cwd=System.IO.Path.GetFullPath(cwd);
        var owner=new CompanyQaDesktop(); ProcessInfo info=new ProcessInfo();
        try {
            owner.InteractiveDesktopAtStart=ReadInteractiveDesktopName();
            owner.DesktopName="FamilyCompanyQa_"+Guid.NewGuid().ToString("N");
            // No DESKTOP_SWITCHDESKTOP (0x100) access. The handle cannot activate this desktop.
            owner.desktop=CreateDesktop(owner.DesktopName,IntPtr.Zero,IntPtr.Zero,0,0x01FFu & ~0x0100u,IntPtr.Zero);
            if(owner.desktop==IntPtr.Zero) throw new Win32Exception();
            owner.job=CreateJobObject(IntPtr.Zero,null);
            if(owner.job==IntPtr.Zero) throw new Win32Exception();
            var limits=new JobLimits(); limits.basic.flags=0x2000; // KILL_ON_JOB_CLOSE
            if(!SetInformationJobObject(owner.job,9,ref limits,(uint)Marshal.SizeOf(typeof(JobLimits)))) throw new Win32Exception();
            var startup=new StartupInfo(); startup.cb=Marshal.SizeOf(typeof(StartupInfo));
            startup.lpDesktop="winsta0\\"+owner.DesktopName; startup.flags=1; startup.show=0;
            if(!CreateProcess(exe,new StringBuilder("\""+exe+"\" "+arguments),IntPtr.Zero,IntPtr.Zero,false,
                0x08000004,IntPtr.Zero,cwd,ref startup,out info)) throw new Win32Exception();
            if(!AssignProcessToJobObject(owner.job,info.process)) throw new Win32Exception();
            owner.Process=Process.GetProcessById((int)info.pid);
            // Cache the process handle before a short-lived probe exits.
            IntPtr retained=owner.Process.Handle;
            if(ResumeThread(info.thread)==UInt32.MaxValue) throw new Win32Exception();
            return owner;
        } catch {
            if(info.process!=IntPtr.Zero) TerminateProcess(info.process,1);
            owner.Dispose(); throw;
        } finally {
            if(info.thread!=IntPtr.Zero) CloseHandle(info.thread);
            if(info.process!=IntPtr.Zero) CloseHandle(info.process);
        }
    }
    public void Dispose() {
        if(job!=IntPtr.Zero) { CloseHandle(job); job=IntPtr.Zero; }
        if(Process!=null) { Process.Dispose(); Process=null; }
        if(desktop!=IntPtr.Zero) { CloseDesktop(desktop); desktop=IntPtr.Zero; }
    }
}
