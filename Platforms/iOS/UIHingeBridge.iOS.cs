#if IOS || MACCATALYST
using System.Runtime.InteropServices;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Foldable.Maui.Platforms.iOS;

/// <summary>
/// Native Objective-C runtime bridge for iOS 27.1+ UIHinge and UIHingeInteraction APIs.
/// </summary>
internal static class UIHingeBridge
{
    private static readonly IntPtr ClassUIHingeInteraction = Class.GetHandle("UIHingeInteraction");
    private static readonly IntPtr ClassUIHinge = Class.GetHandle("UIHinge");

    private static readonly Selector SelAlloc = new("alloc");
    private static readonly Selector SelInitWithUpdateHandler = new("initWithUpdateHandler:");
    private static readonly Selector SelHinge = new("hinge");
    private static readonly Selector SelStatus = new("status");
    private static readonly Selector SelAngle = new("angle");
    private static readonly Selector SelAddInteraction = new("addInteraction:");
    private static readonly Selector SelRemoveInteraction = new("removeInteraction:");

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint nint_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern NFloat NFloat_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    public static bool IsSupported =>
        ClassUIHingeInteraction != IntPtr.Zero &&
        UIDevice.CurrentDevice.CheckSystemVersion(27, 1);

    public delegate void HingeUpdateCallback(int status, double angleInDegrees);

    private unsafe delegate void ObjCBlockHandler(IntPtr block, IntPtr interaction, IntPtr update);

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct ObjCBlockLiteral
    {
        public void* isa;
        public int flags;
        public int reserved;
        public void* invoke;
        public void* descriptor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct ObjCBlockDescriptor
    {
        public ulong reserved;
        public ulong size;
    }

    [DllImport("/usr/lib/system/libsystem_blocks.dylib", EntryPoint = "_NSConcreteGlobalBlock")]
    private static extern IntPtr GetNSConcreteGlobalBlock();

    private static ObjCBlockDescriptor _blockDescriptor = new()
    {
        reserved = 0,
        size = (ulong)Marshal.SizeOf<ObjCBlockLiteral>()
    };

    private static HingeUpdateCallback? _activeCallback;
    private static IntPtr _allocatedBlock;
    private static GCHandle _handlerDelegateHandle;

    public static IntPtr CreateInteraction(HingeUpdateCallback callback)
    {
        if (!IsSupported) return IntPtr.Zero;

        _activeCallback = callback;

        unsafe
        {
            ObjCBlockHandler handler = OnBlockInvoked;
            _handlerDelegateHandle = GCHandle.Alloc(handler);

            _allocatedBlock = Marshal.AllocHGlobal(Marshal.SizeOf<ObjCBlockLiteral>());
            ObjCBlockLiteral* block = (ObjCBlockLiteral*)_allocatedBlock;
            block->isa = (void*)GetNSConcreteGlobalBlock();
            block->flags = 0x50000000; // BLOCK_IS_GLOBAL
            block->reserved = 0;
            block->invoke = (void*)Marshal.GetFunctionPointerForDelegate(handler);
            block->descriptor = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(_blockDescriptor, GCHandleType.Pinned));

            IntPtr allocObj = IntPtr_objc_msgSend(ClassUIHingeInteraction, SelAlloc.Handle);
            IntPtr interaction = IntPtr_objc_msgSend_IntPtr(allocObj, SelInitWithUpdateHandler.Handle, _allocatedBlock);
            return interaction;
        }
    }

    private static void OnBlockInvoked(IntPtr block, IntPtr interaction, IntPtr update)
    {
        if (update == IntPtr.Zero || _activeCallback == null) return;

        try
        {
            IntPtr hingeObj = IntPtr_objc_msgSend(update, SelHinge.Handle);
            if (hingeObj == IntPtr.Zero) return;

            nint status = nint_objc_msgSend(hingeObj, SelStatus.Handle);
            NFloat angleRad = NFloat_objc_msgSend(hingeObj, SelAngle.Handle);
            double angleDeg = (double)angleRad * (180.0 / Math.PI);

            _activeCallback?.Invoke((int)status, angleDeg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UIHingeBridge callback error: {ex.Message}");
        }
    }

    public static void Attach(UIView view, IntPtr interaction)
    {
        if (view == null || interaction == IntPtr.Zero) return;
        void_objc_msgSend_IntPtr(view.Handle, SelAddInteraction.Handle, interaction);
    }

    public static void Detach(UIView view, IntPtr interaction)
    {
        if (view == null || interaction == IntPtr.Zero) return;
        void_objc_msgSend_IntPtr(view.Handle, SelRemoveInteraction.Handle, interaction);
    }
}


#endif
