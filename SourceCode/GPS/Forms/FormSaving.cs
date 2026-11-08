using AgOpenGPS.Core.Translations;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public enum SavingStepState { Pending, Done, Failed }

    public partial class FormSaving : Form
    {
        private readonly bool johan = JohanLock.IsLocked;

        public FormSaving()
        {
            InitializeComponent();

            labelBeer.Text = "✔ " + gStr.gsSaveBeerTime;
            if (johan) JohanStijl();
        }

        // Johan fork: the same list of steps in the canvas look (dark panel, light text, thin outline)
        private void JohanStijl()
        {
            int S(int px) => AgLibrary.Johan.JohanTheme.S(px);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = AgLibrary.Johan.JohanTheme.PanelLight;      // 1 px outline through the padding below
            ClientSize = new Size(S(760), S(360));
            panel1.BackColor = AgLibrary.Johan.JohanTheme.Bg;
            panel1.Bounds = new Rectangle(1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
            listViewSteps.BackColor = AgLibrary.Johan.JohanTheme.Bg;
            listViewSteps.ForeColor = AgLibrary.Johan.JohanTheme.Text;
            listViewSteps.Font = AgLibrary.Johan.JohanTheme.Font(16F);
            listViewSteps.BorderStyle = BorderStyle.None;
            listViewSteps.Bounds = new Rectangle(S(40), S(36), panel1.Width - S(80), S(230));
            progressBar.Visible = false;
            labelBeer.Font = AgLibrary.Johan.JohanTheme.Font(14F, FontStyle.Bold);
            labelBeer.ForeColor = AgLibrary.Johan.JohanTheme.Green;
            labelBeer.Text = "Klaar, tot de volgende keer";
            labelBeer.Location = new Point(S(40), panel1.Height - S(60));
            var bezig = new Label
            {
                Name = "lblBezig",
                Text = "Alles wordt bewaard, een ogenblik",
                Font = AgLibrary.Johan.JohanTheme.Font(12F),
                ForeColor = AgLibrary.Johan.JohanTheme.Muted,
                AutoSize = true,
                Location = new Point(S(40), panel1.Height - S(60))
            };
            panel1.Controls.Add(bezig);
        }

        public void AddStep(string key, string message)
        {
            listViewSteps.Items.Add(new ListViewItem()
            {
                Name = key,
                Text = GetStepText(message + "...", SavingStepState.Pending),
                ForeColor = GetStepColor(SavingStepState.Pending)
            });
        }

        public void UpdateStep(string key, string message, SavingStepState state)
        {
            listViewSteps.Items[key].Text = GetStepText(message, state);
            listViewSteps.Items[key].ForeColor = GetStepColor(state);
        }

        public void Finish()
        {
            progressBar.Visible = false;
            labelBeer.Visible = true;
            var bezig = panel1.Controls["lblBezig"];
            if (bezig != null) bezig.Visible = false;
        }

        private static string GetStepText(string message, SavingStepState state)
        {
            switch (state)
            {
                case SavingStepState.Pending:
                    return "• " + message;
                case SavingStepState.Done:
                    return "✓ " + message;
                case SavingStepState.Failed:
                    return "✗ " + message;
                default:
                    throw new InvalidEnumArgumentException(nameof(state), (int)state, typeof(SavingStepState));
            }
        }

        private Color GetStepColor(SavingStepState state)
        {
            if (johan)
                return state == SavingStepState.Pending ? AgLibrary.Johan.JohanTheme.Muted
                    : state == SavingStepState.Failed ? AgLibrary.Johan.JohanTheme.Red : AgLibrary.Johan.JohanTheme.Text;
            return state == SavingStepState.Pending ? Color.Gray : Color.Black;
        }
    }
}
