using System.Collections.Generic;
using System.Threading.Tasks;
using DbConnect.Core.Models;

namespace DbConnect.Core.Abstractions;

public interface IProfileStore
{
    Task<IReadOnlyList<ConnectionProfile>> ListAsync();
    Task<ConnectionProfile?> GetAsync(string name);
    Task UpsertAsync(ConnectionProfile profile);
    Task DeleteAsync(string name);
}
