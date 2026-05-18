using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HashidsNet;
using InventoryBase.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace InventoryBase.Infrastructure.Services
{
    public class HashService : IHashService
    {
        private readonly Hashids _hashids;

        public HashService(IConfiguration config)
        {
            var salt = config["Hashids:Salt"]
                ?? throw new InvalidOperationException("Hashids:Salt is missing from appsettings.json");

            var minLength = int.TryParse(config["Hashids:MinLength"], out var ml) ? ml : 6;
            _hashids = new Hashids(salt, minLength);
        }

        public string Encode(int id) => _hashids.Encode(id);

        public int? Decode(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) return null;
            var numbers = _hashids.Decode(hash);
            return numbers.Length == 1 ? numbers[0] : null;
        }
    }
}