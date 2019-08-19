using System;
using System.Collections.Generic;

namespace WikiBot
{
    public class WikidataItem
    {
        public string Id { get; set; }
        public List<Tuple<string, string>> Sitelinks { get; set; }
    }
}
