using OpenTok;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Threading;


namespace VongageVideoWinTest
{
    /// <summary>
    /// ScreenSharingCapturer
    /// </summary>
    public class ScreenSharingCapturer : IVideoCapturer
    {
    #region << Field >>

        const int FPS = 15;

        protected int _width;
        protected int _height;
        protected Timer _timer;
        protected IVideoFrameConsumer _frameConsumer;

        protected Texture2D _screenTexture;
        protected OutputDuplication _duplicatedOutput;

    #endregion << Field >>

        /// <summary>
        /// AdapterNo
        /// </summary>
        public int AdapterNo
        {
            get;
            set;
        }

        /// <summary>
        /// GetScreenNum
        /// </summary>
        /// <returns></returns>
        public int GetScreenNum
        {
            get => System.Windows.Forms.Screen.AllScreens.Length;
        }



        /// <summary>
        /// ScreenSharingCapturer
        /// </summary>
        public ScreenSharingCapturer()
        {

        }


        /// <summary>
        /// Init
        /// </summary>
        /// <param name="frameConsumer"></param>
        public void Init(IVideoFrameConsumer frameConsumer)
        {
            this._frameConsumer = frameConsumer;
        }


        /// <summary>
        /// Start
        /// </summary>
        public void Start()
        {
            // Change the output number to select a different desktop
            int numOutput = 0;

            var factory = new Factory1();

            var adapter = factory.GetAdapter1(AdapterNo);
            var device = new SharpDX.Direct3D11.Device(adapter);

            var output = adapter.GetOutput(numOutput);
            var output1 = output.QueryInterface<Output1>();

            // When you have a multimonitor setup, the coordinates might be a little bit strange
            // depending on how you've setup the environment.
            // In any case Right - Left should give the width, and Bottom - Top the height.
            var desktopBounds = output.Description.DesktopBounds;
            _width = desktopBounds.Right - desktopBounds.Left;
            _height = desktopBounds.Bottom - desktopBounds.Top;

            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags   = BindFlags.None,
                Format      = Format.B8G8R8A8_UNorm,
                Width       = _width,
                Height      = _height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels   = 1,
                ArraySize   = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage       = ResourceUsage.Staging
            };

            _screenTexture = new Texture2D(device, textureDesc);
            _duplicatedOutput = output1.DuplicateOutput(device);

            _timer = new Timer((Object stateInfo) =>
            {
                try
                {
                    SharpDX.DXGI.Resource screenResource;
                    OutputDuplicateFrameInformation duplicateFrameInformation;

                    _duplicatedOutput.AcquireNextFrame(1000 / FPS, out duplicateFrameInformation, out screenResource);

                    using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                        device.ImmediateContext.CopyResource(screenTexture2D, _screenTexture);

                    screenResource.Dispose();
                    _duplicatedOutput.ReleaseFrame();

                    var mapSource = device.ImmediateContext.MapSubresource(_screenTexture, 0, MapMode.Read,
                                                                           SharpDX.Direct3D11.MapFlags.None);

                    IntPtr[] planes = { mapSource.DataPointer };
                    int[] strides = { mapSource.RowPitch };

                    using (var frame = VideoFrame.CreateYuv420pFrameFromBuffer(PixelFormat.FormatArgb32, _width, _height,
                                                                               planes, strides))
                    {
                        _frameConsumer.Consume(frame);
                    }

                    device.ImmediateContext.UnmapSubresource(_screenTexture, 0);

                }
                catch (SharpDXException)
                {
                }
            }, null, 0, 1000 / FPS);

            output1.Dispose();
            output.Dispose();
            adapter.Dispose();
            factory.Dispose();
        }


        /// <summary>
        /// Stop
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                using (var timerDisposed = new ManualResetEvent(false))
                {
                    _timer.Dispose(timerDisposed);
                    timerDisposed.WaitOne();
                }
            }
            _timer = null;
        }


        /// <summary>
        /// Destroy
        /// </summary>
        public void Destroy()
        {
            _duplicatedOutput?.Dispose();
            _screenTexture?.Dispose();
        }


        /// <summary>
        /// SetVideoContentHint
        /// </summary>
        /// <param name="contentHint"></param>
        public void SetVideoContentHint(VideoContentHint contentHint)
        {
            if (_frameConsumer == null)
                throw new InvalidOperationException("Content hint can only be set after constructing the " +
                    "Publisher and Capturer.");

            _frameConsumer.SetVideoContentHint(contentHint);
        }


        /// <summary>
        /// GetVideoContentHint
        /// </summary>
        /// <returns></returns>
        public VideoContentHint GetVideoContentHint()
        {
            if (_frameConsumer != null)
                return _frameConsumer.GetVideoContentHint();

            return VideoContentHint.NONE;
        }


        /// <summary>
        /// GetCaptureSettings
        /// </summary>
        /// <returns></returns>
        public VideoCaptureSettings GetCaptureSettings()
        {
            var settings = new VideoCaptureSettings()
            {
                Width   = _width,
                Height  = _height,
                Fps     = FPS,
                MirrorOnLocalRender = false,
                PixelFormat = PixelFormat.FormatYuv420p,
            };

            return settings;
        }

    }
}
