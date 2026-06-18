using ForestIQ.Domain.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.Interface
{
    public interface IAdConnectionCache
    {
        Task<string> Create(PowerShellRequest request, string? kerberosCachePath = null);

        AdConnection? Get(string connectionId);

        void Remove(string connectionId);
    }
}
