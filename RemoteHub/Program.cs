using System.Security.Cryptography;
using RemoteHub.Security;
using RemoteHub.UI;

namespace RemoteHub;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--selftest")
        {
            ApplicationConfiguration.Initialize();
            return SelfTest.Run(args[1..]);
        }
        if (args.Length >= 2 && args[0] == "--screens")
        {
            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.Dark);
            return SelfTest.Screens(args[1]);
        }
        if (args.Length >= 2 && args[0] == "--import")
        {
            try
            {
                if (Vault.Exists && Vault.FileNeedsMasterPassword()) { Console.WriteLine("vault is locked with a master password; import from inside the app"); return 1; }
                var (v, d) = Vault.Exists ? Vault.Open(null) : Vault.CreateNew();
                var text = File.ReadAllText(args[1]);
                var rx = new System.Text.RegularExpressions.Regex(@"(?<target>[^\s@]+@(?<device>[^\s:@]+):(?<proto>[A-Za-z0-9_]+))");
                var ipRx = new System.Text.RegularExpressions.Regex(@"^(?<name>.+?)-(?<ip>\d{1,3}(?:\.\d{1,3}){3})$");
                var existing = d.Machines.Select(m => m.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
                int added = 0;
                foreach (System.Text.RegularExpressions.Match mt in rx.Matches(text))
                {
                    if (!mt.Groups["proto"].Value.Equals("RDP", StringComparison.OrdinalIgnoreCase)) continue;
                    var target = mt.Groups["target"].Value;
                    if (!existing.Add(target)) continue;
                    var device = mt.Groups["device"].Value;
                    string name = device, ip = "";
                    var im = ipRx.Match(device);
                    if (im.Success) { name = im.Groups["name"].Value; ip = im.Groups["ip"].Value; }
                    d.Machines.Add(new Machine { Name = name, Ip = ip, Target = target, Mode = ConnectionMode.Bastion, Group = UI.ImportDialog.GuessGroup(name) });
                    added++;
                }
                v.Save(d);
                Console.WriteLine($"imported {added} machine(s); total now {d.Machines.Count}");
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
            return 0;
        }
        if (args.Length >= 2 && args[0] == "--set-host")
        {
            try
            {
                if (!Vault.Exists || Vault.FileNeedsMasterPassword()) { Console.WriteLine("cannot open vault"); return 1; }
                var (v, d) = Vault.Open(null);
                d.Bastion.Host = args[1];
                v.Save(d);
                Console.WriteLine($"bastion.host set to {args[1]}");
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
            return 0;
        }
        if (args.Length >= 2 && args[0] == "--makeicon") { Icons.WriteIco(args[1]); Console.WriteLine("wrote " + args[1]); return 0; }
        if (args.Length >= 1 && args[0] == "--dump")
        {
            try
            {
                if (!Vault.Exists) { Console.WriteLine("NO VAULT"); return 0; }
                if (Vault.FileNeedsMasterPassword()) { Console.WriteLine("VAULT NEEDS MASTER PASSWORD"); return 0; }
                var (_, d) = Vault.Open(null);
                Console.WriteLine($"bastion.host={d.Bastion.Host} user={d.Bastion.PrimaryUser} auth={d.Bastion.Authorization} hasPwd={d.Bastion.PrimaryPassword.Length > 0}");
                Console.WriteLine($"credentials={d.Credentials.Count}: " + string.Join(", ", d.Credentials.Select(c => $"{c.Name}/{c.Username}")));
                Console.WriteLine($"machines={d.Machines.Count}");
                foreach (var m in d.Machines.OrderByDescending(m => m.LastUsed ?? DateTime.MinValue))
                    Console.WriteLine($"  {m.Name}  ip={m.Ip}  mode={m.Mode}  group={m.Group}  lastUsed={m.LastUsed:yyyy-MM-dd HH:mm}");
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
            return 0;
        }

        using var mutex = new Mutex(true, @"Local\RemoteHub.SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show($"{App.Name} is already running.", App.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.Dark);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, $"{App.Name} — unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);

        Store? store;
        try
        {
            store = OpenStore();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the vault:\n{ex.Message}\n\n{Vault.FilePath}", App.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        if (store == null) return 0;

        Diag.Enabled = true; // on by default while we get the fill working; turn off in Settings later
        Diag.Log($"=== RemoteHub started  v{Application.ProductVersion}  OCR={Rdp.ScreenReader.Available} ===");
        Application.Run(new MainForm(store));
        return 0;
    }

    private static Store? OpenStore()
    {
        if (!Vault.Exists)
        {
            var (v, d) = Vault.CreateNew();
            return new Store(v, d);
        }

        if (!Vault.FileNeedsMasterPassword())
        {
            var (v, d) = Vault.Open(null);
            return new Store(v, d);
        }

        Store? result = null;
        using var dlg = new UnlockDialog(pw =>
        {
            try
            {
                var (v, d) = Vault.Open(pw);
                result = new Store(v, d);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        });
        return dlg.ShowDialog() == DialogResult.OK ? result : null;
    }
}
