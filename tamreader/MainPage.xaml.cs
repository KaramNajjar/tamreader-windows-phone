using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media.Ocr;
using Windows.UI.Xaml.Navigation;
using Windows.Networking.Sockets;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.Networking.Proximity;
using Windows.Graphics.Display;
using Windows.System.Display;
using Windows.Globalization;
using Windows.Media.Capture;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.Graphics.Imaging;
using Windows.ApplicationModel;
using Windows.Media;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using tamreader.Logic;
using Windows.Networking.Connectivity;

namespace tamreader
{

    public class bluetoothConnectionParams
    {
        public RfcommDeviceService chatService;
        public StreamSocket chatSocket;
        public DataWriter chatWriter;
        public DataReader chatReader;
        public bool isConnectedToBluetooth;
    }


    public sealed partial class MainPage : Page
    {
        public static MainPage Current;
        private const string BT_NAME = "Owais";
        private const int SIGNS_NUMBER = 4;
        public static bluetoothConnectionParams connectionParams;
        private static string receivedString = "";
        private static string lastSpeed = "0";
        private static DispatcherTimer dispatcherTimer;
        private static DispatcherTimer stopTimer;
        private static int speedLimit = 80;
        private static bool startChangingText = false;
        private static int textCounter = 0;
        private static bool isCapturing = false;
        private static bool startShowingImage = false;
        private static int showImgCount = 0;
        // private static bool areMaximized = true;
        private static bool smartMode = false;
        TamreaderClient client;
        //signs data
        private static Sign[] signsData;
        private const string TABLE_PATH = "http://tamreaderapi.azurewebsites.net/signs";

        //*************************************************************************************CAMERA ZONE
        // Language for OCR.
        private Language ocrLanguage = new Language("en");


        private List<string> all_words = new List<string>();

        public enum CURR_SIGN { NONE, STOP, NO_ENTRY, RIGHT, SPEED_LOW, SPEED_HIGH, Left };
        private OcrResult[] res = new OcrResult[10];

        // Receive notifications about rotation of the UI and apply any necessary rotation to the preview stream.     
        private readonly DisplayInformation displayInformation = DisplayInformation.GetForCurrentView();

        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // Prevent the screen from sleeping while the camera is running.
        private readonly DisplayRequest displayRequest = new DisplayRequest();

        // MediaCapture and its state variables.
        private MediaCapture mediaCapture;
        private bool isInitialized = false;
        private bool isPreviewing = false;
        public CURR_SIGN sign = CURR_SIGN.NONE;
        // Information about the camera device.
        private bool mirroringPreview = false;
        private bool externalCamera = false;
        private bool toCall = false;
        int stopCounter = 0;
        private static bool violating; // for counting speed violation

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            Current = this;
            connectionParams = new bluetoothConnectionParams();
            connectionParams.chatReader = null;
            connectionParams.chatService = null;
            connectionParams.chatSocket = null;
            connectionParams.chatWriter = null;
            connectionParams.isConnectedToBluetooth = false;
            //stopImg
            //stopImg.Visibility = Visibility.Collapsed;
            // stopImg.Source = new BitmapImage(new Uri("ms-appx:///speedometer.png", UriKind.Absolute));
            //timer 
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            dispatcherTimer.Start();
            //stop timer
            stopTimer = new DispatcherTimer();
            stopTimer.Tick += StopTimer_Tick;
            stopTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            stopTimer.Stop();
            //client
            client = new TamreaderClient();

            //camera
            // Useful to know when to initialize/clean up the camera
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;




        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            //signs data init
            signsData = new Sign[SIGNS_NUMBER];

            initSigns();

        }

        private async void initSigns()
        {
            try
            {
                if (NetworkInformation.GetInternetConnectionProfile() != null)
                {
                    IEnumerable<Sign> list = await TamreaderClient.GetSignsAsync(TABLE_PATH);

                    int i = 0;
                    foreach (Sign sign in list)
                    {
                        signsData[i++] = new Sign(sign);
                    }
                }
            }
            catch (Exception x)
            {
                string b = x.Message;
            }

        }

