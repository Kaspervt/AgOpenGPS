using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Onderhoud en programma" of the settings page (canvas proposal D, 8-11).
    // Version, log, field import, the Beheer screen, the code, the original AgOpenGPS windows and
    // shutting down. Nothing here writes to the steering board; the original windows do that.
    public partial class FormInstellingen
    {
        private void HoofdstukOnderhoud()
        {
            var k = Hoofdstuk("Onderhoud en programma", "Versie, logboek, importeren en de originele vensters.");

            // Version: the AgOpenGPS version this fork is built on, marked as the Johan build
            RijTekst(k, "Versie", () => "AgOpenGPS " + Program.Version + " + johan");

            // Event log of AgOpenGPS (FormEventViewer), same as the Beheer screen
            RijKnoppen(k, "Logboek", "Meldingen en storingen van vandaag",
                ("Bekijken", () => mf.BeheerOpenLog()));

            // Field import: the full AgOpenGPS field menu once. This page closes first, otherwise the
            // main form cannot show the menu (same construction as FormBeheer).
            Action perceelImporteren = () =>
            {
                Close();
                mf.BeginInvoke(new Action(() => mf.JohanImportMenu()));
            };
            RijKnoppen(k, "Perceel importeren", null,
                ("KML, ISO-XML", perceelImporteren));

            // The Beheer screen (status, field tools, original windows) stays reachable from here
            Action beheerOpenen = () =>
            {
                using (var f = new FormBeheer(mf))
                {
                    f.ShowDialog(this);
                }
                // Beheer closes itself when the shutdown question was answered; follow it
                if (mf.johanAfsluitKeuze >= 0)
                {
                    Close();
                }
            };
            RijKnoppen(k, "Beheer", "Status en hulpmiddelen",
                ("Beheer openen", beheerOpenen));

            // Change the code that protects the chapters marked "Achter de code"
            RijKnoppen(k, "Code wijzigen", "De code voor de beschermde hoofdstukken",
                ("Wijzigen", CodeWijzigen));

            // The original AgOpenGPS windows, full screen, for everything this page does not cover
            RijKnoppen(k, "Originele vensters", "Beeldvullend, voor als iets hierboven ontbreekt",
                ("Configuratie", () => mf.BeheerOpenConfig()),
                ("Autostuur", () => mf.BeheerOpenSteer()),
                ("AgIO", () => mf.BeheerOpenAgIO()));

            // Shutting down: our own question, then the normal AgOpenGPS close path. The caller of
            // this page runs JohanAfsluitenUitvoeren() once it has closed.
            RijKnoppen(k, "Afsluiten", "Het perceel en de lijnen worden bewaard",
                ("Programma uit", Afsluiten),
                ("!Tablet uit", Afsluiten));
        }

        // Ask the current code, then the new one twice; store it and apply.
        // FormGetal.Vraag returns the start value when the keypad is cancelled; a start value of -1
        // lies below the minimum (OK refuses it), so -1 can only mean "cancelled".
        private void CodeWijzigen()
        {
            double huidig = FormGetal.Vraag(this, "Huidige code", "", 0, 999999, -1, 0, laatsteVeld);
            if (huidig < 0)
            {
                return;
            }
            if ((int)huidig != Settings.Default.setDisplay_johanCode)
            {
                mf.TimedMessageBox(2000, "Code wijzigen", "Dat is niet de huidige code");
                return;
            }

            double nieuw = FormGetal.Vraag(this, "Nieuwe code", "", 0, 999999, -1, 0, laatsteVeld);
            if (nieuw < 0)
            {
                return;
            }
            double nogmaals = FormGetal.Vraag(this, "Nieuwe code nog een keer", "", 0, 999999, -1, 0, laatsteVeld);
            if (nogmaals < 0)
            {
                return;
            }
            if ((int)nieuw != (int)nogmaals)
            {
                mf.TimedMessageBox(2500, "Code wijzigen", "De twee codes zijn niet gelijk, de code is niet veranderd");
                return;
            }

            Settings.Default.setDisplay_johanCode = (int)nieuw;
            Toepassen();
            mf.TimedMessageBox(2000, "Code wijzigen", "De nieuwe code is opgeslagen");
        }

        // The shutdown question (program off, tablet off, back); close this page when a choice was made
        private void Afsluiten()
        {
            using (var f = new FormAfsluiten(mf))
            {
                f.ShowDialog(this);
            }
            if (mf.johanAfsluitKeuze >= 0)
            {
                Close();
            }
        }
    }
}
