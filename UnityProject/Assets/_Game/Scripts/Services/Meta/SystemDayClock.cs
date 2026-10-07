using System;
using JungleBooze.Core;

namespace JungleBooze.Services.Meta
{
    /// <summary>The device's local calendar day (GDD 13.3: claimed once per calendar day, local time).</summary>
    public sealed class SystemDayClock : IDayClock
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        public int Today
        {
            get
            {
                double days = (DateTime.Now.Date - Epoch).TotalDays;
                return days < 1.0 ? 1 : (int)days;
            }
        }
    }
}
