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
        private bool IsActive = false;

        private GpioController controller;
        private const int ledRed = 2;
        private const int ledGreen = 3;
        private const int ledBlue = 4;

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

            var ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            controller = new GpioController();
            controller.OpenPin(ledRed, PinMode.Output);
            controller.OpenPin(ledGreen, PinMode.Output);
            controller.OpenPin(ledBlue, PinMode.Output);
            Console.WriteLine("GPIO Ready");

            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);
        }
        private async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);

            if (!string.IsNullOrEmpty(messageString) && messageString == "buttonClicked")
            {
                IsActive = !IsActive;
                LedState(IsActive);
            }

            return await Task.FromResult(MessageResponse.Completed);
        }

        private void LedState(bool active)
        {
            if (active)
            {
                controller.Write(ledBlue, PinValue.High);
                controller.Write(ledRed, PinValue.High);
                controller.Write(ledGreen, PinValue.High);
            }
            else
            {
                controller.Write(ledBlue, PinValue.Low);
                controller.Write(ledRed, PinValue.Low);
                controller.Write(ledGreen, PinValue.Low);
            }

            Console.WriteLine($"Led actived: {active}");
        }
    }
}
