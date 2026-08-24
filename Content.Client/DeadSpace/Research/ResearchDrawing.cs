// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.Research;

public static class ResearchDrawing
{
    public static void DrawRing(DrawingHandleScreen handle, Vector2 center, float radius, Color color, float thickness, float uiScale)
    {
        var steps = Math.Max(1, (int) MathF.Round(thickness * uiScale));
        for (var i = 0; i < steps; i++)
            handle.DrawCircle(center, radius - i, color, false);
    }
}
