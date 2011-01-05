using OpenRasta.Configuration.Fluent;

namespace OpenRasta.Codecs.Razor
{
    public static class RazorConfigurationExtensions
    {
        /// <summary>
        /// Adds an html rendering of a resource using a Razor view.
        /// </summary>
        public static ICodecDefinition RenderedByRazor(this ICodecParentDefinition codecParentDefinition, string pageVirtualPath)
        {
            return codecParentDefinition.TranscodedBy<RazorCodec>(new { index = pageVirtualPath });
        }

        public static ICodecDefinition RenderedByRazor(this ICodecParentDefinition codecParentDefinition, object viewVirtualPaths)
        {
            return codecParentDefinition.TranscodedBy<RazorCodec>(viewVirtualPaths);
        }
    }
}