using System.Text;
using Hl7.Fhir.Model;

namespace DtrCheck.Core.Fhir;

public static class DocumentTextDecoder
{
    public static string DecodeText(DocumentReference documentReference)
    {
        var texts = new List<string>();
        foreach (var content in documentReference.Content ?? [])
        {
            var data = content.Attachment?.Data;
            if (data is null || data.Length == 0) continue;
            try
            {
                texts.Add(Encoding.UTF8.GetString(data));
            }
            catch
            {
                // skip malformed attachment data
            }
        }
        return string.Join('\n', texts);
    }
}
