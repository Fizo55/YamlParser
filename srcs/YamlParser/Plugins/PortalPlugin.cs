using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlParser.Core;
using YamlParser.Entities;
using YamlParser.Extensions;
using YamlParser.Shared;
using System.IO;
using System.Linq;

namespace YamlParser.Plugins
{
    public class PortalPlugin : IPlugin
    {
        private readonly Configuration _configuration;

        public PortalPlugin(IOptions<Configuration> configuration) 
        {
            _configuration = configuration.Value;
        }

        public void Run()
        {
            DateTime start = DateTime.Now;
            List<int> mapAlreadyDone = new();
            List<PortalAttributes> portals = $"{_configuration.BasePath}{_configuration.PacketName}".FilterLines(new string[] { "c_map ", "gp " })
                .SelectMany((line, index) =>
                {
                    string[] parts = line.Split(' ');
                    if (parts.Length > 3 && parts[0] == "c_map")
                    {
                        return new[] { (mapId: int.Parse(parts[2]), lineParts: parts) };
                    }
                    return Enumerable.Empty<(int, string[])>();
                })
                .Where(tuple => tuple.lineParts.Length > 4 && tuple.lineParts[0] == "gp" && File.Exists($"{_configuration.BasePath}{_configuration.BinaryMapFolder}/{tuple.mapId}") && File.Exists($"{_configuration.BasePath}{_configuration.BinaryMapFolder}/{int.Parse(tuple.lineParts[3])}"))
                .Select(tuple => new PortalAttributes
                {
                    SourceMapId = tuple.mapId,
                    SourceMapX = short.Parse(tuple.lineParts[1]),
                    SourceMapY = short.Parse(tuple.lineParts[2]),
                    DestinationMapId = int.Parse(tuple.lineParts[3]),
                    DestinationMapX = 0,
                    DestinationMapY = 0,
                    Type = short.Parse(tuple.lineParts[4])
                })
                .Distinct()
                .ToList();

            foreach (PortalAttributes portal in portals)
            {
                if (mapAlreadyDone.Contains(portal.SourceMapId)) continue;

                Portal toSerialize = new() { Portals = new() };
                portals.Where(s => s.SourceMapId == portal.SourceMapId).ToList().ForEach(specialPortal =>
                {
                    PortalAttributes portal1 = portals.FirstOrDefault(s => s.SourceMapId == specialPortal.DestinationMapId);
                    if (portal1 == null) return;

                    specialPortal.DestinationMapX = portal1.SourceMapX;
                    specialPortal.DestinationMapY = portal1.SourceMapY;
                    toSerialize.Portals.Add(specialPortal);
                });

                var serializer = new SerializerBuilder().DisableAliases().Build();
                var yaml = serializer.Serialize(toSerialize);
                _configuration.CreateFile($"portals_{portal.SourceMapId}", _configuration.PortalFolder, yaml);
                mapAlreadyDone.Add(portal.SourceMapId);
            }

            DateTime end = DateTime.Now;
            Console.WriteLine($"Portals parsing done in {(end-start).TotalMinutes} minutes.");
        }
    }
}
