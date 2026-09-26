using System.Numerics;
using System.Runtime.InteropServices;

namespace FishLegs;

// Reads controllers through Apple's GameController framework (the way SDL does on macOS).
// On recent macOS, Apple's driver owns Xbox/PlayStation/Switch controllers: GLFW (raylib) can see
// them over IOKit HID but receives no input. GameController gets the input and needs no permission.
static class MacGameController
{
    public struct Snapshot
    {
        public string Name, Category;
        public Vector2 LeftStick, RightStick, Dpad;   // +y = up
        public float LeftTrigger, RightTrigger;
        public bool A, B, X, Y, LeftShoulder, RightShoulder, Menu, Options, LeftThumb, RightThumb;
    }

    const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    const string Framework = "/System/Library/Frameworks/GameController.framework/GameController";

    [DllImport(ObjCLib)] static extern IntPtr objc_getClass(string name);
    [DllImport(ObjCLib)] static extern IntPtr sel_registerName(string name);
    [DllImport(ObjCLib)] static extern IntPtr objc_autoreleasePoolPush();
    [DllImport(ObjCLib)] static extern void objc_autoreleasePoolPop(IntPtr pool);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] static extern IntPtr Send(IntPtr obj, IntPtr sel);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] static extern IntPtr SendIndex(IntPtr obj, IntPtr sel, nuint index);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] static extern nuint SendUInt(IntPtr obj, IntPtr sel);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] static extern float SendFloat(IntPtr obj, IntPtr sel);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] static extern bool SendBool(IntPtr obj, IntPtr sel);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] static extern bool SendBoolSel(IntPtr obj, IntPtr sel, IntPtr arg);

    static IntPtr gcClass;
    static readonly Dictionary<string, IntPtr> sels = new();
    static bool initTried;

    public static bool Available { get; private set; }
    public static readonly List<Snapshot> Controllers = new();

    static IntPtr Sel(string name)
    {
        if (!sels.TryGetValue(name, out var s)) sels[name] = s = sel_registerName(name);
        return s;
    }

    static void Init()
    {
        initTried = true;
        if (!OperatingSystem.IsMacOS()) return;
        try
        {
            NativeLibrary.Load(Framework);
            gcClass = objc_getClass("GCController");
            Available = gcClass != IntPtr.Zero;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { Available = false; }
    }

    // Call once per frame on the main thread.
    public static void Poll()
    {
        if (!initTried) Init();
        Controllers.Clear();
        if (!Available) return;

        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            IntPtr list = Send(gcClass, Sel("controllers"));
            nuint count = list == IntPtr.Zero ? 0 : SendUInt(list, Sel("count"));
            for (nuint i = 0; i < count; i++)
            {
                IntPtr ctrl = SendIndex(list, Sel("objectAtIndex:"), i);
                IntPtr pad = Send(ctrl, Sel("extendedGamepad"));
                if (pad == IntPtr.Zero) continue;   // micro/simple gamepads aren't worth supporting here
                Controllers.Add(Read(ctrl, pad));
            }
        }
        finally { objc_autoreleasePoolPop(pool); }
    }

    static Snapshot Read(IntPtr ctrl, IntPtr pad) => new()
    {
        Name = NsString(Send(ctrl, Sel("vendorName"))) ?? "Controller",
        Category = RespondsTo(ctrl, "productCategory") ? NsString(Send(ctrl, Sel("productCategory"))) ?? "" : "",
        LeftStick = Stick(Send(pad, Sel("leftThumbstick"))),
        RightStick = Stick(Send(pad, Sel("rightThumbstick"))),
        Dpad = Stick(Send(pad, Sel("dpad"))),
        LeftTrigger = Value(Send(pad, Sel("leftTrigger"))),
        RightTrigger = Value(Send(pad, Sel("rightTrigger"))),
        A = Pressed(pad, "buttonA"),
        B = Pressed(pad, "buttonB"),
        X = Pressed(pad, "buttonX"),
        Y = Pressed(pad, "buttonY"),
        LeftShoulder = Pressed(pad, "leftShoulder"),
        RightShoulder = Pressed(pad, "rightShoulder"),
        Menu = Pressed(pad, "buttonMenu"),
        Options = Pressed(pad, "buttonOptions"),
        LeftThumb = Pressed(pad, "leftThumbstickButton"),
        RightThumb = Pressed(pad, "rightThumbstickButton"),
    };

    static bool RespondsTo(IntPtr obj, string selector) => SendBoolSel(obj, Sel("respondsToSelector:"), Sel(selector));

    // Optional elements (menu/options/stick clicks) may be missing or nil on some controllers.
    static bool Pressed(IntPtr pad, string element)
    {
        if (!RespondsTo(pad, element)) return false;
        IntPtr button = Send(pad, Sel(element));
        return button != IntPtr.Zero && SendBool(button, Sel("isPressed"));
    }

    static float Value(IntPtr element) => element == IntPtr.Zero ? 0 : SendFloat(element, Sel("value"));

    static Vector2 Stick(IntPtr dirPad) => dirPad == IntPtr.Zero
        ? Vector2.Zero
        : new Vector2(Value(Send(dirPad, Sel("xAxis"))), Value(Send(dirPad, Sel("yAxis"))));

    static string NsString(IntPtr ns) => ns == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(ns, Sel("UTF8String")));
}
