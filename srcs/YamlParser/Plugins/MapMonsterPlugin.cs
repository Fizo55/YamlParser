using YamlDotNet.Serialization;
using YamlParser.Core;
using YamlParser.Entities;
using YamlParser.Extensions;
using YamlParser.Shared;
using System.IO;
using System.Linq;

namespace YamlParser.Plugins
{
    public class MapMonsterPlugin : IPlugin
    {
        private readonly Configuration _configuration;
        private readonly List<int> _mapAlreadyDone;
        private readonly Serializer _serializer;

        public MapMonsterPlugin(IOptions<Configuration> configuration)
        {
            _configuration = configuration.Value;
            _serializer = new SerializerBuilder().DisableAliases().Build();
            _mapAlreadyDone = new();
        }

        public void Run()
        {
            var filteredMobsLines = Path.Combine(_configuration.BasePath, _configuration.PacketName).FilterLines(new[] { "in", "c_map" });

            var mapsMonsters = filteredMobsLines
                .SelectMany((line, index) =>
                {
                    var parts = line.Split(' ');
                    if (parts.Length > 3 && parts[0] == "c_map")
                    {
                        return new[] { (mapId: int.Parse(parts[2]), lineParts: parts) };
                    }
                    return Enumerable.Empty<(int, string[])>();
                })
                .Where(tuple => tuple.lineParts.Length > 7 && tuple.lineParts[0] == "in" && tuple.lineParts[1] == "3" && File.Exists(Path.Combine(_configuration.BasePath, _configuration.BinaryMapFolder, tuple.mapId.ToString())) && File.Exists(Path.Combine(_configuration.BasePath, _configuration.BinaryMapFolder, tuple.lineParts[3])))
                .Select(tuple => new MapMonsterData
                {
                    MapMonsterId = int.Parse(tuple.lineParts[3]),
                    VNum = int.Parse(tuple.lineParts[2]),
                    MapX = short.Parse(tuple.lineParts[4]),
                    MapY = short.Parse(tuple.lineParts[5]),
                    Position = (byte)(tuple.lineParts[6] == string.Empty ? 0 : byte.Parse(tuple.lineParts[6])),
                    MapId = tuple.mapId
                })
                .GroupBy(m => m.MapId)
                .Select(g => new MapMonster { MapId = g.Key, Monsters = g.ToList() })
                .ToList();

            foreach (var mapMonster in mapsMonsters)
            {
                if (_mapAlreadyDone.Contains(mapMonster.MapId)) continue;

                var yaml = _serializer.Serialize(mapMonster);
                _configuration.CreateFile($"map_{mapMonster.MapId}_monsters", _configuration.MapMonsterFolder, yaml);
                _mapAlreadyDone.Add(mapMonster.MapId);
            }
        }
    }
}
