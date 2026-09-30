using WebExpress.WebCore;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Core
{
    [Name("kleenestar.core:plugin.name")]
    [Description("kleenestar.core:plugin.description")]
    [Icon("/assets/img/kleenestar.svg")]
    [Application<KleeneStarApplication>()]
    [Dependency("webexpress.webapp")]
    public sealed class KleeneStarPlugin : IPlugin
    {
        /// <summary>  
        /// Initializes a new instance of the class.  
        /// </summary>  
        public KleeneStarPlugin()
        {
            WebEx.Favicon = "/assets/img/kleenestar.svg";
        }

        /// <summary>  
        /// Called when the plugin starts working. Run is called concurrently.  
        /// </summary>  
        public void Run()
        {
        }

        /// <summary>
        /// Called when the host shuts down. The plugin holds no resources of its own.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
