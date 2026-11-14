namespace Polhem.Definition.Drafting.Session
{
    /// <summary>
    /// Copies a <see cref="FormDraft"/>, so a turn that fails or is cancelled can put the draft back as it was.
    /// </summary>
    internal static class DraftCopy
    {
        public static FormDraft Clone(FormDraft source)
        {
            var copy = new FormDraft();
            Restore(copy, source);
            return copy;
        }

        /// <summary>
        /// Makes <paramref name="target"/> hold what <paramref name="source"/> holds, keeping the target instance so
        /// whoever holds it sees the change.
        /// </summary>
        public static void Restore(FormDraft target, FormDraft source)
        {
            target.ProgId = source.ProgId;
            target.DisplayName = source.DisplayName;
            target.DisplayNameZhTw = source.DisplayNameZhTw;
            target.Menu.FolderId = source.Menu.FolderId;
            target.Menu.FolderCaption = source.Menu.FolderCaption;
            target.Menu.FolderCaptionZhTw = source.Menu.FolderCaptionZhTw;
            target.Menu.EntryCaption = source.Menu.EntryCaption;
            target.Menu.EntryCaptionZhTw = source.Menu.EntryCaptionZhTw;
            target.Tables.Clear();
            foreach (var table in source.Tables)
                target.Tables.Add(Clone(table));
        }

        private static DraftTable Clone(DraftTable source)
        {
            var copy = new DraftTable
            {
                TableName = source.TableName,
                DisplayName = source.DisplayName,
                DisplayNameZhTw = source.DisplayNameZhTw,
            };
            foreach (var field in source.Fields)
                copy.Fields.Add(Clone(field));
            return copy;
        }

        private static DraftField Clone(DraftField source)
        {
            var copy = new DraftField
            {
                FieldName = source.FieldName,
                Caption = source.Caption,
                CaptionZhTw = source.CaptionZhTw,
                DbType = source.DbType,
                MaxLength = source.MaxLength,
                ControlType = source.ControlType,
                ValueExpression = source.ValueExpression,
                Required = source.Required,
                SourceQuote = source.SourceQuote,
                Relation = source.Relation is null ? null : new DraftRelation
                {
                    ProgId = source.Relation.ProgId,
                    Confidence = source.Relation.Confidence,
                    Reason = source.Relation.Reason,
                },
            };
            foreach (var option in source.Options)
                copy.Options.Add(new DraftOption { Value = option.Value, Text = option.Text });
            return copy;
        }
    }
}
