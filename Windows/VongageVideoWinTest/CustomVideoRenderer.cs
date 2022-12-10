using OpenTok;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace VongageVideoWinTest
{
    /// <summary>
    /// CustomVideoRenderer
    /// </summary>
    public class CustomVideoRenderer : Control, IVideoRenderer
    {
    #region << Field >>

        private int FrameWidth = -1;
        private int FrameHeight = -1;
        private WriteableBitmap VideoBitmap;

        protected int index = 1;

        protected const string PipeName = "PIPE_APP_SHARE";
        protected MemoryMappedFile _sharedMemory;
        protected MemoryMappedViewAccessor _accessor;

    #endregion << Field >>

        /// <summary>
        /// IsSender
        /// </summary>
        public bool IsSender
        {
            get;
            set;
        } = true;


        /// <summary>
        /// EnableBlueFilter
        /// </summary>
        public bool EnableBlueFilter
        {
            get;
            set;
        }

        /// <summary>
        /// CustomVideoRenderer
        /// </summary>
        static CustomVideoRenderer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomVideoRenderer), new FrameworkPropertyMetadata(typeof(CustomVideoRenderer)));
        }


        /// <summary>
        /// CustomVideoRenderer
        /// </summary>
        public CustomVideoRenderer()
        {
            var brush = new ImageBrush()
            {
                Stretch = Stretch.UniformToFill
            };

            Background = brush;
        }


        /// <summary>
        /// CustomVideoRenderer
        /// </summary>
        ~CustomVideoRenderer()
        {
            _accessor?.Dispose();
            _sharedMemory?.Dispose();
        }


        /// <summary>
        /// RenderFrame
        /// </summary>
        /// <param name="frame"></param>
        public void RenderFrame(VideoFrame frame)
        {

            // WritableBitmap has to be accessed from a STA thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (frame.Width != FrameWidth || frame.Height != FrameHeight)
                    {
                        FrameWidth = frame.Width;
                        FrameHeight = frame.Height;
                        VideoBitmap = new WriteableBitmap(FrameWidth, FrameHeight, 96, 96, PixelFormats.Bgr32, null);

                        if (Background is ImageBrush)
                        {
                            ImageBrush b = (ImageBrush)Background;
                            b.ImageSource = VideoBitmap;
                        }
                        else
                        {
                            throw new Exception("Please use an ImageBrush as background in the SampleVideoRenderer control");
                        }
                    }

                    if (VideoBitmap != null)
                    {
                        VideoBitmap.Lock();
                        {
                            IntPtr[] buffer = { VideoBitmap.BackBuffer };
                            int[] stride = { VideoBitmap.BackBufferStride };
                            frame.ConvertInPlace(OpenTok.PixelFormat.FormatArgb32, buffer, stride);

                            if (EnableBlueFilter)
                            {
                                // This is a very slow filter just for demonstration purposes
                                IntPtr p = VideoBitmap.BackBuffer;
                                for (int y = 0; y < FrameHeight; y++)
                                {
                                    for (int x = 0; x < FrameWidth; x++, p += 4)
                                    {
                                        Marshal.WriteInt32(p, Marshal.ReadInt32(p) & 0xff);
                                    }
                                    p += stride[0] - FrameWidth * 4;
                                }
                            }
                        }
                        VideoBitmap.AddDirtyRect(new Int32Rect(0, 0, FrameWidth, FrameHeight));
                        VideoBitmap.Unlock();

                        if (IsSender)
                        {
                            SendImage(VideoBitmap);
                        }
                    }
                }
                finally
                {
                    frame.Dispose();
                }
            }));

        }


        /// <summary>
        /// SaveImage
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="fileName"></param>
        public void SaveImage(WriteableBitmap bitmap, string fileName)
        {
            try
            {
                using (var stream = new FileStream(fileName,
                                                     FileMode.Create, FileAccess.Write))
                {
                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }
            }
            catch
            {
            }
        }


        /// <summary>
        /// SaveImage
        /// </summary>
        /// <param name="bitmap"></param>
        public void SendImage(WriteableBitmap bitmap)
        {
            try
            {
                var width = bitmap.PixelWidth;
                var height = bitmap.PixelHeight;
                var stride = width * ((bitmap.Format.BitsPerPixel + 7) / 8);

                var bitmapData = new byte[height * stride];

                bitmap.CopyPixels(bitmapData, stride, 0);

                if (_accessor == null)
                {
                    InitMemoryMapped();
                }

                var offset = sizeof(int);
                if (_accessor != null)
                {
                    _accessor.Write(0, bitmapData.Length);
                    _accessor.Write(offset, width);
                    _accessor.Write(offset*2, height);
                    _accessor.WriteArray(offset * 3, bitmapData, 0, bitmapData.Length);
                }
            }
            catch (System.Exception err)
            {
                Trace.WriteLine(err.Message);
            }
        }


        /// <summary>
        /// InitMemoryMapped
        /// </summary>
        protected void InitMemoryMapped()
        {
            _sharedMemory = MemoryMappedFile.CreateNew(PipeName, 1920 * 1080 * 24);
            _accessor = _sharedMemory.CreateViewAccessor();

        }
    }
}
