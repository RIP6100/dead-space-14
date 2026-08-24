// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using Content.Shared.DeadSpace.Research.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Research;

[DataDefinition]
public sealed partial class MosaicBoard : ISerializationHooks
{
    [DataField]
    public int Radius = 2;

    [DataField]
    public List<MosaicEndpoint> Endpoints = new();

    [DataField]
    public List<Vector2i> Holes = new();

    [DataField]
    public string? Ascii;

    [DataField]
    public Dictionary<string, ProtoId<ResearchFieldPrototype>> Legend = new();

    void ISerializationHooks.AfterDeserialization()
    {
        if (Ascii is { } ascii)
            ParseAscii(ascii);
    }

    private void ParseAscii(string ascii)
    {
        var lines = new List<string>(ascii.Replace("\r", "").Split('\n'));

        while (lines.Count > 0 && lines[0].Trim().Length == 0)
            lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
            lines.RemoveAt(lines.Count - 1);

        if (lines.Count == 0 || lines.Count % 2 == 0)
            throw new InvalidOperationException(
                $"Mosaic ASCII: рядов должно быть нечётное число (2*radius+1), получено {lines.Count}.");

        var radius = (lines.Count - 1) / 2;
        var endpoints = new List<MosaicEndpoint>();
        var holes = new List<Vector2i>();

        for (var i = 0; i < lines.Count; i++)
        {
            var r = i - radius;
            var tokens = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var expected = 2 * radius + 1 - Math.Abs(r);
            if (tokens.Length != expected)
                throw new InvalidOperationException(
                    $"Mosaic ASCII: в ряду {i} (r={r}) ожидалось {expected} ячеек, а найдено {tokens.Length}.");

            var qmin = Math.Max(-radius, -radius - r);
            for (var k = 0; k < tokens.Length; k++)
            {
                var token = tokens[k];
                var pos = new Vector2i(qmin + k, r);

                if (token == ".")
                    continue;

                if (token == "#")
                {
                    holes.Add(pos);
                    continue;
                }

                if (!Legend.TryGetValue(token, out var field))
                    throw new InvalidOperationException(
                        $"Mosaic ASCII: символ '{token}' (ряд r={r}) не найден в legend.");
                endpoints.Add(new MosaicEndpoint { Pos = pos, Field = field });
            }
        }

        Radius = radius;
        Endpoints = endpoints;
        Holes = holes;
    }
}

[DataDefinition]
public sealed partial class MosaicEndpoint
{
    [DataField]
    public Vector2i Pos;

    [DataField(required: true)]
    public ProtoId<ResearchFieldPrototype> Field;
}
