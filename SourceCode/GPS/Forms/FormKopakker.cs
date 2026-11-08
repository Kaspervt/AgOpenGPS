using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: "Kopakker" as one bar above the bottom buttons. One width, built straight away
    // from the field boundary (JohanBouwKopakker), on/off, sections off on the headland, remove.
    // Non-modal, so the map shows the result while you set it.
    public class FormKopakker : Form
    {
        private readonly FormGPS mf;
        private readonly Timer tick = new Timer { Interval = 500 };
        private Label lblBreedte;
        private Button btnAan, btnSecties;
        private double breedte;

        public FormKopakker(FormGPS formGPS)
        {
            mf = formGPS;
            Name = "FormKopakker";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bar;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            TopMost = false;

            var scr = Screen.FromControl(mf).Bounds;
            int h = 96, w = Math.Min(1310, scr.Width - 32);
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Bottom - 132 - 12 - h, w, h);

            breedte = mf.JohanKopakkerBreedte();
            if (breedte <= 0) breedte = Math.Round(Math.Max(1.0, mf.tool.width - mf.tool.overlap) * 2) / 2;
            BuildLayout();

            tick.Tick += (s, e) => UpdateLabel();
            Load += (s, e) => { UpdateLabel(); tick.Start(); };
            FormClosed += (s, e) => tick.Stop();
        }

        private Button Knop(int x, int wdt, string tekst, string icon, Action actie)
        {
            var b = new JohanRoundButton
            {
                Bounds = new Rectangle(x, 14, wdt, Height - 28),
                Text = tekst,
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(11F, FontStyle.Bold),
                Image = icon == null ? null : JohanSkin.Icon(icon, 26),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                TextAlign = ContentAlignment.MiddleCenter
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = JohanTheme.PanelLight;
            b.Click += (s, e) => { actie(); UpdateLabel(); mf.Activate(); };
            Controls.Add(b);
            return b;
        }

        private bool Heeft => mf.isJobStarted && mf.bnd.bndList.Count > 0 && mf.bnd.bndList[0].hdLine.Count > 0;

        private void Bouw(double nieuw)
        {
            nieuw = Math.Max(0.5, Math.Min(60, Math.Round(nieuw * 2) / 2));
            if (mf.JohanBouwKopakker(nieuw)) breedte = nieuw;
        }

        private void BuildLayout()
        {
            int x = 14, gap = 10;
            double werk = Math.Max(0.5, mf.tool.width - mf.tool.overlap);

            Knop(x, 96, "0,5 m", "chevron-left_licht_64", () => Bouw(breedte - 0.5)); x += 96 + gap;
            lblBreedte = new Label
            {
                Bounds = new Rectangle(x, 14, 150, Height - 28),
                Font = JohanTheme.Font(18F, FontStyle.Bold),
                ForeColor = JohanTheme.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            Controls.Add(lblBreedte); x += 150 + gap;
            Knop(x, 96, "0,5 m", "chevron-right_licht_64", () => Bouw(breedte + 0.5)); x += 96 + 2 * gap;

            Knop(x, 150, "1 werkgang", "kopakker_licht_64", () => Bouw(werk)); x += 150 + gap;
            Knop(x, 160, "2 werkgangen", "kopakker_licht_64", () => Bouw(2 * werk)); x += 160 + 2 * gap;

            btnAan = Knop(x, 96, "Aan", "check_licht_64", () =>
            {
                if (!Heeft) { Bouw(breedte); return; }
                mf.bnd.isHeadlandOn = !mf.bnd.isHeadlandOn;
                if (!mf.bnd.isHeadlandOn && mf.vehicle.isHydLiftOn) mf.vehicle.isHydLiftOn = false;
            }); x += 96 + gap;
            btnSecties = Knop(x, 200, "Secties uit op kop", "box-margin_licht_64", () =>
            {
                mf.bnd.isSectionControlledByHeadland = !mf.bnd.isSectionControlledByHeadland;
                ToolSettings.Default.setHeadland_isSectionControlled = mf.bnd.isSectionControlledByHeadland;
                ToolSettings.Default.Save();
            }); x += 200 + gap;
            Knop(x, 92, "Weg", "trash_licht_64", () => { mf.JohanBouwKopakker(0); });

            var klaar = Knop(Width - 14 - 96, 96, "Klaar", "check_licht_64", () => Close());
            klaar.BackColor = JohanTheme.Green;
            klaar.ForeColor = Color.White;
        }

        private void UpdateLabel()
        {
            lblBreedte.Text = Heeft ? breedte.ToString("0.0").Replace('.', ',') + " m" : "geen";
            if (btnAan != null)
            {
                bool aan = Heeft && mf.bnd.isHeadlandOn;
                btnAan.Text = !Heeft ? "Maak" : aan ? "Aan" : "Uit";
                btnAan.BackColor = aan ? JohanTheme.Green : JohanTheme.Panel;
                btnAan.ForeColor = aan ? Color.White : JohanTheme.Text;
            }
            if (btnSecties != null)
            {
                bool sc = mf.bnd.isSectionControlledByHeadland;
                btnSecties.BackColor = sc ? JohanTheme.Green : JohanTheme.Panel;
                btnSecties.ForeColor = sc ? Color.White : JohanTheme.Text;
            }
        }
    }
}
