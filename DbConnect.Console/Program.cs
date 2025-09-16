using DbConnect.Core.Models;
using DbConnect.Core.Services;

var store = new FileProfileStore();
var tester = new ConnectionTester();

if (args.Length == 0)
{
    Console.WriteLine("Comandos: add | list | test");
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "add":
        if (args.Length < 7)
        {
            Console.WriteLine("Uso: add <name> <kind> <hostOrFile> <port> <database> <username> [password]");
            return;
        }
        var kind = Enum.Parse<DbKind>(args[2], ignoreCase: true);
        int? port = int.TryParse(args[4], out var p) ? p : null;

        var profile = new ConnectionProfile(
            Name: args[1],
            Kind: kind,
            HostOrFile: args[3],
            Port: port,
            Database: args[5],
            Username: args[6],
            Password: args.Length >= 8 ? args[7] : null,
            CreatedAtUtc: DateTime.UtcNow
        );

        await store.UpsertAsync(profile);
        Console.WriteLine($"Perfil '{profile.Name}' salvo.");
        break;

    case "list":
        var all = await store.ListAsync();
        foreach (var it in all)
            Console.WriteLine($"{it.Name} -> {it.Kind} {it.HostOrFile}:{it.Port} DB={it.Database} USER={it.Username}");
        break;

    case "test":
        if (args.Length < 2) { Console.WriteLine("Uso: test <name>"); return; }
        var prof = await store.GetAsync(args[1]);
        if (prof is null) { Console.WriteLine("Perfil não encontrado."); return; }
        var (ok, msg) = await tester.TestAsync(prof);
        Console.WriteLine(ok ? $"✅ {msg}" : $"❌ {msg}");
        break;

    default:
        Console.WriteLine("Comando desconhecido.");
        break;
}
