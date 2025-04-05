using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SimpleVirus
{
    public partial class Form1 : Form
    {
        private static readonly Random random = new Random();
        private static readonly List<string> imageResources = new List<string>
        {
            "SimpleVirus.Resource.1.jpg",
            "SimpleVirus.Resource.2.jpg",
            "SimpleVirus.Resource.3.jpg",
            "SimpleVirus.Resource.4.jpg",
            "SimpleVirus.Resource.5.jpg"
        };


        private const int DisplayWidth = 400;
        private const int DisplayHeight = 400;
        private bool isRunning = false;
        private Thread drawingThread;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        public Form1()
        {
            // Включение DPI-awareness (аналог SetProcessDPIAware() из Python)
            if (Environment.OSVersion.Version.Major >= 6)
                SetProcessDPIAware();

            InitializeComponent();
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Visible = false;
            this.ShowInTaskbar = false;

            // Вывод всех доступных ресурсов для отладки
            Console.WriteLine("Available resources:");
            foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                Console.WriteLine(name);
            }

            isRunning = true;
            drawingThread = new Thread(DrawRandomImageLoop);
            drawingThread.IsBackground = true;
            drawingThread.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isRunning = false;
            drawingThread?.Join(100);
        }

        private Bitmap LoadImageFromResources(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException($"Resource '{resourceName}' not found in embedded resources. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
                return new Bitmap(stream);
            }
        }

        private void DrawRandomImageLoop()
        {
            while (isRunning)
            {
                try
                {
                    string resourceName = imageResources[random.Next(imageResources.Count)];
                    
                    using (Bitmap originalImage = LoadImageFromResources(resourceName))
                    using (Bitmap rgbImage = ConvertToRgb(originalImage))
                    using (Bitmap resizedImage = ResizeImage(rgbImage, DisplayWidth, DisplayHeight))
                    {
                        int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                        int screenHeight = Screen.PrimaryScreen.Bounds.Height;

                        int x = screenWidth <= DisplayWidth ? 0 : random.Next(0, screenWidth - DisplayWidth);
                        int y = screenHeight <= DisplayHeight ? 0 : random.Next(0, screenHeight - DisplayHeight);

                        DrawImageOnDesktopFast(resizedImage, x, y);
                    }

                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }

        private Bitmap ConvertToRgb(Bitmap image)
        {
            Bitmap rgbImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(rgbImage))
            {
                g.DrawImage(image, 0, 0, image.Width, image.Height);
            }
            return rgbImage;
        }

        private Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            Bitmap resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, width, height);
            }
            return resized;
        }

        private void DrawImageOnDesktopFast(Bitmap image, int x, int y)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                using (Bitmap buffer = new Bitmap(DisplayWidth, DisplayHeight, PixelFormat.Format32bppArgb))
                using (Graphics bufferGraphics = Graphics.FromImage(buffer))
                {
                    bufferGraphics.Clear(Color.Transparent);
                    bufferGraphics.DrawImage(image, 0, 0);

                    IntPtr hBitmap = buffer.GetHbitmap();
                    IntPtr hdcMem = CreateCompatibleDC(hdc);
                    SelectObject(hdcMem, hBitmap);

                    BitBlt(hdc, x, y, DisplayWidth, DisplayHeight, hdcMem, 0, 0, SRCCOPY);

                    DeleteDC(hdcMem);
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int SRCCOPY = 0x00CC0020;
    }
}