using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;

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

        static List<string> ingridents = new List<string>() { "Lettuce", "Tomato"};
        static Dictionary<string, Vector3> locations = new Dictionary<string, Vector3>();

        public static async Task GetCommandsAsync()
        {

            renderer.pullPoint = true;
            Console.WriteLine("Give Me a Command(all, ingredient):");
            var command = Console.ReadLine();

            await SynthesisToSpeakerAsync("Welcome to auto setup for Panera light control");
            await SynthesisToSpeakerAsync("Please wait for ingrident to be spoken then hit enter to continue when right hand is in place");

            foreach (var item in ingridents)
            {
                await SynthesisToSpeakerAsync(item);
                // Wait
                Console.WriteLine("Hit enter once hand in place");
                Console.ReadKey();
                // Read setting
                System.Numerics.Vector3 thePoint = renderer.thePoint;
                locations.Add(item, thePoint);
                Console.WriteLine($"Right Hand Found X:{thePoint.X} Y:{thePoint.Y} Z:{thePoint.Z} ");
            }


        }

        public static async Task LoopDetectorAsync()
        {
            DateTime lastEvent = DateTime.Now;
            int offsetVal = 200;

            while (renderer.IsActive)
            {
                if (DateTime.Now.Subtract(lastEvent).TotalMilliseconds > 250)
                {
                    lastEvent = DateTime.Now;
                    System.Numerics.Vector3 thePoint = renderer.thePoint;

                    var first = locations.FirstOrDefault(x => ((x.Value.X - offsetVal < thePoint.X && thePoint.X < x.Value.X + offsetVal) &&
                    (x.Value.Y - offsetVal < thePoint.Y && thePoint.Y < x.Value.Y + offsetVal) &&
                    (x.Value.Z - offsetVal < thePoint.Z && thePoint.Z < x.Value.Z + offsetVal)));

                    if (!string.IsNullOrEmpty(first.Key))
                    {
                        Console.WriteLine("Found " + first.Key);
                    }

                }
                
            }
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