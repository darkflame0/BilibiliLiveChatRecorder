using System;
using System.Collections.Generic;
using System.Text;

namespace System.Threading
{
    public static class SemaphoreSlimExtensions
    {
        public static bool TryRelease(this SemaphoreSlim sem)
        {
            try
            {
                sem.Release();
                return true;
            }
            catch (SemaphoreFullException)
            {
                return false;
            }
        }
    }
}
