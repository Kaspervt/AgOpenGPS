using System;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    // Johan fork: replaces the AgOpenGPS job menu in driver mode.
    // Same dialog results as FormJob, so the caller in btnJobMenu_Click needs no change:
    // Yes = new field, Abort = ISO-XML, No = KML, Retry = copy of existing, OK + isEasyDriveRequested = just drive.
    public class FormNieuwPerceel : Form
    {
        private readonly FormGPS mf;

        public FormNieuwPerceel(FormGPS formGPS)
        {
            mf = formGPS;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromControl(mf).Bounds;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(14F);
            KeyPreview = true;
            ShowInTaskbar = false;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Terug(); };
            BuildLayout();
        }

        private void BuildLayout()
        {
            int w = Width, h = Height, x = 56;
            Controls.Add(new Label { Text = "Nieuw perceel", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, 36) });
            Controls.Add(new Label { Text = "Kies waar het perceel vandaan komt", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + 2, 92) });

            int cardW = (w - 2 * x - 20) / 2, cardH = 118, gap = 20, top = 150;
            var items = new[]
            {
                new { Titel = "Uit Lijnbeheer (ISO-XML)", Uitleg = "Perceel, grens en lijnen in één keer, zoals ze in Lijnbeheer staan", Icon = "file-import_licht_64", Actie = new Action(async () => { await SluitHuidig(); DialogResult = DialogResult.Abort; Close(); }) },
                new { Titel = "Leeg perceel", Uitleg = "Alleen een naam; grens en lijnen maak je rijdend", Icon = "folder-plus_licht_64", Actie = new Action(async () => { await SluitHuidig(); DialogResult = DialogResult.Yes; Close(); }) },
                new { Titel = "Uit KML (Google Earth)", Uitleg = "Grens uit een KML-bestand", Icon = "world_licht_64", Actie = new Action(async () => { await SluitHuidig(); DialogResult = DialogResult.No; Close(); }) },
                new { Titel = "Kopie van bestaand perceel", Uitleg = "Zelfde grens en lijnen, nieuwe naam voor dit seizoen", Icon = "copy_licht_64", Actie = new Action(() => { DialogResult = DialogResult.Retry; Close(); }) },
                new { Titel = "Gewoon rijden", Uitleg = "Zonder perceel, met een losse lijn (wordt niet bewaard)", Icon = "tractor_licht_64", Actie = new Action(() => { mf.isEasyDriveRequested = true; DialogResult = DialogResult.OK; Close(); }) },
            };

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                int col = i % 2, row = i / 2;
                var card = new Button
                {
                    Bounds = new Rectangle(x + col * (cardW + gap), top + row * (cardH + gap), cardW, cardH),
                    BackColor = JohanTheme.Panel,
                    ForeColor = JohanTheme.Text,
                    FlatStyle = FlatStyle.Flat,
                    Text = "",
                    Image = JohanSkin.Icon(it.Icon, 40),
                    ImageAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(24, 0, 0, 0)
                };
                card.FlatAppearance.BorderSize = 0;
                card.FlatAppearance.MouseOverBackColor = JohanTheme.PanelLight;
                var actie = it.Actie;
                card.Click += (s, e) => actie();
                var t = new Label { Text = it.Titel, Font = JohanTheme.Font(16F, FontStyle.Bold), ForeColor = JohanTheme.Text, BackColor = Color.Transparent, AutoSize = true, Location = new Point(90, 28) };
                var u = new Label { Text = it.Uitleg, Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, BackColor = Color.Transparent, AutoSize = false, Bounds = new Rectangle(90, 62, cardW - 110, 40) };
                t.Click += (s, e) => actie();
                u.Click += (s, e) => actie();
                card.Controls.Add(t);
                card.Controls.Add(u);
                Controls.Add(card);
            }

            var terug = new Button
            {
                Text = "Terug",
                Bounds = new Rectangle(x, h - 100, 200, 64),
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            terug.FlatAppearance.BorderSize = 0;
            terug.Click += (s, e) => Terug();
            Controls.Add(terug);
        }

        private async System.Threading.Tasks.Task SluitHuidig()
        {
            if (mf.isJobStarted) await mf.FileSaveEverythingBeforeClosingField();
        }

        private void Terug()
        {
            mf.isCancelJobMenu = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
