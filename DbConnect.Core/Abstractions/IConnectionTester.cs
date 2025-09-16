using System.Threading.Tasks;
using DbConnect.Core.Models;

namespace DbConnect.Core.Abstractions;

public interface IConnectionTester
{
    Task<(bool ok, string message)> TestAsync(ConnectionProfile profile);
    string BuildConnectionString(ConnectionProfile profile);
}
