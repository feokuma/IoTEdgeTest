using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Loader;
using System.Text;
using System.Device.Gpio;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;

namespace WorkerModule
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private int counter;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Init();
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            await WhenCancelled(cts.Token);
        }

        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        private async Task Init()
        {
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            var ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            var thread = new Thread(() => ThreadBody(ioTHubModuleClient));
            thread.Start();
        }

        private void ThreadBody(object userContext)
        {
            int ledRed = 1;
            int ledGreen = 2;
            int ledBlue = 3;
            int button = 17;

            using (var controller = new GpioController())
            {
                controller.OpenPin(ledRed, PinMode.Output);
                controller.OpenPin(ledGreen, PinMode.Output);
                controller.OpenPin(ledBlue, PinMode.Output);
                controller.OpenPin(button, PinMode.Input);

                Console.WriteLine("Pins opened");

                while (true)
                {

                    // if (controller.Read(button) == PinValue.High)
                    // {
                    controller.Write(ledBlue, PinValue.High);
                    controller.Write(ledRed, PinValue.High);
                    controller.Write(ledGreen, PinValue.High);
                    // }

                    Thread.Sleep(300);
                    // else
                    // {
                    controller.Write(ledBlue, PinValue.Low);
                    controller.Write(ledRed, PinValue.Low);
                    controller.Write(ledGreen, PinValue.Low);
                    // }
                    Thread.Sleep(1000);
                }
            }
        }

        private async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                using (var pipeMessage = new Message(messageBytes))
                {
                    foreach (var prop in message.Properties)
                    {
                        pipeMessage.Properties.Add(prop.Key, prop.Value);
                    }
                    await moduleClient.SendEventAsync("output1", pipeMessage);

                    Console.WriteLine("Received message sent");
                }
            }
            return MessageResponse.Completed;
        }
    }
}
