using OpenTok;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;


namespace VongageVideoWinTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
    #region << Field >>

        public const string API_KEY = "XXXXXXXXXX";
        public const string SESSION_ID = "XXXXXXXXXX";
        public const string TOKEN = "XXXXXXXXXX";

        protected ScreenSharingCapturer _capturer;
        protected Session   _session;
        protected Publisher _publisher;
        protected bool      _disconnect = false;
        protected Dictionary<Stream, Subscriber> _subscriberByStream = new Dictionary<Stream, Subscriber>();
        private Context _context;

    #endregion << Field >>

        /// <summary>
        /// MainWindow
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            if (API_KEY == "" || SESSION_ID == "" || TOKEN == "")
            {
                MessageBox.Show("Please fill out the API_KEY, SESSION_ID and TOKEN variables in the source code " +
                                "in order to connect to the session", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _connectDisconnectButton.IsEnabled = false;
            }
        }


        /// <summary>
        /// Init_Click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Init_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _context = new Context(new WPFDispatcher());

                _capturer = new ScreenSharingCapturer();
                var screenNum = _capturer.GetScreenNum;
                _capturer.AdapterNo = screenNum - 1;

                // We create the publisher here to show the preview when application starts
                // Please note that the PublisherVideo component is added in the xaml file
                _publisher = new Publisher.Builder(_context)
                {
                    Renderer = _publisherVideo,
                    Capturer = _capturer,
                    HasAudioTrack = false
                }.Build();

                // We set the video source type to screen to disable the downscaling of the video
                // in low bandwidth situations, instead the frames per second will drop.
                _publisher.VideoSourceType = VideoSourceType.Screen;

                _session = new Session.Builder(_context, API_KEY, SESSION_ID).Build();

                _session.Connected += Session_Connected;
                _session.Disconnected += Session_Disconnected;
                _session.Error += Session_Error;
                _session.StreamReceived += Session_StreamReceived;
                _session.StreamDropped += Session_StreamDropped;
            }
            catch (System.Exception err)
            {
                Trace.WriteLine(err.Message);
            }
        }


        /// <summary>
        /// Window_Loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var appPath = GetCurrentAppDir();
            appPath = System.IO.Path.Combine(appPath, @"Unity\ShareScreenUnity.exe");

            if (!System.IO.File.Exists(appPath)) 
            {
                return;
            }

            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                _grid.Children.Add(new UnityHost
                {
                    AppPath = appPath
                });
            }
        }


        /// <summary>
        /// MainWindow_Closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var subscriber in _subscriberByStream.Values)
            {
                subscriber.Dispose();
            }

            _publisher?.Dispose();
            _session?.Dispose();
        }


        /// <summary>
        /// UpdateGridSize
        /// </summary>
        /// <param name="numberOfSubscribers"></param>
        private void UpdateGridSize(int numberOfSubscribers)
        {
            int rows = Convert.ToInt32(Math.Round(Math.Sqrt(numberOfSubscribers)));
            int cols = rows == 0 ? 0 : Convert.ToInt32(Math.Ceiling(((double)numberOfSubscribers) / rows));

            _subscriberGrid.Columns = cols;
            _subscriberGrid.Rows = rows;
        }


        /// <summary>
        /// Connect_Click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_disconnect)
                {
                    Trace.WriteLine("Disconnecting session");
                    _session.Unpublish(_publisher);
                    _session.Disconnect();
                }
                else
                {
                    Trace.WriteLine("Connecting session");
                    _session.Connect(TOKEN);
                }
            }
            catch (OpenTokException ex)
            {
                Trace.WriteLine("OpenTokException " + ex.Message);
            }

            _disconnect = !_disconnect;
            _connectDisconnectButton.Content = _disconnect ? "Disconnect" : "Connect";
        }


        /// <summary>
        /// SendVideoVisibility_Click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void SendVideoVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (_publisherVideo == null)
            {
                return;
            }

            _publisherVideo.Visibility = _publisherVideo.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }


        /// <summary>
        /// ReceiveVideoVisibility_Click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void ReceiveVideoVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (_subscriberGrid == null)
            {
                return;
            }

            _subscriberGrid.Visibility = _subscriberGrid.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }


        /// <summary>
        /// _sendToggle_Click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _sendToggle_Click(object sender, RoutedEventArgs e)
        {
            foreach (var subscriber in _subscriberByStream.Values)
            {
                ((CustomVideoRenderer)subscriber.VideoRenderer).IsSender = !((CustomVideoRenderer)subscriber.VideoRenderer).IsSender;
            }
        }

    #region << Session >>

        /// <summary>
        /// Session_Connected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Session_Connected(object sender, EventArgs e)
        {
            try
            {
                _session.Publish(_publisher);
            }
            catch (OpenTokException ex)
            {
                Trace.WriteLine("OpenTokException " + ex.Message);
            }
        }


        /// <summary>
        /// Session_Disconnected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Session_Disconnected(object sender, EventArgs e)
        {
            Trace.WriteLine("Session disconnected");
            _subscriberByStream.Clear();
            _subscriberGrid.Children.Clear();
        }


        /// <summary>
        /// Session_Error
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Session_Error(object sender, Session.ErrorEventArgs e)
        {
            MessageBox.Show("Session error:" + e.ErrorCode, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }


        /// <summary>
        /// Session_StreamReceived
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Session_StreamReceived(object sender, Session.StreamEventArgs e)
        {
            Trace.WriteLine("Session stream received");

            //var renderer = new VideoRenderer();
            var renderer = new CustomVideoRenderer();
            //renderer.EnableBlueFilter = true;

            _subscriberGrid.Children.Add(renderer);

            UpdateGridSize(_subscriberGrid.Children.Count);

            var subscriber = new Subscriber.Builder(_context, e.Stream)
            {
                Renderer = renderer
            }.Build();

            _subscriberByStream.Add(e.Stream, subscriber);

            try
            {
                _session.Subscribe(subscriber);
            }
            catch (OpenTokException ex)
            {
                Trace.WriteLine("OpenTokException " + ex.Message);
            }
        }


        /// <summary>
        /// Session_StreamDropped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Session_StreamDropped(object sender, Session.StreamEventArgs e)
        {
            Trace.WriteLine("Session stream dropped");

            var subscriber = _subscriberByStream[e.Stream];
            if (subscriber != null)
            {
                _subscriberByStream.Remove(e.Stream);

                try
                {
                    _session.Unsubscribe(subscriber);
                }
                catch (OpenTokException ex)
                {
                    Trace.WriteLine("OpenTokException " + ex.Message);
                }

                _subscriberGrid.Children.Remove((UIElement)subscriber.VideoRenderer);
                UpdateGridSize(_subscriberGrid.Children.Count);
            }
        }

    #endregion << Session >>

    #region << Utility >>

        /// <summary>
        /// GetCurrentAppDir
        /// </summary>
        /// <returns></returns>
        public string GetCurrentAppDir()
        {
            return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

    #endregion << Utility >>
    }
}
