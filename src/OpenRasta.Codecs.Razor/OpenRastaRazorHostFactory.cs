using System.Diagnostics.CodeAnalysis;
using System.Web.Razor;
using System.Web.WebPages.Razor;

namespace OpenRasta.Codecs.Razor
{
    public class OpenRastaRazorHostFactory
    {
        public static OpenRastaRazorHost CreateHost(string virtualPath, string physicalPath = null)
        {
            return new OpenRastaRazorHost(virtualPath, physicalPath);
        }
    }
}