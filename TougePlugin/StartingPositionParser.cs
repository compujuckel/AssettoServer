using System.Numerics;
using System.Text.RegularExpressions;
using Scriban;
using Serilog;

namespace TougePlugin;

public static class StartingAreaParser
{
    public static Dictionary<string, Vector3>[][] Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var areas = new List<Dictionary<string, Vector3>[]>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.StartsWith("[starting_area_"))
            {
                if (i + 4 >= lines.Length)
                    throw new FormatException($"Incomplete starting_area block at line {i + 1}");

                var slot1 = ParseSlot(lines[i + 1], lines[i + 2], "leader_pos", "leader_heading", i + 1);
                var slot2 = ParseSlot(lines[i + 3], lines[i + 4], "chaser_pos", "chaser_heading", i + 3);

                areas.Add([slot1, slot2]);
                i += 4; // Skip next 4 lines since they’ve been processed
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                throw new FormatException($"Unexpected line outside of a starting_area block at line {i + 1}: '{line}'");
            }
        }
        return areas.ToArray();
    }

    private static Dictionary<string, Vector3> ParseSlot(string posLine, string headingLine, string expectedPosKey, string expectedHeadingKey, int baseLine)
    {
        var posMatch = Regex.Match(posLine.Trim(), $@"^{expectedPosKey}\s*=\s*(-?\d+\.?\d*),\s*(-?\d+\.?\d*),\s*(-?\d+\.?\d*)$");
        var headingMatch = Regex.Match(headingLine.Trim(), $@"^{expectedHeadingKey}\s*=\s*(-?\d+\.?\d*)$");

        if (!posMatch.Success)
            throw new FormatException($"Expected '{expectedPosKey} = x, y, z' at line {baseLine + 1}, got: '{posLine}'");

        if (!headingMatch.Success)
            throw new FormatException($"Expected '{expectedHeadingKey} = float' at line {baseLine + 2}, got: '{headingLine}'");

        float x = float.Parse(posMatch.Groups[1].Value);
        float y = float.Parse(posMatch.Groups[2].Value);
        float z = float.Parse(posMatch.Groups[3].Value);
        float headingDeg = 64f + float.Parse(headingMatch.Groups[1].Value); // No idea why +64, but it works.
        float headingRad = headingDeg * MathF.PI / 180f;

        Vector3 direction = new(MathF.Sin(headingRad), 0f, MathF.Cos(headingRad));
        Log.Debug($"Direction vector: {direction}");

        return new Dictionary<string, Vector3>
        {
            ["Position"] = new Vector3(x, y, z),
            ["Direction"] = direction
        };
    }
}
