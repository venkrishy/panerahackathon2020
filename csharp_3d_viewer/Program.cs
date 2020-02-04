using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using uPLibrary.Networking.M2Mqtt;

namespace Csharp_3d_viewer
{
    class Program
    {
        private static Renderer renderer;
        static void Main()
        {
            var videoTask = RunVideo();

            Task.Delay(2000).Wait();
            GetCommandsAsync().Wait();

            LoopDetectorAsync().Wait();
            videoTask.Wait();

        }

        private static void SaveSettings()
        {
            string json = JsonConvert.SerializeObject(locations);
            System.IO.File.WriteAllText(@"layout.json", json);
        }

        private static void LoadSettings()
        {
            string text = System.IO.File.ReadAllText(@"layout.json");

            locations = JsonConvert.DeserializeObject<Dictionary<string, Model.ItemLocation>>(text);

        }

        private static Task RunVideo()
        {
            return Task.Run(() =>
            {
                using (var visualizerData = new VisualizerData())
                {
                    renderer = new Renderer(visualizerData);

                    renderer.StartVisualizationThread();

                    // Open device.
                    using (Device device = Device.Open())
                    {
                        device.StartCameras(new DeviceConfiguration()
                        {
                            CameraFPS = FPS.FPS30,
                            ColorResolution = ColorResolution.Off,
                            DepthMode = DepthMode.NFOV_Unbinned,
                            WiredSyncMode = WiredSyncMode.Standalone,
                        });

                        var deviceCalibration = device.GetCalibration();
                        PointCloud.ComputePointCloudCache(deviceCalibration);

                        using (Tracker tracker = Tracker.Create(deviceCalibration, new TrackerConfiguration() { ProcessingMode = TrackerProcessingMode.Gpu, SensorOrientation = SensorOrientation.Default }))
                        {
                            while (renderer.IsActive)
                            {
                                using (Capture sensorCapture = device.GetCapture())
                                {
                                    // Queue latest frame from the sensor.
                                    tracker.EnqueueCapture(sensorCapture);
                                }

                                // Try getting latest tracker frame.
                                using (Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false))
                                {
                                    if (frame != null)
                                    {
                                        // Save this frame for visualization in Renderer.

                                        // One can access frame data here and extract e.g. tracked bodies from it for the needed purpose.
                                        // Instead, for simplicity, we transfer the frame object to the rendering background thread.
                                        // This example shows that frame popped from tracker should be disposed. Since here it is used
                                        // in a different thread, we use Reference method to prolong the lifetime of the frame object.
                                        // For reference on how to read frame data, please take a look at Renderer.NativeWindow_Render().

                                        visualizerData.Frame = frame.Reference();
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        static List<string> ingredients = new List<string>() { "turkey", "sliced bacon", "chopped bacon", "signature sauce", "emerald greens", "tomato", "salt and pepper", "avocado", "gouda cheese", "pickle spear", "spicy mustard", "mayo", "gorgonzola cheese", "american cheese", "red onion" };

        //static List<string> ingredients = new List<string>() { "tomato","turkey", "sliced bacon", "chopped bacon" };

        static Dictionary<string, Model.ItemLocation> locations = new Dictionary<string, Model.ItemLocation>();


        static Model.ItemHit handIn = null;

        public static async Task GetCommandsAsync()
        {
            try
            {
                LoadSettings();
            }
            catch (Exception)
            {
            }

            renderer.pullPoint = true;
            //await SynthesisToSpeakerAsync("Welcome to auto setup for Panera light control");
            Console.Write("Give Me a Command(setup, setup short, run, exit):");
            var command = Console.ReadLine();

            while (command != "exit" && command != "run")
            {

                if (locations.ContainsKey(command))
                {
                        var itemIng = locations[command];

                        // Wait
                        Console.WriteLine("Hit enter once hand in place");
                        Console.ReadKey();
                        // Read setting
                        System.Numerics.Vector3 thePoint = renderer.thePoint;
                        Model.ItemLocation theItem;
                        if (locations.ContainsKey(command))
                        {
                            theItem = locations[command];
                            theItem.Location = thePoint;
                        }
                        else
                        {
                            locations.Add(command, new Model.ItemLocation() { Location = thePoint, Item = command });
                        }
                        Console.WriteLine($"Right Hand Found X:{thePoint.X} Y:{thePoint.Y} Z:{thePoint.Z} ");
                    

                    SaveSettings();
                }
                else
                {
                    switch (command)
                    {
                        case "setup":
                            locations.Clear();
                            await SynthesisToSpeakerAsync("Please wait for ingredient to be spoken then hit enter to continue when right hand is in place");

                            foreach (var item in ingredients)
                            {
                                //await SynthesisToSpeakerAsync(item);
                                // Wait
                                Console.WriteLine(item);
                                Console.WriteLine("Hit enter once hand in place");
                                Console.ReadKey();
                                // Read setting
                                System.Numerics.Vector3 thePoint = renderer.thePoint;
                                locations.Add(item, new Model.ItemLocation() { Location = thePoint, Item = item });
                                Console.WriteLine($"Right Hand Found X:{thePoint.X} Y:{thePoint.Y} Z:{thePoint.Z} ");
                            }

                            SaveSettings();
                            break;
                        case "setup short":
                            await SynthesisToSpeakerAsync("Please wait for ingredient to be spoken then hit enter to continue when right hand is in place");

                            for (int i = 0; i < 3; i++)
                            {
                                var item = ingredients[i];
                                await SynthesisToSpeakerAsync(item);
                                // Wait
                                Console.WriteLine("Hit enter once hand in place");
                                Console.ReadKey();
                                // Read setting
                                System.Numerics.Vector3 thePoint = renderer.thePoint;
                                Model.ItemLocation theItem;
                                if (locations.ContainsKey(item))
                                {
                                    theItem = locations[item];
                                    theItem.Location = thePoint;
                                }
                                else
                                {
                                    locations.Add(item, new Model.ItemLocation() { Location = thePoint, Item = item });
                                }
                                Console.WriteLine($"Right Hand Found X:{thePoint.X} Y:{thePoint.Y} Z:{thePoint.Z} ");
                            }

                            SaveSettings();
                            break;
                    }
                }

                Console.Write("Next Command:");
                command = Console.ReadLine();

            }

        }

        public static async Task LoopDetectorAsync()
        {
            DateTime lastEvent = DateTime.Now;
            int zoffsetVal = 200;
            int xoffsetVal = 100;

            while (renderer.IsActive)
            {
                if (DateTime.Now.Subtract(lastEvent).TotalMilliseconds > 40)
                {
                    lastEvent = DateTime.Now;
                    System.Numerics.Vector3 thePoint = renderer.thePoint;

                   // Console.WriteLine($"Right Hand Found X:{thePoint.X} Y:{thePoint.Y} Z:{thePoint.Z} ");

                    var first = locations.Where(x => ((x.Value.Location.X - xoffsetVal < thePoint.X && thePoint.X < x.Value.Location.X + xoffsetVal) &&
                    (x.Value.Location.Y - 10 < thePoint.Y && thePoint.Y < x.Value.Location.Y + 400) &&
                    (x.Value.Location.Z - zoffsetVal < thePoint.Z && thePoint.Z < x.Value.Location.Z + zoffsetVal))).OrderBy(x=> Math.Abs(Vector3.Distance(thePoint,x.Value.Location))).FirstOrDefault();


                    if (string.IsNullOrEmpty(first.Key))
                    {
                        if (handIn != null)
                        {
                            await SendIngredient();

                            //todo: add mqtt
                        }
                        handIn = null;
                    }
                    else
                    {
                        // something has been hit
                        if (handIn == null || handIn.Item != first.Key)
                        {
                            if(handIn != null && handIn.Item != first.Key)
                            {
                                await SendIngredient();

                            }
                            Console.WriteLine($"Found {first.Key} Hand In");
                            handIn = new Model.ItemHit() { Item = first.Key, HitTime = DateTime.Now };
                        }

                    }

                }

            }
        }

        private static async Task SendIngredient()
        {
            double timeGap = DateTime.Now.Subtract(handIn.HitTime).TotalMilliseconds;

            Console.WriteLine($"Time Gap {handIn.Item}:  {timeGap} ");
            if (timeGap > 150)
            {
                // todo: control with time
                Console.WriteLine($"!!EVENT {handIn.Item} Hand Out");
                Product product = new Product();
                product.ingredient = handIn.Item;
                await InvokeMQTT(product);
            }
        }

        public static async Task InvokeMQTT(Product product)
        {
            string connectionString = "mqtt.panerahackathon.com";

            var client = new MqttClient(connectionString);
            client.Connect("");
            string Message = JsonConvert.SerializeObject(product);
            // Publish a message to topic
            client.Publish("ingredient/pulled", System.Text.Encoding.UTF8.GetBytes(Message));
        }

        public static async Task SynthesisToSpeakerAsync(string text)
        {
            // Creates an instance of a speech config with specified subscription key and service region.
            // Replace with your own subscription key and service region (e.g., "westus").
            // The default language is "en-us".
            var config = SpeechConfig.FromSubscription("1165569e3b7b469c9b3a78a5ab2cee3f", "southcentralus");

            // Creates a speech synthesizer using the default speaker as audio output.
            using (var synthesizer = new SpeechSynthesizer(config))
            {
                //// Receive a text from console input and synthesize it to speaker.
                //Console.WriteLine("Type some text that you want to speak...");
                //Console.Write("> ");
                //string text = Console.ReadLine();

                using (var result = await synthesizer.SpeakTextAsync(text))
                {
                    if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        Console.WriteLine($"Speech synthesized to speaker for text [{text}]");
                    }
                    else if (result.Reason == ResultReason.Canceled)
                    {
                        var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                        Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                        if (cancellation.Reason == CancellationReason.Error)
                        {
                            Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                            Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                            Console.WriteLine($"CANCELED: Did you update the subscription info?");
                        }
                    }
                }

            }
        }
    }
}