        private void StopTimer_Tick(object sender, object e)
        {
            stopCounter++;
            if (stopCounter < 10)
            {

                if (int.Parse(lastSpeed) == 0)
                {
                    //CAR STOPPED
                    signsData[3].ObeyTimes++;
                    successMediaElement.Play();
                    stopCounter = 0;
                    stopImg.Visibility = Visibility.Collapsed;
                    maximizeSpeedView();
                    stopTimer.Stop();
                }


            }
            else
            {

                if (int.Parse(lastSpeed) == 0)
                {
                    //CAR STOPPED
                    signsData[3].ObeyTimes++;
                    successMediaElement.Play();
                    stopCounter = 0;
                    stopImg.Visibility = Visibility.Collapsed;
                    maximizeSpeedView();
                    stopTimer.Stop();


                }
                else
                {
                    //CAR DIDN'T STOP
                    signsData[3].DisobeyTimes++;
                    failMediaElement.Play();
                    stopCounter = 0;
                    stopImg.Visibility = Visibility.Collapsed;
                    maximizeSpeedView();
                    stopTimer.Stop();

                }
            }

        }

        #region Camera methods

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// Ckecks if English language is avaiable for OCR on device and starts camera preview..
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;

            if (!OcrEngine.IsLanguageSupported(ocrLanguage))
            {
                // rootPage.NotifyUser(ocrLanguage.DisplayName + " is not supported.", NotifyType.ErrorMessage);

                return;
            }

