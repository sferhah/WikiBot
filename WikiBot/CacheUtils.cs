using System.IO;
using System.Reflection;

namespace WikiBot
{
    public class CacheUtils
    {
        //These files have not been changed since 2013, NOTE : CommonData should be updated for new wikis
        public static readonly string CommonDataXmlFileName = "CommonData.xml";
        public static readonly string TransitionalDtdFileName = "xhtml1-transitional.dtd";
        public static readonly string Lat1EntFileName = "xhtml-lat1.ent";
        public static readonly string SpecialEntFileName = "xhtml-special.ent";
        public static readonly string SymbolEntFileName = "xhtml-symbol.ent";
      

        public static Stream GetCacheStream(string cacheFile)
        {
            var assembly = typeof(CacheUtils).GetTypeInfo().Assembly;
            var assembly_namespace = typeof(CacheUtils).Assembly.GetName().Name;

            return assembly.GetManifestResourceStream(assembly_namespace + ".Cache." + cacheFile);
        }
    }

}
