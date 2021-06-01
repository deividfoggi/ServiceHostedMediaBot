namespace ServiceHostedMediaBot.Controller
{
    using Microsoft.AspNetCore.Mvc;
    using ServiceHostedMediaBot.Logging;

    public class HomeController : Controller
    {
        private readonly SampleObserver observer;

        public HomeController(SampleObserver observer)
        {
            this.observer = observer;
        }

        public string Get()
        {
            return "Home Page";
        }

        [HttpGet]
        [Route("/logs")]
        public IActionResult GetLogs(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 1000)
        {
            this.AddRefreshHeader(3);
            return this.Content(
                this.observer.GetLogs(skip, take),
                System.Net.Mime.MediaTypeNames.Text.Plain,
                System.Text.Encoding.UTF8);
        }

        [HttpGet]
        [Route("/logs/{filter}")]
        public IActionResult GetLogs(
            string filter,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 1000)
        {
            this.AddRefreshHeader(3);
            return this.Content(
                this.observer.GetLogs(filter, skip, take),
                System.Net.Mime.MediaTypeNames.Text.Plain,
                System.Text.Encoding.UTF8);
        }

        private void AddRefreshHeader(int seconds)
        {
            this.Response.Headers.Add("Cache-Control", "private,must-revalidate,post-check=1,pre-check=2,no-cache");
            this.Response.Headers.Add("Refresh", seconds.ToString());
        }            
    }
}
