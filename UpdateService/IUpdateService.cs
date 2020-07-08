using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hypertherm.Update
{
    public interface IUpdateService
    {
        Task Update(string version = "latest");
        Version LatestReleasedVersion();
        bool IsUpdateAvailable();
        Task<List<string>> ListReleases();
    }
}