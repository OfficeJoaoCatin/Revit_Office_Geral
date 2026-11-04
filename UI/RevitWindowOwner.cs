using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace RevitFamilyImporter.UI
{
    public static class RevitWindowOwner
    {
        public static void Attach(Window window, UIApplication uiApp)
        {
            if (window == null) return;

            IntPtr owner = GetMainWindowHandle(uiApp);
            if (owner == IntPtr.Zero) return;

            try
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            catch { }
        }

        private static IntPtr GetMainWindowHandle(UIApplication uiApp)
        {
            try
            {
                object value = typeof(UIApplication)
                    .GetProperty("MainWindowHandle")
                    ?.GetValue(uiApp);

                if (value is IntPtr handle && handle != IntPtr.Zero)
                    return handle;
            }
            catch { }

            try { return Process.GetCurrentProcess().MainWindowHandle; }
            catch { return IntPtr.Zero; }
        }
    }
}

