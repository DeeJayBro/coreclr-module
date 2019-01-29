using System;
using System.Runtime.InteropServices;

namespace AltV.Net.Native
{
    internal static partial class Alt
    {
        [StructLayout(LayoutKind.Sequential)]
        public sealed class Storage<T>
        {
            uint refCount;
            T value;
        }
    }
}