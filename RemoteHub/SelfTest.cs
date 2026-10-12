using System.Security.Cryptography;
using System.Text;
using RemoteHub.Rdp;
using RemoteHub.Security;
using RemoteHub.UI;

namespace RemoteHub;

/// <summary>
/// "RemoteHub.exe --selftest out.txt [screenshot.png ...]" — checks the vault, login building, key typing,
/// prompt detection and that the RDP ActiveX loads and reports a failed connection. Set REMOTEHUB_HOME to a
/// temp folder first so the real vault is not touched.
/// </summary>
internal static class SelfTest
{
    /// <summary>"--screens dir": renders the main window and dialogs to PNGs with sample data (for layout checks).</summary>
    public static int Screens(string dir)
    {
        Directory.CreateDirectory(dir);
        var (v, d) = Vault.CreateNew();
        d.Bastion.Host = "172.16.150.32";
        d.Bastion.PrimaryUser = "h.shahmoradi";
        d.Credentials.Add(new CredentialProfile { Name = "Domain admin", Username = "SANAY\\h.shahmoradi", Password = "x" });
        foreach (var (n, ip) in new[] { ("NFCore", "172.16.170.221"), ("NFDB01", "172.16.11.22"), ("IRPTSQLPRD", "172.16.11.239"), ("PTN-IRPTSUIP", "172.19.11.22"), ("IrenexAPI", "172.16.10.32"), ("CSDIData", "172.16.170.227") })
            d.Machines.Add(new Machine { Name = n, Ip = ip, Target = Machine.MakeTarget(n, ip), Group = ImportDialog.GuessGroup(n), Favorite = n == "NFCore", LastUsed = n == "NFDB01" ? DateTime.Now : null });
        var store = new Store(v, d);

        void Snap(Form f, string name)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-4000, -4000);
            f.ShowInTaskbar = false;
            f.Show();
            Application.DoEvents();
            using var bmp = new Bitmap(f.Width, f.Height);
            f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.Size));
            bmp.Save(Path.Combine(dir, name + ".png"));
            f.Close();
        }

        using (var main = new MainForm(store)) Snap(main, "main");
        using (var s = new SettingsDialog(store)) Snap(s, "settings");
        using (var m = new MachineDialog(store, d.Machines[0])) Snap(m, "machine");
        using (var c = new CredentialsDialog(store)) Snap(c, "credentials");
        using (var q = new QuickConnectForm(store)) Snap(q, "quick");
        var fills = new List<FillEntry>
        {
            new("h.shahmoradi", "bastion / step 1", "h.shahmoradi", "x", IsBastion: true),
            new("Domain admin", "SANAY\\M_Rajabi", "SANAY\\M_Rajabi", "x"),
            new("Database user", "sa", "sa", "x"),
        };
        using (var p = new CredentialPicker(fills)) Snap(p, "picker");
        using (var mb = new MiniFsBar(() => { }, () => { })) { mb.PrepareForPreview("NFCore  ·  172.16.170.221", TabStatus.Connected); Snap(mb, "minibar"); }
        return 0;
    }

    private static string Shorten(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 90 ? s[..90] + "…" : s;
    }

    private static Bitmap MockLoginPage(bool withDevice)
    {
        var bmp = new Bitmap(1280, 720);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(10, 31, 94));
        using var f = new Font("Segoe UI", 11f);
        int y = 260;
        if (withDevice)
        {
            g.DrawString("Target Information Required", f, Brushes.White, 440, 200);
            g.DrawString("Device", f, Brushes.White, 300, y);
            g.DrawString("NFCore-172.16.170.221", f, Brushes.White, 420, y);
            y += 40;
        }
        else
        {
            using var big = new Font("Segoe UI Semibold", 22f);
            g.DrawString("WALLIX", big, Brushes.White, 560, 160);
        }
        g.DrawString("Login", f, Brushes.White, 300, y);
        g.FillRectangle(Brushes.White, 420, y, 500, 24);
        g.DrawString("Password", f, Brushes.White, 300, y + 44);
        g.FillRectangle(Brushes.White, 420, y + 44, 500, 24);
        return bmp;
    }

    public static int Run(string[] args)
    {
        var log = new StringBuilder();
        int failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
            if (!ok) failures++;
        }

        try
        {
            // ---- vault
            var (v, d) = Vault.CreateNew();
            d.Credentials.Add(new CredentialProfile { Name = "t", Username = "DOM\\me", Password = "p@ss Wörd!" });
            v.Save(d);
            var (_, d2) = Vault.Open(null);
            Check("vault round-trip", d2.Credentials.Single().Password == "p@ss Wörd!");
            Check("vault file has no plaintext", !File.ReadAllText(Vault.FilePath).Contains("p@ss"));
            v.SetMasterPassword("secret1", d);
            Check("vault master flag", Vault.FileNeedsMasterPassword());
            var (v3, d3) = Vault.Open("secret1");
            Check("vault open with master", d3.Credentials.Single().Username == "DOM\\me");
            bool threw = false;
            try { Vault.Open("wrong"); } catch (CryptographicException) { threw = true; }
            Check("vault rejects wrong master", threw);
            Check("verify master", v3.VerifyMasterPassword("secret1") && !v3.VerifyMasterPassword("x"));
            v3.SetMasterPassword(null, d3);
            Check("vault remove master", !Vault.FileNeedsMasterPassword() && Vault.Open(null).data.Credentials.Count == 1);

            // ---- login building
            var b = new BastionConfig { PrimaryUser = "h.shahmoradi", Authorization = "Sanay-Shahmoradi" };
            var m = new Machine { Target = "Interactive@NFCore-172.16.170.221:RDP" };
            Check("login with auth", Store.BuildBastionLogin(b, m) == "Interactive@NFCore-172.16.170.221:RDP:Sanay-Shahmoradi:h.shahmoradi", Store.BuildBastionLogin(b, m));
            b.Authorization = "";
            Check("login without auth", Store.BuildBastionLogin(b, m) == "Interactive@NFCore-172.16.170.221:RDP:h.shahmoradi", Store.BuildBastionLogin(b, m));
            Check("selector login", Store.BuildBastionLogin(b, new Machine()) == "h.shahmoradi");
            Check("make target", Machine.MakeTarget("NFCore", "172.16.170.221") == "Interactive@NFCore-172.16.170.221:RDP");

            // ---- key typing
            var steps = KeyTyper.Parse("{login}{TAB}{password}{DELAY:300}{ENTER}x").ToList();
            Check("parse sequence", steps.Count == 6 && steps[3].Kind == KeyTyper.StepKind.Delay && steps[3].Value == 300 && steps[5].Text == "x");
            var keys = new List<(int sc, bool up)>();
            KeyTyper.AppendText(keys, "aA!");
            Check("scancodes", keys.SequenceEqual(new List<(int, bool)>
            {
                (0x1E, false), (0x1E, true),
                (0x2A, false), (0x1E, false), (0x1E, true), (0x2A, true),
                (0x2A, false), (0x02, false), (0x02, true), (0x2A, true),
            }));
            Check("non-US char rejected", !KeyTyper.CanType("pass€", out var bad) && bad == '€');

            // ---- SendInput INPUT struct size (the bug that made SendInput return 0)
            Check("INPUT struct size is 40 (x64)", KeyTyper.NativeInputSize == 40, KeyTyper.NativeInputSize.ToString());

            // ---- grouping
            var groups = new[] { "NFCore", "NFDB01", "IRPTOraclePRD", "PTN-IRPTSUIP", "IrenexAPI", "CSDIData", "RCDB", "SanayAccessURL", "IssuerAffairsRC" }
                .Select(ImportDialog.GuessGroup).ToArray();
            Check("guess groups", string.Join(",", groups) == "NF,NF,IRPT,PTN,Irenex,CSDI,RC,Sanay,Issuer", string.Join(",", groups));

            // ---- prompt detector on real screenshots
            foreach (var img in args.Skip(1))
            {
                using var bmp = new Bitmap(img);
                log.AppendLine($"INFO  detector {Path.GetFileName(img)} -> {PromptDetector.Analyze(bmp)}");
                var (scr, txt) = ScreenReader.ClassifyAsync(bmp).GetAwaiter().GetResult();
                log.AppendLine($"INFO  ocr      {Path.GetFileName(img)} -> {scr}   [{Shorten(txt)}]");
            }

            // ---- OCR on mocks: WALLIX step-1 login page vs step-2 target page (same colours, different text)
            using (var mock = MockLoginPage(withDevice: false))
                Check("ocr: step-1 login page (mock)", ScreenReader.ClassifyAsync(mock).GetAwaiter().GetResult().screen == ScreenReader.Screen.BastionLogin);
            using (var mock = MockLoginPage(withDevice: true))
                Check("ocr: step-2 target page (mock)", ScreenReader.ClassifyAsync(mock).GetAwaiter().GetResult().screen == ScreenReader.Screen.TargetPrompt);

            // ---- Reparenting a SessionView between forms must preserve its window handle,
            //      otherwise a detached tab would recreate the RDP control and drop the connection.
            using (var fA = new Form { Width = 800, Height = 600, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) })
            using (var fB = new Form { Width = 800, Height = 600, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) })
            {
                var rp = new SessionView(new Store(Vault.Open(null).vault, new AppData()), new Machine { Name = "rp", Ip = "127.0.0.1:1", Mode = ConnectionMode.Direct });
                fA.Controls.Add(rp);
                fA.Show();
                Application.DoEvents();
                var h1 = rp.Handle;
                bool created1 = rp.IsHandleCreated;
                fA.Controls.Remove(rp);
                fB.Controls.Add(rp);
                fB.Show();
                Application.DoEvents();
                var h2 = rp.Handle;
                Check("detach preserves window handle", created1 && rp.IsHandleCreated && h1 == h2, $"{h1}->{h2}");
                fB.Controls.Remove(rp);
                rp.Dispose();
            }

            // ---- RDP ActiveX: load, configure, connect to a closed port, expect a clean disconnect
            var store = new Store(Vault.Open(null).vault, new AppData());
            store.Data.Bastion.Host = "127.0.0.1";
            store.Data.Bastion.Port = 1;
            store.Data.Bastion.PrimaryUser = "tester";
            store.Data.Bastion.PrimaryPassword = "pw";
            using var form = new Form { Width = 1000, Height = 700, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            var direct = new SessionView(store, new Machine { Name = "direct", Ip = "127.0.0.1:1", Mode = ConnectionMode.Direct });
            var bastion = new SessionView(store, new Machine { Name = "bastion", Target = "Interactive@X-1.2.3.4:RDP" });
            form.Controls.Add(direct);
            form.Controls.Add(bastion);
            int done = 0;
            string directText = "", bastionText = "";
            var deadline = DateTime.UtcNow.AddSeconds(45);
            var poll = new System.Windows.Forms.Timer { Interval = 250 };
            poll.Tick += (_, _) =>
            {
                done = (direct.Tab.Status == TabStatus.Disconnected ? 1 : 0) + (bastion.Tab.Status == TabStatus.Disconnected ? 1 : 0);
                if (done == 2 || DateTime.UtcNow > deadline) { directText = direct.OverlayText; bastionText = bastion.OverlayText; poll.Stop(); form.Close(); }
            };
            form.Shown += (_, _) =>
            {
                direct.Connect();
                bastion.Visible = true;
                bastion.Connect();
                poll.Start();
            };
            Application.Run(form);
            Check("rdp direct -> disconnected event", direct.Tab.Status == TabStatus.Disconnected, directText);
            Check("rdp bastion -> disconnected event", bastion.Tab.Status == TabStatus.Disconnected, bastionText);
        }
        catch (Exception ex)
        {
            Check("exception", false, ex.ToString());
        }

        log.AppendLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILURE(S)");
        File.WriteAllText(args[0], log.ToString());
        return failures;
    }
}
