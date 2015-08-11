using System;
using System.Diagnostics;
using ServiceStack.Text;
using Funq;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;

namespace SelfHost1
{
    [Route("/process-http-request")]
    public class BasicRequest : IReturn<bool>
    {
    }

    [Route("/process-queued-request")]
    public class QueueBasicRequest : IReturn<bool>
    {
    }

    public interface IDependency
    {
        Guid GetInstanceId();
    }

    public class SomeDependency : IDependency, IDisposable
    {
        private readonly Guid _id = Guid.NewGuid();

        public Guid GetInstanceId()
        {
            return _id;
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing: " + _id);
        }
    }

    public class MessageConsumingService : Service
    {
        private readonly IDependency _dependency;

        public MessageConsumingService(IDependency dependency)
        {
            _dependency = dependency;
        }

        public bool Any(BasicRequest request)
        {
            Console.WriteLine(_dependency.GetInstanceId());
            return true;
        }
    }

    public class MessagePublishingService : Service
    {
        private readonly IMessageService _messageService;

        public MessagePublishingService(IMessageService messageService)
        {
            _messageService = messageService;
        }

        public bool Any(QueueBasicRequest request)
        {
            using (var mqClient = _messageService.CreateMessageQueueClient())
            {
                mqClient.Publish(new BasicRequest());
            }
            return true;
        }
    }

    public class AppHost : AppSelfHostBase
    {
        public AppHost() : base("SelfHost1", typeof(AppHost).Assembly)
        {}

        public override void Configure(Container container)
        {
            container.Register<IDependency>(c => new SomeDependency()).ReusedWithin(ReuseScope.Request);
            container.Register<IMessageService>(c => new RabbitMqServer());
            var mqServer = container.Resolve<IMessageService>();
            mqServer.RegisterHandler<BasicRequest>(ServiceController.ExecuteMessage);
            mqServer.Start();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            new AppHost().Init().Start("http://*:8088/");
            "ServiceStack SelfHost listening at http://localhost:8088 ".Print();
            Process.Start("http://localhost:8088/");
            Console.ReadLine();
        }
    }
}
