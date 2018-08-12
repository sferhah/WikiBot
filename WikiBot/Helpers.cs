using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace WikiBot
{
    public static class TaskHelper
    {
        /// <summary>Creates a task that completes after a time delay.</summary>
        /// <param name="seconds">Number of seconds to wait before completing the returned task.</param>
        public static async Task DelayInSecondsAsync(int seconds) => await Task.Delay(seconds * 1000);
    }

    public static class XmlHelper
    {
        /// <summary>This helper function constructs XPathDocument object, makes XPath query and
        /// returns XPathNodeIterator object for selected nodes.</summary>
        /// <param name="xmlSource">Source XML data.</param>
        /// <param name="xpathQuery">XPath query to select specific nodes in XML data.</param>
        /// <param name="xmlNs">XML namespace manager.</param>
        /// <returns>XPathNodeIterator object.</returns>
        public static XPathNodeIterator GetXMLIterator(string xmlSource, string xpathQuery, XmlNamespaceManager xmlNs)
            => new XPathDocument(GetXMLReader(xmlSource)).CreateNavigator().Select(xpathQuery, xmlNs);

        /// <summary>This helper function constructs XmlReader object
        /// using provided XML source code.</summary>
        /// <param name="xmlSource">Source XML data.</param>
        /// <returns>XmlReader object.</returns>
        public static XmlReader GetXMLReader(string xmlSource)
        {
            if (xmlSource.Contains("<!DOCTYPE html>"))
            {
                xmlSource = xmlSource.Replace("<!DOCTYPE html>", "<!DOCTYPE html PUBLIC " +
                    "\"-//W3C//DTD XHTML 1.0 Transitional//EN\" " +
                    "\"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">");
            }

            if (!xmlSource.Contains("<html xmlns="))
            {
                xmlSource = xmlSource.Replace("<html", "<html xmlns=\"http://www.w3.org/1999/xhtml\"");
            }

            XmlReaderSettings settings = new XmlReaderSettings
            {
                XmlResolver = new XmlUrlResolverWithCache(),
                CheckCharacters = false,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true
            };

            // For .NET 4.0 and higher DtdProcessing property should be used instead of ProhibitDtd
            if (settings.GetType().GetProperty("DtdProcessing") != null)
            {
                Type t = typeof(XmlReaderSettings).GetProperty("DtdProcessing").PropertyType;
                settings.GetType().InvokeMember("DtdProcessing",
                    BindingFlags.DeclaredOnly | BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.SetProperty, null, settings,
                    new Object[] { Enum.Parse(t, "2") });    // 2 is a value of DtdProcessing.Parse
            }
            else if (settings.GetType().GetProperty("ProhibitDtd") != null)
            {
                settings.GetType().InvokeMember("ProhibitDtd",
                    BindingFlags.DeclaredOnly | BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.SetProperty,
                    null, settings, new Object[] { false });
            }

            return XmlReader.Create(new StringReader(xmlSource), settings);
        }        
    }

 
}
