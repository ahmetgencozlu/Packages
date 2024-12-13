using System.ComponentModel;
using System.Net;

namespace AppPulse.Model.Response
{
    public class AppPulseResponse
    {

        [DefaultValue("1.0")]
        public string Version => "1.0";

        public HttpStatusCode StatusCode { get; set; }

        public string Status { get; set; }

        public TimeSpan TotalDuration { get; set; }

        public IEnumerable<AppPulseServiceResponse> Services { get; set; }
    }
}
