// DotNetWikiBot Framework 3.15 - designed to make robots for MediaWiki-powered wiki sites
// Requires Microsoft .NET Framework 3.5+ or Mono 1.9+.
// Distributed under the terms of the GNU GPL 2.0 license: http://www.gnu.org/licenses/gpl-2.0.html
// Copyright (c) Iaroslav Vassiliev (2006-2016) codedriller@gmail.com

using System;
using System.Xml;

namespace WikiBot
{

    /// <summary>Class defines custom XML URL resolver, that has a caching capability. See
    /// <see href="http://www.w3.org/blog/systeam/2008/02/08/w3c_s_excessive_dtd_traffic">this page</see>
    /// for details.</summary>
    /// <exclude/>
    class XmlUrlResolverWithCache : XmlUrlResolver
    {
        /// <summary>List of cached files absolute URIs.</summary>
        static readonly string[] cachedFilesURIs = 
        {
            "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd",
            "http://www.w3.org/TR/xhtml1/DTD/xhtml-lat1.ent",
            "http://www.w3.org/TR/xhtml1/DTD/xhtml-symbol.ent",
            "http://www.w3.org/TR/xhtml1/DTD/xhtml-special.ent"
        };
        
        /// <summary>List of cached files names.</summary>
        static readonly string[] cachedFiles =             
        {
            CacheUtils.TransitionalDtdFileName,
            CacheUtils.Lat1EntFileName,
            CacheUtils.SymbolEntFileName,
            CacheUtils.SpecialEntFileName,            
        };

        /// <summary>Overriding GetEntity() function to implement local cache.</summary>
        /// <param name="absoluteUri">Absolute URI of requested entity.</param>
        /// <param name="role">User's role for accessing specified URI.</param>
        /// <param name="ofObjectToReturn">Type of object to return.</param>
        /// <returns>Returns object or requested type.</returns>
        public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            if (absoluteUri.ToString().EndsWith("-/W3C/DTD XHTML 1.0 Transitional/EN"))
            {   
                return CacheUtils.GetCacheStream(CacheUtils.TransitionalDtdFileName);
            }

            if (absoluteUri.ToString().EndsWith("-//W3C//ENTITIES Latin 1 for XHTML//EN"))
            {   
                return CacheUtils.GetCacheStream(CacheUtils.Lat1EntFileName);
            }

            if (absoluteUri.ToString().EndsWith("-//W3C//ENTITIES Symbols for XHTML//EN"))
            {
                return CacheUtils.GetCacheStream(CacheUtils.SymbolEntFileName);
            }

            if (absoluteUri.ToString().EndsWith("-//W3C//ENTITIES Special for XHTML//EN"))
            {   
                return CacheUtils.GetCacheStream(CacheUtils.SpecialEntFileName);
            }

            for (int i = 0; i < XmlUrlResolverWithCache.cachedFilesURIs.Length; i++)
            {
                if (absoluteUri.ToString().EndsWith(XmlUrlResolverWithCache.cachedFiles[i]))
                {
                    return CacheUtils.GetCacheStream(XmlUrlResolverWithCache.cachedFiles[i]);
                }
            }

            return base.GetEntity(absoluteUri, role, ofObjectToReturn);
        }
    }

}