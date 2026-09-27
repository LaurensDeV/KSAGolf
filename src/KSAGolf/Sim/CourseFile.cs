using System.Text.Json;
using System.Text.Json.Serialization;

namespace KSAGolf;

/// <summary>
/// The sizes a course is built to. Provisional until judged in game: the lane has to suit a kitten
/// about 60 cm tall with a 45 cm putter.
/// </summary>
internal static class CourseSize
{
    public const double CellM = 1.0;

    /// <summary>The lane's inner width at every port, centred on the cell edge, so any two pieces meet flush.</summary>
    public const double LaneM = 0.8;

    /// <summary>
    /// One level's rise. A ramp is at least 0.15 steep so a ball stopped on it rolls back -- the ball rests
    /// on anything under <see cref="GolfBall.RestSlope"/> -- which over a 2 m ramp is 30 cm.
    /// </summary>
    public const double LevelM = 0.30;
}

/// <summary>
/// A course as saved: where it stands on which body, and its pieces. The floor is kept as a height above
/// the ground at the anchor rather than a radius, so a KSA update that reshapes the terrain moves the course
/// with it instead of burying it.
/// </summary>
internal sealed record CourseFile
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public string Name { get; init; } = "";
    public int Par { get; init; } = 2;
    public string Body { get; init; } = "";
    public double LatitudeDeg { get; init; }
    public double LongitudeDeg { get; init; }

    /// <summary>The grid's +J, clockwise from north.</summary>
    public double HeadingDeg { get; init; }

    public double FloorAboveGroundM { get; init; }
    public double CellM { get; init; } = CourseSize.CellM;
    public List<SavedPiece> Pieces { get; init; } = [];

    public sealed record SavedPiece(string Def, int I, int J, int Turns, bool Mirrored, int Level);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static CourseFile Of(Course course, CourseFile where) => where with
    {
        Version = CurrentVersion,
        Pieces = [.. course.Pieces.Select(p => new SavedPiece(p.Def.Id, p.Origin.I, p.Origin.J, p.Orientation.Quarter, p.Orientation.Mirrored, p.Level))],
    };

    /// <summary>The course, leaving out pieces this build does not know, which are named in <paramref name="unknown"/>.</summary>
    public Course ToCourse(out List<string> unknown)
    {
        unknown = [];
        List<Placed> pieces = [];
        foreach (SavedPiece saved in Pieces)
        {
            if (PieceCatalogue.Find(saved.Def) is not { } def)
            {
                unknown.Add(saved.Def);
                continue;
            }

            pieces.Add(new Placed(def, new Cell(saved.I, saved.J), new Orientation(saved.Turns, saved.Mirrored), saved.Level));
        }

        return new Course(pieces);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>The file read, or null with the reason: a course from a newer build is refused rather than half-read.</summary>
    public static CourseFile? FromJson(string json, out string why)
    {
        why = "";
        try
        {
            CourseFile? file = JsonSerializer.Deserialize<CourseFile>(json, Options);
            if (file is null) why = "empty";
            else if (file.Version > CurrentVersion) why = $"written by a newer KSAGolf (version {file.Version})";
            else return file;
        }
        catch (JsonException e)
        {
            why = e.Message;
        }

        return null;
    }
}
