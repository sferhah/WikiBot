using System;

namespace WikiBot
{
    /// <summary>Class defines wiki Revision object.</summary> 
    public class Revision
    {
        /// <summary>Username or IP-address of last page contributor.</summary>
        public string LastUser { get; set; }

        /// <summary>Page revision ID in the MediaWiki database.</summary>
        public long Id { get; set; }

        /// <summary>Last edit comment.</summary>
        public string Comment { get; set; }

        /// <summary>Date and time of last edit expressed in UTC (Coordinated Universal Time).
        /// Call "timestamp.ToLocalTime()" to convert to local time if it is necessary.</summary>
        public DateTime Timestamp { get; set; }
    }
}
