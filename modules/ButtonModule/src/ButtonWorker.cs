using System;
using System.Device.Gpio;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Extensions.Hosting;

namespace ButtonModule
{
    public class ButtonWorker : BackgroundService
    {
        public ModuleClient ioTHubModuleClient;
        public DateTime LastEvent = DateTime.MinValue;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Init();

            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        public async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            var controller = new GpioController();
            controller.OpenPin(17, PinMode.InputPullUp);
            controller.RegisterCallbackForPinValueChangedEvent(17, PinEventTypes.Rising, buttonPin_ValueChanged);
        }

        private async void buttonPin_ValueChanged(object sender, PinValueChangedEventArgs args)
        {
            if (DateTime.Now.Subtract(LastEvent) < TimeSpan.FromMilliseconds(500))
                return;

            LastEvent = DateTime.Now;
            var messageBytes = Encoding.ASCII.GetBytes($"buttonClicked");
            using (var pipeMessage = new Message(messageBytes))
            {
                await ioTHubModuleClient.SendEventAsync("output1", pipeMessage);

                Console.WriteLine("Received message sent");
            }
        }

    }
}