using Microsoft.Extensions.Logging;

namespace LayoutBrowser
{
    public class LayoutManager
    {
        private readonly ILogger logger;

        public LayoutManager(ILogger logger)
        {
            this.logger = logger;
        }
    }
}