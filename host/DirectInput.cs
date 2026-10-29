using System.Runtime.InteropServices;

namespace GH3Launcher;

/* Just enough DirectInput 8 to see controllers the way x360ce does: the same device GUIDs, every
   axis ranged -32768..32767, buttons and POVs numbered from 1. COM calls go straight through the
   vtables (IDirectInput8W / IDirectInputDevice8W). */
sealed unsafe class DirectInput : IDisposable
{
    public sealed class Device
    {
        public Guid Instance, Product;
        public string Name;
        public bool XInput;               // an XInput pad seen through DirectInput (its path has "IG_")
        public int Axes, Buttons, Povs;
        internal IntPtr Ptr;
        internal bool Acquired;
    }

    // DIJOYSTATE: 6 axes, 2 sliders, 4 POVs, 32 buttons
    [StructLayout(LayoutKind.Sequential)]
    public struct JoyState
    {
        public int X, Y, Z, Rx, Ry, Rz;
        public fixed int Slider[2];
        public fixed uint Pov[4];
        public fixed byte Button[32];
    }

    [DllImport("dinput8.dll")]
    static extern int DirectInput8Create(IntPtr hinst, int version, in Guid riid, out IntPtr di, IntPtr outer);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetModuleHandleW(IntPtr name);

    static readonly Guid IID_IDirectInput8W = new("BF798031-483A-4DA2-AA99-5D64ED369700");
    static readonly Guid[] AxisGuids =
    {
        new("A36D02E0-C9F3-11CF-BFC7-444553540000"), new("A36D02E1-C9F3-11CF-BFC7-444553540000"),
        new("A36D02E2-C9F3-11CF-BFC7-444553540000"), new("A36D02F4-C9F3-11CF-BFC7-444553540000"),
        new("A36D02F5-C9F3-11CF-BFC7-444553540000"), new("A36D02E3-C9F3-11CF-BFC7-444553540000")
    };
    static readonly Guid SliderGuid = new("A36D02E4-C9F3-11CF-BFC7-444553540000");
    static readonly Guid PovGuid = new("A36D02F2-C9F3-11CF-BFC7-444553540000");

    [StructLayout(LayoutKind.Sequential)]
    struct ObjFormat { public Guid* Guid; public int Ofs; public uint Type; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    struct DataFormat { public int Size, ObjSize, Flags, DataSize, NumObjs; public ObjFormat* Objs; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DevInstance
    {
        public int Size;
        public Guid Instance, Product;
        public int DevType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string InstanceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ProductName;
        public Guid FFDriver;
        public short UsagePage, Usage;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PropRange { public int Size, HeaderSize, Obj, How, Min, Max; }
    [StructLayout(LayoutKind.Sequential)]
    struct DevCaps { public int Size, Flags, DevType, Axes, Buttons, Povs, FFSamplePeriod, FFMinTimeResolution, FirmwareRevision, HardwareRevision, FFDriverVersion; }

    IntPtr di;
    readonly IntPtr hwnd;
    readonly IntPtr format;   // DIDATAFORMAT + its objects + their GUIDs, in one unmanaged block
    public readonly List<Device> Devices = new();

    public DirectInput(IntPtr hwnd)
    {
        this.hwnd = hwnd;
        Marshal.ThrowExceptionForHR(DirectInput8Create(GetModuleHandleW(IntPtr.Zero), 0x0800, IID_IDirectInput8W, out di, IntPtr.Zero));
        format = BuildFormat();
    }

    /* c_dfDIJoystick, built by hand (it lives in dinput8.lib, not in the DLL) */
    static IntPtr BuildFormat()
    {
        const uint AnyOptional = 0x00FFFF00 | 0x80000000, Axis = 3, Pov = 0x10, Button = 0x0C, AspectPosition = 0x100;
        int objs = 6 + 2 + 4 + 32;
        int guidBytes = 8 * sizeof(Guid);
        var block = Marshal.AllocHGlobal(sizeof(DataFormat) + objs * sizeof(ObjFormat) + guidBytes);
        var df = (DataFormat*)block;
        var of = (ObjFormat*)(block + sizeof(DataFormat));
        var guids = (Guid*)(block + sizeof(DataFormat) + objs * sizeof(ObjFormat));
        for (int i = 0; i < 6; i++) guids[i] = AxisGuids[i];
        guids[6] = SliderGuid;
        guids[7] = PovGuid;
        int n = 0;
        for (int i = 0; i < 6; i++) of[n++] = new ObjFormat { Guid = &guids[i], Ofs = i * 4, Type = Axis | AnyOptional, Flags = AspectPosition };
        for (int i = 0; i < 2; i++) of[n++] = new ObjFormat { Guid = &guids[6], Ofs = 24 + i * 4, Type = Axis | AnyOptional, Flags = AspectPosition };
        for (int i = 0; i < 4; i++) of[n++] = new ObjFormat { Guid = &guids[7], Ofs = 32 + i * 4, Type = Pov | AnyOptional };
        for (int i = 0; i < 32; i++) of[n++] = new ObjFormat { Guid = null, Ofs = 48 + i, Type = Button | AnyOptional };
        *df = new DataFormat { Size = sizeof(DataFormat), ObjSize = sizeof(ObjFormat), Flags = 1 /* DIDF_ABSAXIS */, DataSize = sizeof(JoyState), NumObjs = objs, Objs = of };
        return block;
    }

