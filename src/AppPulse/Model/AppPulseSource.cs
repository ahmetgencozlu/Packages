using AppPulse.Enum;

namespace AppPulse.Model
{
    public class AppPulseSource
    {
        public string Name { get; set; }
        public DependencyType DependencyType { get; set; }
        public DatabaseInformation DatabaseInformation { get; set; }
        public RabbitMqInformation RabbitMqInformation { get; set; }
        public RedisCacheInformation RedisCacheInformation { get; set; }
        public ExternalServiceInformation ExternalServiceInformation { get; set; }
    }
}
