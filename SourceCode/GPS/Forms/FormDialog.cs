using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Properties;

namespace AgOpenGPS.Forms
{
    public enum DialogSeverity
    {
        Error,
        Warning,
        Info
    }

    public partial class FormDialog : Form
    {
        private static readonly Color ErrorBorderColor = Color.FromArgb(192, 0, 0);
        private static readonly Color ErrorBackgroundColor = Color.FromArgb(255, 192, 192);
        private static readonly Color WarningBorderColor = Color.FromArgb(192, 145, 0);
        private static readonly Color WarningBackgroundColor = Color.FromArgb(227, 217, 152);
        private static readonly Color InfoBorderColor = Color.FromArgb(0, 0, 192);
        private static readonly Color InfoBackgroundColor = Color.FromArgb(192, 192, 255);

        private Color _borderColor = Color.CornflowerBlue;

        private FormDialog(string title, string message, bool showCancel, DialogSeverity? severity)
        {
            InitializeComponent();
            _showCancel = showCancel;

            if (severity.HasValue)
            {
                SetSeverity(severity.Value);
            }

            labelTitle.Text = title;
            labelMessage.Text = message;
            buttonCancel.Visible = showCancel;
            if (JohanLock.IsLocked) JohanStijl(severity);
        }

        // Johan fork: the same message in the canvas look (dark panel, coloured left edge, round buttons)
        private bool johan;
        private void JohanStijl(DialogSeverity? severity)
        {
            johan = true;
            int S(int px) => AgLibrary.Johan.JohanTheme.S(px);
            AutoScaleMode = AutoScaleMode.None;   // sizes below are already scaled, WinForms must not scale again
            BackColor = AgLibrary.Johan.JohanTheme.Bg;
            ForeColor = AgLibrary.Johan.JohanTheme.Text;
            _borderColor = severity == DialogSeverity.Error ? AgLibrary.Johan.JohanTheme.Red
                : severity == DialogSeverity.Warning ? AgLibrary.Johan.JohanTheme.Amber
                : AgLibrary.Johan.JohanTheme.Blue;
            ClientSize = new Size(S(700), S(360));
            pictureBoxIcon.Visible = false;

            labelTitle.Font = AgLibrary.Johan.JohanTheme.Font(20F, FontStyle.Bold);
            labelTitle.ForeColor = AgLibrary.Johan.JohanTheme.Text;
            labelTitle.Bounds = new Rectangle(S(40), S(28), S(620), S(48));
            labelMessage.Font = AgLibrary.Johan.JohanTheme.Font(14F);
            labelMessage.ForeColor = AgLibrary.Johan.JohanTheme.Text;
            labelMessage.Bounds = new Rectangle(S(40), S(84), S(620), S(170));
            labelMessage.TextAlign = ContentAlignment.TopLeft;

            foreach (var old in new[] { buttonOK, buttonCancel }) old.Visible = false;
            int bw = S(160), bh = S(60), y = S(360) - S(28) - bh;
            var ok = new JohanRoundButton
            {
                Text = _showCancel ? "OK" : "Sluiten",
                Bounds = new Rectangle(S(700) - S(40) - bw, y, bw, bh),
                BackColor = AgLibrary.Johan.JohanTheme.Green,
                ForeColor = Color.White,
                Font = AgLibrary.Johan.JohanTheme.Font(14F, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            Controls.Add(ok);
            AcceptButton = ok;
            if (_showCancel)
            {
                var cancel = new JohanRoundButton
                {
                    Text = "Annuleren",
                    Bounds = new Rectangle(ok.Left - S(12) - bw, y, bw, bh),
                    BackColor = AgLibrary.Johan.JohanTheme.Panel,
                    ForeColor = AgLibrary.Johan.JohanTheme.Text,
                    Font = AgLibrary.Johan.JohanTheme.Font(14F, FontStyle.Bold),
                    DialogResult = DialogResult.Cancel
                };
                Controls.Add(cancel);
                CancelButton = cancel;
            }
        }
        private bool _showCancel;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            if (johan)
            {
                // coloured edge on the left, thin outline around
                using (var b = new SolidBrush(_borderColor)) e.Graphics.FillRectangle(b, 0, 0, AgLibrary.Johan.JohanTheme.S(10), Height);
                using (var p = new Pen(AgLibrary.Johan.JohanTheme.PanelLight, 1)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                return;
            }
            var borderPen = new Pen(_borderColor, 20);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width, Height);
        }

        private void SetSeverity(DialogSeverity severity)
        {
            switch (severity)
            {
                case DialogSeverity.Error:
                    _borderColor = ErrorBorderColor;
                    BackColor = ErrorBackgroundColor;
                    pictureBoxIcon.Image = Resources.Error;
                    break;
                case DialogSeverity.Warning:
                    _borderColor = WarningBorderColor;
                    BackColor = WarningBackgroundColor;
                    pictureBoxIcon.Image = Resources.Warning;
                    break;
                case DialogSeverity.Info:
                    _borderColor = InfoBorderColor;
                    BackColor = InfoBackgroundColor;
                    pictureBoxIcon.Image = Resources.Info;
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(severity), (int)severity, typeof(DialogSeverity));
            }

            pictureBoxIcon.Visible = true;
        }

        public static void Show(string title, string message, DialogSeverity? severity = null)
        {
            // Log to event viewer before showing dialog
            if (severity == DialogSeverity.Error || severity == DialogSeverity.Warning)
            {
                Log.EventWriter($"Dialog: {title} | {message}");
            }

            using (var form = new FormDialog(title, message, showCancel: false, severity))
            {
                form.ShowDialog();
            }
        }

        public static DialogResult ShowQuestion(string title, string message, DialogSeverity? severity = null)
        {
            using (var form = new FormDialog(title, message, showCancel: true, severity))
            {
                return form.ShowDialog();
            }
        }
    }
}

