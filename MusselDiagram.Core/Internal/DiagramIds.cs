namespace MusselDiagram.Internal;

using System.Security.Cryptography;

internal static class DiagramIds
{
    public static string Create() => RandomNumberGenerator.GetHexString(16, lowercase: true);
}
