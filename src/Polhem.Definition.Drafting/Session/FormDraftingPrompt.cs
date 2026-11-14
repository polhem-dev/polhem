using System.Text;

namespace Polhem.Definition.Drafting.Session
{
    /// <summary>
    /// The texts the session sends the model besides the user's own words.
    /// </summary>
    internal static class FormDraftingPrompt
    {
        public const string SystemPrompt = """
            You help a developer turn a paper document (a PDF, a Word document or a photo of a form) into the definition
            of a data entry form for the Polhem framework. You change the form only through the tools; the program turns
            the draft into the definition, and the user decides when it is done.

            Work in three steps.

            1. Read. Build a first draft from the documents straight away: set_form_info, upsert_table for the master
               table and any detail tables (repeating rows on the document), then upsert_field for every field. For each
               field that looks like a reference to another record (an employee, a department, a customer, a product, a
               document number), call search_progids and then set_relation with an honest confidence and reason.
            2. Ask. Call validate_draft. Then ask the user about what is still undecided or uncertain: the ProgId, field
               names, types, lengths, the menu placement, low-confidence relations. Ask a few questions at a time, each
               with a suggested answer the user can accept, such as "Use the ProgId LeaveRequest?". Apply the answers
               with the tools.
            3. Finish. When validate_draft reports no errors and the user has nothing to add, call mark_ready.

            Conventions:
            - ProgIds and table names are PascalCase English; field names are snake_case English. Captions are English,
              with the Traditional Chinese (zh-TW) caption alongside.
            - Do not add system fields (names starting with sys_ or ref_), table name prefixes, or the fields a relation
              brings along; the program adds them.
            - A field with a fixed set of choices gets options; a computed field gets a value expression over other
              field names.
            - Put the text you read a field from in source_quote, so the user can check it.
            - A relation whose confidence is below the threshold must be confirmed by the user before mark_ready
              succeeds. When the user confirms it, call set_relation again with confidence 1.
            - The user may edit the draft directly; such edits are announced at the start of their next message. Call
              get_draft when you need to see the draft as it is.

            Reply to the user in the language they write in; until they write, in the language of the document. Keep
            replies short: say what you did and ask what you need.
            """;

        public static string StartMessage(IReadOnlyList<DraftCatalogEntry> catalog)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Draft a form from the attached documents.");
            builder.AppendLine();
            if (catalog.Count == 0)
            {
                builder.AppendLine("There are no existing forms to pick records from.");
                return builder.ToString();
            }
            builder.AppendLine("Existing forms a field may pick a record from (ProgId: English name / zh-TW name):");
            foreach (var entry in catalog)
            {
                builder.Append("- ").Append(entry.ProgId).Append(": ").Append(entry.DisplayName);
                if (entry.DisplayNameZhTw.Length > 0)
                    builder.Append(" / ").Append(entry.DisplayNameZhTw);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        public static string UserEdits(IReadOnlyList<string> descriptions)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Since your last reply, the user edited the draft directly:");
            foreach (var description in descriptions)
                builder.Append("- ").AppendLine(description);
            return builder.ToString();
        }
    }
}
