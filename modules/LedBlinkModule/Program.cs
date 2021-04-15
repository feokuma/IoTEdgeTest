namespace LedBlinkModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using System.Device.Gpio;

    class Program
    {
        static int ledRed = 2;
        static int ledGreen = 3;
        static int ledBlue = 4;

        static GpioController controller = null;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            controller = new GpioController();
            controller.OpenPin(ledRed, PinMode.Output);
            controller.OpenPin(ledGreen, PinMode.Output);
            controller.OpenPin(ledBlue, PinMode.Output);

            Console.WriteLine("Pins opened");

            var thread = new Thread(() => ThreadBody(ioTHubModuleClient));
            thread.Start();
        }

        private static void ThreadBody(object userContext)
        {
            while (true)
            {
                controller.Write(ledRed, PinValue.High);
                controller.Write(ledGreen, PinValue.High);
                controller.Write(ledBlue, PinValue.High);

                Thread.Sleep(1000);

                controller.Write(ledRed, PinValue.Low);
                controller.Write(ledGreen, PinValue.Low);
                controller.Write(ledBlue, PinValue.Low);

                Thread.Sleep(1000);
            }
        }
    }
}
