using System.Net;

namespace Beauty_Aesthetics_WebPos.Components.Services.Clinical;

internal static class FollowUpNoteContent
{
    // Keep the medical fields readable in Senang while allowing this app to
    // restore them separately when the API only persists RtfMessage.
    private const string Prefix = "<!-- beauty-follow-up:v1 -->\n<p><strong>Symptoms</strong></p>\n<p>";
    private const string DiagnosesSeparator = "</p>\n<p><strong>Diagnoses</strong></p>\n<p>";
    private const string NotesSeparator = "</p>\n<p><strong>Notes</strong></p>\n<!-- beauty-follow-up:notes -->\n";

    public static string Encode(string notes, string symptoms, string diagnoses) =>
        Prefix + EncodeField(symptoms) + DiagnosesSeparator + EncodeField(diagnoses) + NotesSeparator + notes;

    public static (string Notes, string Symptoms, string Diagnoses) Decode(string? content)
    {
        var original = content ?? string.Empty;
        var normalized = original.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return (original, string.Empty, string.Empty);
        }

        var diagnosesStart = normalized.IndexOf(DiagnosesSeparator, Prefix.Length, StringComparison.Ordinal);
        if (diagnosesStart < 0)
        {
            return (original, string.Empty, string.Empty);
        }

        var diagnosesValueStart = diagnosesStart + DiagnosesSeparator.Length;
        var notesStart = normalized.IndexOf(NotesSeparator, diagnosesValueStart, StringComparison.Ordinal);
        if (notesStart < 0)
        {
            // Preserve legacy or incomplete content; never discard an unreadable note.
            return (original, string.Empty, string.Empty);
        }

        return (
            normalized[(notesStart + NotesSeparator.Length)..],
            DecodeField(normalized[Prefix.Length..diagnosesStart]),
            DecodeField(normalized[diagnosesValueStart..notesStart]));
    }

    public static string NormalizeField(string value) =>
        value.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string EncodeField(string value) =>
        WebUtility.HtmlEncode(NormalizeField(value))
            .Replace("\n", "<br />", StringComparison.Ordinal);

    private static string DecodeField(string value) =>
        WebUtility.HtmlDecode(value.Replace("<br />", "\n", StringComparison.Ordinal));
}
