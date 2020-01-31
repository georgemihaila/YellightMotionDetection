using AForge.Video.DirectShow;
using MotionDetection.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YeelightAPI;

namespace MotionDetection
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string _bindingsFilename = "bindings.json";

        private readonly CameraConfiguration _configuration;

        private IEnumerable<Device> _lights;

        private Dictionary<string, DateTime> _cameraCooloffs = new Dictionary<string, DateTime>();

        public MainWindow()
        {
            InitializeComponent();
            if (File.Exists(_bindingsFilename))
            {
                try
                {
                    _configuration = JsonConvert.DeserializeObject<CameraConfiguration>(File.ReadAllText(_bindingsFilename));
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }

            InitAsync();
        }

        private async static Task<bool> EnsureDeviceConnectedAsync(Device device, int retries = 3)
        {
            if (device.IsConnected)
                return true;
            int c = 0;
            do
            {
                try
                {
                    var result = await device.Connect();
                    if (result)
                        return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            while (c++ < retries);
            return false;
        }

        private async void InitAsync()
        {
            //Lights
            Console.WriteLine("Searching for Yeelight devices...");
            _lights = await DeviceLocator.Discover();
            Console.WriteLine($"Found {_lights.Count()} device{(_lights.Count() != 1 ? "s" : string.Empty)}: {string.Join(", ", _lights.Select(x => x.Id))}");

            //Cameras
            var videoDevicesInfo = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            LayoutRoot.Columns = videoDevicesInfo.Count;
            LayoutRoot.Rows = 1;
            foreach (FilterInfo deviceInfo in videoDevicesInfo)
            {
                var device = new VideoCaptureDevice(deviceInfo.MonikerString);
                var hash = SHA256(deviceInfo.MonikerString);
                var view = new MotionView(device, hash);
                Console.WriteLine($"Initialized camera {hash}");
                view.MotionDetected += async (sender, args) =>
                {
                    var view = sender as MotionView;
                    /*var message = $"{view.DeviceID.Substring(0, 4)}: {args.Direction.X}, {args.Direction.Y}";
                    Console.WriteLine(message);*/
                    await HandleCameraMovementAsync(view.DeviceID, args.Direction.X, args.Direction.Y);
                };
                LayoutRoot.Children.Insert(0, view);
            }
            //Action
        }

        private async Task HandleCameraMovementAsync(string cameraID, params MovementDirection[] directions)
        {
            Func<CameraBinding, bool> cameraIDPredicate = x => x.CameraID == cameraID;
            if (_configuration.CameraBindings.Any(cameraIDPredicate))
            {
                if (_cameraCooloffs.ContainsKey(cameraID))
                {
                    if (DateTime.Now.Subtract(_cameraCooloffs[cameraID]).TotalMilliseconds < _configuration.Cooloff)
                        return;
                }

                var binding = _configuration.CameraBindings.First(cameraIDPredicate);
                foreach (var direction in directions)
                {
                    Func<CameraDirectionLightStateBinding, bool> directionPredicate = x => x.Direction == direction;
                    if (binding.Bindings.Any(directionPredicate))
                    {
                        var actions = binding.Bindings.First(directionPredicate).Actions;
                        IEnumerable<(bool On, string LightID)> mergedTargetLights = actions.On.Select(x => (true, x)).Union(actions.Off.Select(x => (false, x)));
                        var tasks = mergedTargetLights.Select(target => Task.Run(async () =>
                        {
                            Func<Device, bool> lightPredicate = x => x.Id == target.LightID;
                            if (_lights.Any(lightPredicate))
                            {
                                var light = _lights.First(lightPredicate);
                                var connected = await EnsureDeviceConnectedAsync(light);
                                if (connected)
                                {
                                    if (target.On)
                                    {
                                        await light.TurnOn();
                                    }
                                    else
                                    {
                                        await light.TurnOff();
                                    }
                                }
                            }
                        }));
                        await Task.WhenAll(tasks);
                        _cameraCooloffs[cameraID] = DateTime.Now;
                    }
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Console.WriteLine("Closing...");
            foreach(var x in _lights)
            {
                if (x.IsConnected)
                {
                    x.Disconnect();
                }
            }
            Process.GetCurrentProcess().Kill(); //Force terminate the app because AForge makes the process hang
        }

        private static string SHA256(string value)
        {
            var crypt = new SHA256Managed();
            string hash = String.Empty;
            byte[] crypto = crypt.ComputeHash(Encoding.ASCII.GetBytes(value));
            foreach (byte theByte in crypto)
            {
                hash += theByte.ToString("x2");
            }
            return hash;
        }
    
        
    }
}
