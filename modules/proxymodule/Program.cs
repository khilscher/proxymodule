namespace proxymodule
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
    using System.Collections.Generic;     // For KeyValuePair<>
    using Microsoft.Azure.Devices.Shared; // For TwinCollection
    using Newtonsoft.Json;                // For JsonConvert
    using System.Diagnostics;

    class Program
    {

        static string forwardAddress { get; set; } = "forward / 1.1.1.1:3129";
        static string configFile = @"/etc/privoxy/config";
        //static Process proc;
        static bool isProxyRunning = false;

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

            // Register callback to be called when a message is received by the module
            //await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);
            // Read the TemperatureThreshold value from the module twin's desired properties
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

        }

        static Task Proxy()
        {

            try
            {

                if(!isProxyRunning)
                {

                    // Start proxy
                    StartProxy();

                }
                else
                {

                    // Restart proxy
                    StopProxy();
                    StartProxy();

                }

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Task.CompletedTask;

        }

        static Task StartProxy()
        {

            Console.WriteLine("Starting proxy service...");

            // Check if Privoxy config file exists
            if(File.Exists(configFile))
            {
                Console.WriteLine("Found Privoxy config file");
            }
            else
            {
                throw new FileNotFoundException();
            }

            // Start the Privoxy process
            ProcessStartInfo startInfo = new ProcessStartInfo() 
                { 
                    FileName = "/usr/sbin/privoxy", 
                    Arguments = $"--no-daemon {configFile}",
                    UseShellExecute = false, 
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }; 

            var proc = new Process() { StartInfo = startInfo, };

            proc.OutputDataReceived += (sender, data) => {
                Console.WriteLine(data.Data);
            };

            proc.ErrorDataReceived += (sender, data) => {
                Console.WriteLine(data.Data);
            };

            try
            {

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                isProxyRunning = true;

                //Console.WriteLine($"Status: {proc.Responding}");

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Task.CompletedTask;

        }

        static Task StopProxy()
        {

            Console.WriteLine("Killing proxy service...");

            // Start the Privoxy process
            ProcessStartInfo startInfo = new ProcessStartInfo() 
                { 
                    FileName = "/usr/bin/killall", 
                    Arguments = $"privoxy",
                    UseShellExecute = false, 
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }; 

            var proc = new Process() { StartInfo = startInfo, };

            proc.OutputDataReceived += (sender, data) => {
                Console.WriteLine(data.Data);
            };

            proc.ErrorDataReceived += (sender, data) => {
                Console.WriteLine(data.Data);
            };

            try
            {

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                isProxyRunning = false;

                //Console.WriteLine($"Status: {proc.Responding}");

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Task.CompletedTask;

        }

        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                // Received update to forwarding address
                if (desiredProperties["Forward"]!=null)
                {
                    
                    // Find and replace the existing forwarding rule
                    string newAddress = desiredProperties["Forward"];
                    string fileText = File.ReadAllText(configFile);
                    fileText = fileText.Replace(forwardAddress, newAddress);
                    File.WriteAllText(configFile, fileText);
                    forwardAddress = desiredProperties["Forward"];
                    Console.WriteLine($"Updated forward address to: {forwardAddress}");

                }

                // Remove forwarding
                if (desiredProperties["Forward"]==null)
                {

                    // Comment out the forwarding rule
                    string newAddress = forwardAddress.Insert(0, "#");
                    string fileText = File.ReadAllText(configFile);
                    fileText = fileText.Replace(forwardAddress, newAddress);
                    File.WriteAllText(configFile, fileText);
                    forwardAddress = newAddress;
                    Console.WriteLine($"Updated forward address to: {forwardAddress}");

                }

                // Start or Restart the proxy service if we receive 
                // config updates via twin updates
                await Proxy();

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            //return Task.CompletedTask;
        }




/*
        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
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
        */
    }
}

