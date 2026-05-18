using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryBase.Core.Interfaces.Services
{
    public interface IHashService
    {
        string Encode(int id);
        int? Decode(string hash); 
    }
}