            //        await StartCameraAsync();

        }


        /// <summary>
        /// Invoked immediately before the Page is unloaded and is no longer the current source of a parent Frame.
        /// Stops camera if initialized.
        /// </summary>
        /// <param name="e"></param>
        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;

            await CleanupCameraAsync();
        }

        /// <summary>
        /// Occures on app suspending. Stops camera if initialized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active.
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();

                await CleanupCameraAsync();

                displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;

                deferral.Complete();

                UploadSignsData();
            }
        }

        private async void UploadSignsData()
        {
            try
            {
                for (int i = 0; i < SIGNS_NUMBER; i++)
                {
                    await client.CreateSignAsync(signsData[i]);

                }
            }
            catch (Exception)
            {
            }
        }


        /// <summary>
        /// Occures on app resuming. Initializes camera if available.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="o"></param>
        private void Application_Resuming(object sender, object o)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;

                //  await StartCameraAsync();
            }
        }

        /// <summary>
        /// Occures when display orientation changes.
        /// Sets camera rotation preview.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            if (isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }


        /// <summary>
        /// Captures image from camera ,recognizes text and displays it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async Task startCapturing()
        {

            //Get information about the preview.

            var previewProperties = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;


            int videoFrameWidth = (int)previewProperties.Width;
            int videoFrameHeight = (int)previewProperties.Height;

            // In portrait modes, the width and height must be swapped for the VideoFrame to have the correct aspect ratio and avoid letterboxing / black bars.
            if (!externalCamera && (displayInformation.CurrentOrientation == DisplayOrientations.Portrait || displayInformation.CurrentOrientation == DisplayOrientations.PortraitFlipped))
            {
                videoFrameWidth = (int)previewProperties.Height;
                videoFrameHeight = (int)previewProperties.Width;
            }

            for (int i = 0; i < 10; i++)
            {
                // Create the video frame to request a SoftwareBitmap preview frame.
                var videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, videoFrameWidth, videoFrameHeight);

                // Capture the preview frame.

                using (var currentFrame = await mediaCapture.GetPreviewFrameAsync(videoFrame))
                {
                    // Collect the resulting frame.
                    SoftwareBitmap bitmap = currentFrame.SoftwareBitmap;

                    OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(ocrLanguage);

                    if (ocrEngine == null)
                        return;

                    res[i] = await ocrEngine.RecognizeAsync(bitmap);


                }

                // Iterate over recognized lines of text.
                foreach (var line in res[i].Lines)
                {
                    // Iterate over words in line.

                    foreach (var word in line.Words)
                    {
                        //all_words.Add(word.Text);
                        switch (word.Text.ToUpper())
                        {

                            case "STOP":
                            case "TOP":
                            case "STO":
                                sign = CURR_SIGN.STOP;
                                minimizeSpeedView();
                                stopImg.Visibility = Visibility.Visible;
                                stopTimer.Start();
                                i = 1000; //break the capturing
                                break;

                            case "30":
                                speedLimit = 30;
                                sign = CURR_SIGN.SPEED_LOW;
                                i = 1000;//break the capturing
                                break;

                            case "50":
                                speedLimit = 50;
                                sign = CURR_SIGN.SPEED_HIGH;
                                i = 1000;//break the capturing

                                break;

                            case "ENTRY":
                            case "NTR":
                            case "ENT":
                            case "NO":
                            case "NO ENTRY":
                                sign = CURR_SIGN.NO_ENTRY;
                                i = 1000;
                                break;


                            case "LEFT":
                                sign = CURR_SIGN.Left;
                                i = 1000;
                                break;

                            case "RIGHT":
                                sign = CURR_SIGN.RIGHT;
                                i = 1000;
                                break;
                        }




                    }



                }
            }
            isCapturing = false;
        }


        /// <summary>
        /// Starts the camera. Initializes resources and starts preview.
        /// </summary>
        private async Task StartCameraAsync()
        {
            if (!isInitialized)
            {
                await InitializeCameraAsync();
            }

            if (isInitialized)
            {
                all_words.Clear();
            }
        }

        /// <summary>
        /// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, and starts preview.
        /// </summary>
        private async Task InitializeCameraAsync()
        {
            if (mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not.
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

                if (cameraDevice == null)
                {
                    return;
                }

                // Create MediaCapture and its settings.
                mediaCapture = new MediaCapture();

                // Register for a notification when something goes wrong
                mediaCapture.Failed += MediaCapture_Failed;

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                // Initialize MediaCapture
                try
                {
                    await mediaCapture.InitializeAsync(settings);
                    isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    //Denied access to the camera
                }
                catch (Exception)
                {
                    //Exception when init MediaCapture.
                }

                // If initialization succeeded, start the preview.
                if (isInitialized)
                {
                    // Figure out where the camera is located
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                    {
                        // No information on the location of the camera, assume it's an external camera, not integrated on the device.
                        externalCamera = true;
                    }
                    else
                    {
                        // Camera is fixed on the device.
                        externalCamera = false;

                        // Only mirror the preview if the camera is on the front panel.
                        mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }

                    await StartPreviewAsync();
                }
            }
        }

        /// <summary>
        /// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on and unlocks the UI.
        /// </summary>
        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running.
            displayRequest.RequestActive();

            // Set the preview source in the UI and mirror it if necessary.
            PreviewControl.Source = mediaCapture;
            PreviewControl.FlowDirection = mirroringPreview ? Windows.UI.Xaml.FlowDirection.RightToLeft : Windows.UI.Xaml.FlowDirection.LeftToRight;

            // Start the preview.
            try
            {
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (Exception)
            {
                //   rootPage.NotifyUser("Exception starting preview." + ex.Message, NotifyType.ErrorMessage);
            }

            // Initialize the preview to the current orientation.
            if (isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }

        /// <summary>
        /// Gets the current orientation of the UI in relation to the device and applies a corrective rotation to the preview.
        /// </summary>
        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device.
            if (externalCamera) return;

            // Calculate which way and how far to rotate the preview.
            int rotationDegrees;
            VideoRotation sourceRotation;
            CalculatePreviewRotation(out sourceRotation, out rotationDegrees);

            // Set preview rotation in the preview source.
            mediaCapture.SetPreviewRotation(sourceRotation);

            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var props = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, rotationDegrees);
            await mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
        }

        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes, and locks the UI
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            try
            {
                isPreviewing = true;
                await mediaCapture.StopPreviewAsync();
            }
            catch (Exception)
            {
                // Use the dispatcher because this method is sometimes called from non-UI threads.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //   rootPage.NotifyUser("Exception stopping preview. " + ex.Message, NotifyType.ErrorMessage);
                });
            }

            // Use the dispatcher because this method is sometimes called from non-UI threads.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PreviewControl.Source = null;

                // Allow the device to sleep now that the preview is stopped.
                displayRequest.RequestRelease();
            });
        }

        /// <summary>
        /// Cleans up the camera resources (after stopping the preview if necessary) and unregisters from MediaCapture events.
        /// </summary>
        private async Task CleanupCameraAsync()
        {
            if (isInitialized)
            {
                if (isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                isInitialized = false;
            }

            if (mediaCapture != null)
            {
                mediaCapture.Failed -= MediaCapture_Failed;
                mediaCapture.Dispose();
                mediaCapture = null;
            }
        }

        /// <summary>
        /// Queries the available video capture devices to try and find one mounted on the desired panel.
        /// </summary>
        /// <param name="desiredPanel">The panel on the device that the desired camera is mounted on.</param>
        /// <returns>A DeviceInformation instance with a reference to the camera mounted on the desired panel if available,
        ///          any other camera if not, or null if no camera is available.</returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures.
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel.
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found.
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        /// <summary>
        /// Reads the current orientation of the app and calculates the VideoRotation necessary to ensure the preview is rendered in the correct orientation.
        /// </summary>
        /// <param name="sourceRotation">The rotation value to use in MediaCapture.SetPreviewRotation.</param>
        /// <param name="rotationDegrees">The accompanying rotation metadata with which to tag the preview stream.</param>
        private void CalculatePreviewRotation(out VideoRotation sourceRotation, out int rotationDegrees)
        {
            // Note that in some cases, the rotation direction needs to be inverted if the preview is being mirrored.

            switch (displayInformation.CurrentOrientation)
            {
                case DisplayOrientations.Portrait:
                    if (mirroringPreview)
                    {
                        rotationDegrees = 270;
                        sourceRotation = VideoRotation.Clockwise270Degrees;
                    }
                    else
                    {
                        rotationDegrees = 90;
                        sourceRotation = VideoRotation.Clockwise90Degrees;
                    }
                    break;

                case DisplayOrientations.LandscapeFlipped:
                    // No need to invert this rotation, as rotating 180 degrees is the same either way.
                    rotationDegrees = 180;
                    sourceRotation = VideoRotation.Clockwise180Degrees;
                    break;

                case DisplayOrientations.PortraitFlipped:
                    if (mirroringPreview)
                    {
                        rotationDegrees = 90;
                        sourceRotation = VideoRotation.Clockwise90Degrees;
                    }
                    else
                    {
                        rotationDegrees = 270;
                        sourceRotation = VideoRotation.Clockwise270Degrees;
                    }
                    break;

                case DisplayOrientations.Landscape:
                default:
                    rotationDegrees = 0;
                    sourceRotation = VideoRotation.None;
                    break;
            }
        }

        /// <summary>
        /// Handles MediaCapture failures. Cleans up the camera resources.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="errorEventArgs"></param>
        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            await CleanupCameraAsync();

        }
        #endregion Camera methods

        //returns speed in cm/sec
        private string getSpeed(string lastSpeed)
        {
            double speed = int.Parse(lastSpeed);
            speed *= 0.2;
            speed *= Math.PI;

            int speedVal = Convert.ToInt32(speed);

            return speedVal.ToString();
        }

        private async void DispatcherTimer_Tick(object sender, object e)
        {
            try
            {

                string currSpeed = lastSpeed.Substring(0, lastSpeed.Length - 1);
                if (currSpeed != "")
                    currSpeed = getSpeed(currSpeed);
                else
                    currSpeed = "0";

                if (currSpeed.Length == 1)
                {
                    recString.Text = "0" + currSpeed;
                }
                else
                {
                    recString.Text = currSpeed;
                }

                if (int.Parse(currSpeed) < speedLimit)
                {
                    recString.Foreground = new SolidColorBrush(Colors.Green);
                    violating = false;

                }
                else
                {
                    if (violating == false)
                    {
                        violating = true;
                        if (speedLimit == 30)
                        {
                            //SPEED LOW
                            signsData[2].DisobeyTimes++;
                        }
                        else
                        {
                            //SPEED HIGH
                            signsData[1].DisobeyTimes++;

                        }
                    }
                    recString.Foreground = new SolidColorBrush(Colors.Red);
                }
                if (toCall)
                {
                    toCall = false;
                    await CaptureAndGetSign();

                }

                string status = connectBtn.Content.ToString();
                if (startChangingText == false && status != "Device is connected." && status != "Connect" && status != "Trying to connect...")
                    startChangingText = true;

                if (startChangingText)
                {
                    textCounter++;
                    if (textCounter == 40)
                    {
                        textCounter = 0;
                        connectBtn.Content = "Connect";
                        startChangingText = false;
                    }
                }

                if (startShowingImage)
                {
                    showImgCount++;
                    if (showImgCount == 40)
                    {
                        startShowingImage = false;
                        stopImg.Visibility = Visibility.Collapsed;
                        maximizeSpeedView();
                        stopImg.Source = new BitmapImage
                            (new Uri("ms-appx:///images/STOP.png", UriKind.Absolute));
                        showImgCount = 0;

                    }
                }

                if (sign == CURR_SIGN.SPEED_LOW || sign == CURR_SIGN.SPEED_HIGH)
                {
                    startShowingImage = true;
                    string src = "ms-appx:///images/speed30.png";
                    if (sign == CURR_SIGN.SPEED_HIGH)
                    {
                        src = "ms-appx:///images/speed50.png";
                    }
                    stopImg.Source = new BitmapImage
                          (new Uri(src, UriKind.Absolute));

                    minimizeSpeedView();
                    stopImg.Visibility = Visibility.Visible;
                    sign = CURR_SIGN.NONE;
                }

                if (sign == CURR_SIGN.NO_ENTRY)
                {
                    startShowingImage = true;
                    string src = "ms-appx:///images/no_entry.png";
                    stopImg.Source = new BitmapImage
                          (new Uri(src, UriKind.Absolute));
                    stopImg.Visibility = Visibility.Visible;
                    minimizeSpeedView();
                    signsData[0].DisobeyTimes++;//no entry violation
                    failMediaElement.Play();

                    sign = CURR_SIGN.NONE;
                }

                if (sign == CURR_SIGN.RIGHT || sign == CURR_SIGN.Left)
                {
                    startShowingImage = true;
                    string src = "ms-appx:///images/turnRight.png";
                    if (sign == CURR_SIGN.Left)
                        src = "ms-appx:///images/turnLeft.png";
                    stopImg.Source = new BitmapImage
                          (new Uri(src, UriKind.Absolute));
                    stopImg.Visibility = Visibility.Visible;
                    minimizeSpeedView();

                    sign = CURR_SIGN.NONE;

                }
            }
            catch (Exception ex)
            {
                string b = ex.Message;
            }

        }

        //SENDING AND RECEIVING
        public static async void SendBTSignal(string signString)
        {

            connectionParams.chatWriter.WriteString(signString);
            await connectionParams.chatWriter.StoreAsync();
        }
        /*
         When you execute something synchronously, you wait for it to finish before moving on to another task. When you execute something asynchronously
         , you can move on to another task before it finishes.
         */
        private void RecieveBTSignal()
        {
            char ch = '\0';

            while (true)
            {
                try
                {
                    uint sizeFieldCount;
                    IAsyncOperation<uint> taskLoad = connectionParams.chatReader.LoadAsync(1);
                    taskLoad.AsTask().Wait();
                    sizeFieldCount = taskLoad.GetResults();
                    if (sizeFieldCount != 1)
                    {
                        connectionParams.isConnectedToBluetooth = false;
                        return; // the socket was closed before reading.
                    }
                    byte b = connectionParams.chatReader.ReadByte();
                    ch = Convert.ToChar(b);

                    if (ch == '\n')
                    {
                        if (receivedString.Length > 1)
                        {
                            toCall = false;
                            if (receivedString[0] == 'W' && smartMode)
                            {
                                if (!isCapturing)
                                {
                                    isCapturing = true;
                                    toCall = true;
                                }

                            }

                            lastSpeed = receivedString.Substring(1);
                        }
                        receivedString = "";
                    }
                    else
                    {

                        receivedString += ch;
                    }
                }
                catch (Exception ex)
                {
                    string a = ex.Message;
                }
            }
        }

        private async Task CaptureAndGetSign()
        {
            try
            {
                await StartCameraAsync();
                await startCapturing();
            }
            catch (Exception ex)
            {
                string b = ex.Message;
            }
        }
        private async Task connectToBT()
        {
            connectBtn.Content = "Trying to connect...";
            bool deviceFound = false;
            if (connectionParams.isConnectedToBluetooth)
            {
                return;
            }
            PeerFinder.AlternateIdentities["Bluetooth:Paired"] = "";
            var pairedDevices = null as IReadOnlyList<PeerInformation>;
            try
            {
                pairedDevices = await PeerFinder.FindAllPeersAsync();

            }
            catch (Exception)
            {
                connectBtn.Content = "Phone is not connected to bluetooth!";
                return;
            }
            if (pairedDevices.Count == 0)
            {
                connectBtn.Content = "No paired devices found!";
            }
            else
            {
                foreach (var device in pairedDevices)
                {
                    if (device.DisplayName == BT_NAME)
                    {
                        connectionParams.chatSocket = new StreamSocket();
                        try
                        {
                            await connectionParams.chatSocket.ConnectAsync(device.HostName, "1");
                            connectionParams.chatWriter = new DataWriter(connectionParams.chatSocket.OutputStream);
                            connectionParams.chatReader = new DataReader(connectionParams.chatSocket.InputStream);
                            connectBtn.Content = "Device is connected.";
                            deviceFound = true;
                            connectionParams.isConnectedToBluetooth = true;
                            Action a = new Action(RecieveBTSignal);
                            Task t = new Task(a);
                            t.Start();
                        }
                        catch (Exception ex)
                        {
                            connectBtn.Content = ex.Message;
                        }
                    }
                }
                if (connectionParams.chatSocket == null)
                {
                    connectBtn.Content = "Arduino device is Not Paired!";
                }
                if (!deviceFound)
                {
                    connectBtn.Content = "Arduino device is not connected!";
                }
            }
        }
        private void disconnectFromBT()
        {
            connectionParams.chatSocket.Dispose();
            connectionParams.chatReader.DetachStream();
            connectionParams.chatReader.Dispose();
            connectionParams.chatWriter.DetachStream();
            connectionParams.chatWriter.Dispose();
            connectionParams.chatReader = null;
            connectionParams.chatService = null;
            connectionParams.chatSocket = null;
            connectionParams.chatWriter = null;
            connectionParams.isConnectedToBluetooth = false;
            connectBtn.Content = "Not connected";
        }
        private async void button1_Click(object sender, RoutedEventArgs e)
        {
               if (!connectionParams.isConnectedToBluetooth)
                   await connectToBT();
        }
        private void minimizeSpeedView()
        {

            //current speed text block
            Grid.SetRow(textBlock1, 2);
            Thickness margin = textBlock1.Margin;
            margin.Left = 10;
            margin.Top = 21;
            margin.Right = 0;
            margin.Bottom = 0;
            textBlock1.Text = "Current Speed:";
            textBlock1.Margin = margin;
            textBlock1.Height = 28;
            textBlock1.Width = 145;
            textBlock1.FontSize = 20;

            //speed viewer
            Thickness margin1 = recString.Margin;
            margin1.Left = 193;
            margin1.Top = 16;
            margin1.Right = 0;
            margin1.Bottom = 0;
            recString.Margin = margin1;
            Grid.SetRow(recString, 2);
            recString.FontSize = 24;
            recString.Width = 42;
            recString.Height = 39;

            //speedometer picture
            Thickness margin2 = image.Margin;
            margin2.Left = 0;
            margin2.Top = 6;
            margin2.Right = 11;
            margin2.Bottom = 0;
            image.Margin = margin2;
            image.HorizontalAlignment = HorizontalAlignment.Right;
            image.VerticalAlignment = VerticalAlignment.Top;
            image.Height = 60;
            image.Width = 59;
            Grid.SetRow(image, 2);

            //units text
            Thickness margin3 = unitsText.Margin;
            margin3.Left = 220;
            margin3.Top = 27;
            margin3.Right = 0;
            margin3.Bottom = 0;
            unitsText.Margin = margin3;
            Grid.SetRow(unitsText, 2);

        }
        private void maximizeSpeedView()
        {
            //current speed text block
            Grid.SetRow(textBlock1, 1);
            Thickness margin = textBlock1.Margin;
            margin.Left = 76;
            margin.Top = 10;
            margin.Right = 0;
            margin.Bottom = 0;
            textBlock1.Text = "Current Speed";
            textBlock1.Margin = margin;
            textBlock1.FontSize = 30;
            textBlock1.Height = 46;
            textBlock1.Width = 212;

            //speed viewer
            Thickness margin1 = recString.Margin;
            margin1.Left = 116;
            margin1.Top = 77;
            margin1.Right = 0;
            margin1.Bottom = 0;
            recString.Margin = margin1;
            Grid.SetRow(recString, 1);
            recString.FontSize = 100;
            recString.Width = 117;
            recString.Height = 137;

            //speedometer picture
            Thickness margin2 = image.Margin;
            margin2.Left = 0;
            margin2.Top = 214;
            margin2.Right = 97;
            margin2.Bottom = 0;
            image.Margin = margin2;
            image.Height = 148;
            image.Width = 148;
            Grid.SetRow(image, 1);

            //units text
            Thickness margin3 = unitsText.Margin;
            margin3.Left = 225;
            margin3.Top = 178;
            margin3.Right = 0;
            margin3.Bottom = 0;
            unitsText.Margin = margin3;
            Grid.SetRow(unitsText, 1);




        }

        private void toggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (toggleSwitch.IsOn)
            {
                smartMode = true;
            }
            else
            {
                smartMode = false;
            }
        }
    }
}