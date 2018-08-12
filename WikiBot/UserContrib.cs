using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace WikiBot
{
    /// <summary>Class defines wiki UserContrib object.</summary> 
    public class UserContrib
    {
        public int Namespace { get; set; }
        public string Comment { get; set; }
        public string Title { get; set; }

        /// <summary>Date and time of last edit expressed in UTC (Coordinated Universal Time).
        /// Call "timestamp.ToLocalTime()" to convert to local time if it is necessary.</summary>
        public DateTime Timestamp { get; set; }
    }


    /// <summary>Class defines wiki UserContrib object.</summary> 
    public class RecentChange
    {   
        public long Id { get; set; }
        public string User { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public DateTime Timestamp { get; set; }
        public long OldRevid { get; set; }
        public long Revid { get; set; }
    }

    /// <summary>Class defines wiki Compare object.</summary> 
    [JsonObject("compare")]
    public class Compare
    {
        [JsonProperty("*")]
        public string Content { get; set; }

        public IEnumerable<string> Insertions 
            => XDocument.Parse("<root>" + Content + "</root>").Descendants("ins").Select(x => x.Value);

        public IEnumerable<string> Deletions
            => XDocument.Parse("<root>" + Content + "</root>").Descendants("del").Select(x => x.Value);
    }
}