    static void** Vt(IntPtr p) => *(void***)p;

    /* The attached game controllers, each opened (background, non-exclusive) and acquired. */
    public void Refresh()
    {
        var found = new List<DevInstance>();
        var handle = GCHandle.Alloc(found);
        try
        {
            var cb = (delegate* unmanaged[Stdcall]<DevInstance*, IntPtr, int>)&EnumCallback;
            // IDirectInput8::EnumDevices(DI8DEVCLASS_GAMECTRL, cb, ref, DIEDFL_ATTACHEDONLY)
            ((delegate* unmanaged[Stdcall]<IntPtr, int, void*, IntPtr, int, int>)Vt(di)[4])(di, 4, cb, GCHandle.ToIntPtr(handle), 1);
        }
        finally { handle.Free(); }

        foreach (var d in Devices.ToList())
            if (!found.Any(f => f.Instance == d.Instance)) { Close(d); Devices.Remove(d); }
        foreach (var f in found)
        {
            if (Devices.Any(d => d.Instance == f.Instance)) continue;
            var dev = Open(f);
            if (dev != null) Devices.Add(dev);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    static int EnumCallback(DevInstance* inst, IntPtr refData)
    {
        var list = (List<DevInstance>)GCHandle.FromIntPtr(refData).Target;
        var copy = Marshal.PtrToStructure<DevInstance>((IntPtr)inst);
        list.Add(copy);
        return 1;   // DIENUM_CONTINUE
    }

    Device Open(DevInstance f)
    {
        IntPtr dev;
        var guid = f.Instance;
        // IDirectInput8::CreateDevice
        if (((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, IntPtr, int>)Vt(di)[3])(di, &guid, &dev, IntPtr.Zero) < 0) return null;
        var v = Vt(dev);
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)v[11])(dev, format);                    // SetDataFormat
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, int>)v[13])(dev, hwnd, 8 | 2);          // SetCooperativeLevel(BACKGROUND|NONEXCLUSIVE)
        var range = new PropRange { Size = sizeof(PropRange), HeaderSize = 16, Obj = 0, How = 0, Min = -32768, Max = 32767 };
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, PropRange*, int>)v[6])(dev, (IntPtr)4, &range); // SetProperty(DIPROP_RANGE)
        var caps = new DevCaps { Size = sizeof(DevCaps) };
        ((delegate* unmanaged[Stdcall]<IntPtr, DevCaps*, int>)v[3])(dev, &caps);                    // GetCapabilities
        var d = new Device
        {
            Instance = f.Instance, Product = f.Product, Name = (f.ProductName ?? f.InstanceName ?? "").Trim(),
            Ptr = dev, Axes = caps.Axes, Buttons = caps.Buttons, Povs = caps.Povs, XInput = IsXInput(dev)
        };
        d.Acquired = ((delegate* unmanaged[Stdcall]<IntPtr, int>)v[7])(dev) >= 0;                  // Acquire
        return d;
    }

    static bool IsXInput(IntPtr dev)
    {
        // DIPROP_GUIDANDPATH (12): {header, guidClass, wszPath[260]}
        const int size = 16 + 16 + 520;
        var buf = stackalloc byte[size];
        *(int*)buf = size; *(int*)(buf + 4) = 16; *(int*)(buf + 8) = 0; *(int*)(buf + 12) = 0;
        if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void*, int>)Vt(dev)[5])(dev, (IntPtr)12, buf) < 0) return false;
        var path = new string((char*)(buf + 32));
        return path.Contains("IG_", StringComparison.OrdinalIgnoreCase);
    }

    public bool Read(Device d, out JoyState s)
    {
        s = default;
        var v = Vt(d.Ptr);
        if (!d.Acquired) d.Acquired = ((delegate* unmanaged[Stdcall]<IntPtr, int>)v[7])(d.Ptr) >= 0;
        if (!d.Acquired) return false;
        ((delegate* unmanaged[Stdcall]<IntPtr, int>)v[25])(d.Ptr);                                  // Poll
        JoyState st;
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, int, JoyState*, int>)v[9])(d.Ptr, sizeof(JoyState), &st);   // GetDeviceState
        if (hr < 0) { d.Acquired = false; return false; }
        s = st;
        return true;
    }

    void Close(Device d)
    {
        if (d.Ptr == IntPtr.Zero) return;
        var v = Vt(d.Ptr);
        ((delegate* unmanaged[Stdcall]<IntPtr, int>)v[8])(d.Ptr);   // Unacquire
        ((delegate* unmanaged[Stdcall]<IntPtr, uint>)v[2])(d.Ptr);  // Release
        d.Ptr = IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var d in Devices) Close(d);
        Devices.Clear();
        if (di != IntPtr.Zero) { ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Vt(di)[2])(di); di = IntPtr.Zero; }
        if (format != IntPtr.Zero) Marshal.FreeHGlobal(format);
    }
}
