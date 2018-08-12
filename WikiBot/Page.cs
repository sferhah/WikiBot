using System;

namespace WikiBot
{
    /// <summary>Class defines wiki page object.</summary> 
    public class Page
    {

        internal Page() { }

        public PageContent Content { get; internal set; } = new PageContent();

        public string Title => Content.Title;

        public Revision Revision { get; internal set; } = new Revision();

        /// <summary>Page's ID in MediaWiki database.</summary>
        public string PageId { get; set; }

        /// <summary>True, if last edit was minor edit.</summary>
        public bool LastMinorEdit { get; set; }

        /// <summary>Last contributor's ID in MediaWiki database.</summary>
        public string LastUserId { get; set; }

        /// <summary>Time of last page load (UTC). Used to detect edit conflicts.</summary>
        public DateTime LastLoadTime { get; set; }

        /// <summary>True, if this page is in bot account's watchlist.</summary>
        public bool Watched { get; set; }

        public override string ToString() => this.Content.Title;
    }

}
