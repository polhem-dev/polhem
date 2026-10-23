using Polhem.Core;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Layouts
{
    /// <summary>
    /// Form layout generator.
    /// Converts a <see cref="FormSchema"/> into a single-record-mode <see cref="FormLayout"/>
    /// (master sections + detail grids).
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This is a design-time tool. It produces the starting point a definition editor
    /// saves as a <see cref="FormLayout"/> definition file, which is then reviewed and adjusted by hand.
    /// The runtime never calls it: a form renders the stored definition, and a missing definition
    /// is a configuration error rather than a cue to generate one. Generating at runtime would let
    /// the layout — the authority on what appears on screen — silently degrade into a projection of
    /// the schema that nobody reviewed and that is stored nowhere.
    /// </remarks>
    public static class FormLayoutGenerator
    {
        /// <summary>
        /// System fields that must be added to detail grids regardless of <see cref="FormField.Visible"/>.
        /// Grid controls require these for row binding and master association.
        /// </summary>
        private static readonly string[] s_gridIdentityFields =
            { SysFields.RowId, SysFields.MasterRowId };

        /// <summary>
        /// Generates a form layout from a form schema definition.
        /// </summary>
        /// <param name="schema">The form schema definition.</param>
        /// <param name="layoutId">The layout ID to assign.</param>
        /// <returns>The generated form layout.</returns>
        public static FormLayout Generate(FormSchema schema, string layoutId)
        {
            ArgumentNullException.ThrowIfNull(schema);

            var layout = new FormLayout
            {
                LayoutId = layoutId,
                ProgId = schema.ProgId,
                Caption = schema.DisplayName,
                ColumnCount = 2,
            };

            AddSections(schema, layout);
            AddDetails(schema, layout);

            return layout;
        }

        /// <summary>
        /// Adds to <paramref name="layout"/> the fields of <paramref name="schema"/> that
        /// <see cref="Generate"/> would lay out and <paramref name="layout"/> does not have yet,
        /// and the detail grids it lacks. Everything already in the layout stays as it is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This keeps a layout that was adjusted by hand in step with its schema after fields are
        /// added. A field is placed next to its neighbour in the schema: after the nearest earlier
        /// field the layout already shows, in that field's section, or else before the nearest later
        /// one. A field with neither goes to the end of the last section; a detail column goes before
        /// the hidden identity columns at the end of its grid.
        /// </para>
        /// <para>
        /// Fields are matched by name, ignoring case. Nothing is removed or changed: a field the schema
        /// no longer has stays in the layout, and so does a field whose type changed.
        /// </para>
        /// <para>
        /// IMPORTANT: The layout is changed in place. Do not pass a layout taken from the definition
        /// cache; clone it first.
        /// </para>
        /// </remarks>
        /// <param name="layout">The layout to complete.</param>
        /// <param name="schema">The form schema the layout belongs to.</param>
        /// <returns>
        /// The fields added, as <c>TableName.FieldName</c>, in the order they were added; empty when
        /// the layout already had them all.
        /// </returns>
        public static IReadOnlyList<string> Merge(FormLayout layout, FormSchema schema)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(schema);

            var generated = Generate(schema, layout.LayoutId);
            var added = new List<string>();
            MergeSections(layout, generated, schema.MasterTable?.TableName ?? string.Empty, added);
            MergeDetails(layout, generated, added);
            return added;
        }

        private static void MergeSections(FormLayout layout, FormLayout generated, string tableName, List<string> added)
        {
            if (generated.Sections!.Count == 0) return;
            var source = generated.Sections[0].Fields!.ToList();
            var sections = layout.Sections!;

            for (int i = 0; i < source.Count; i++)
            {
                if (FindField(sections, source[i].FieldName) is not null) continue;

                var (target, index) = FindAnchor(source, i, name => FindField(sections, name))
                    ?? (LastSection(layout, generated.Sections[0]).Fields!, -1);
                if (index < 0) target.Add(source[i]);
                else target.Insert(index, source[i]);
                added.Add($"{tableName}.{source[i].FieldName}");
            }
        }

        private static void MergeDetails(FormLayout layout, FormLayout generated, List<string> added)
        {
            foreach (var generatedGrid in generated.Details!.ToList())
            {
                var grid = layout.Details!.FirstOrDefault(g => StringUtilities.IsEquals(g.TableName, generatedGrid.TableName));
                if (grid is null)
                {
                    layout.Details!.Add(generatedGrid);
                    added.AddRange(generatedGrid.Columns!.Select(c => $"{generatedGrid.TableName}.{c.FieldName}"));
                    continue;
                }

                var source = generatedGrid.Columns!.ToList();
                var columns = grid.Columns!;
                for (int i = 0; i < source.Count; i++)
                {
                    if (IndexOf(columns, source[i].FieldName) >= 0) continue;

                    var anchor = FindAnchor<LayoutColumn, LayoutColumnCollection>(source, i,
                        name => IndexOf(columns, name) is var at and >= 0 ? (columns, at) : null);
                    int index = anchor?.Index ?? TrailingIdentityStart(columns);
                    columns.Insert(index, source[i]);
                    added.Add($"{grid.TableName}.{source[i].FieldName}");
                }
            }
        }

        /// <summary>
        /// Where to insert <c>source[i]</c>: after the nearest earlier item that <paramref name="find"/>
        /// locates, else before the nearest later one; <see langword="null"/> when it locates neither.
        /// </summary>
        private static (TList List, int Index)? FindAnchor<TItem, TList>(
            List<TItem> source, int i, Func<string, (TList List, int Index)?> find)
            where TItem : LayoutFieldBase
        {
            for (int j = i - 1; j >= 0; j--)
            {
                if (find(source[j].FieldName) is { } before) return (before.List, before.Index + 1);
            }
            for (int j = i + 1; j < source.Count; j++)
            {
                if (find(source[j].FieldName) is { } after) return after;
            }
            return null;
        }

        private static (LayoutFieldCollection List, int Index)? FindField(LayoutSectionCollection sections, string fieldName)
        {
            foreach (var section in sections)
            {
                int index = IndexOf(section.Fields!, fieldName);
                if (index >= 0) return (section.Fields!, index);
            }
            return null;
        }

        private static int IndexOf<T>(IList<T> items, string fieldName) where T : LayoutFieldBase
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (StringUtilities.IsEquals(items[i].FieldName, fieldName)) return i;
            }
            return -1;
        }

        private static LayoutSection LastSection(FormLayout layout, LayoutSection generated)
        {
            if (layout.Sections!.Count > 0) return layout.Sections[^1];
            var section = new LayoutSection
            {
                Name = generated.Name,
                Caption = generated.Caption,
                ShowCaption = generated.ShowCaption,
            };
            layout.Sections.Add(section);
            return section;
        }

        /// <summary>The index of the first of the hidden identity columns at the end of a grid.</summary>
        private static int TrailingIdentityStart(LayoutColumnCollection columns)
        {
            int index = columns.Count;
            while (index > 0 && !columns[index - 1].Visible
                && s_gridIdentityFields.Any(s => StringUtilities.IsEquals(s, columns[index - 1].FieldName)))
            {
                index--;
            }
            return index;
        }

        /// <summary>
        /// Adds the master area as a single default section "Main".
        /// </summary>
        private static void AddSections(FormSchema schema, FormLayout layout)
        {
            var master = schema.MasterTable;
            if (master?.Fields == null) return;

            var section = new LayoutSection
            {
                Name = "Main",
                Caption = master.DisplayName,
                // The master section caption repeats the form's own name, which the host already
                // frames (tab header / window title). Keep the caption as metadata but do not
                // render it on the single-record surface. Detail sections keep their captions.
                ShowCaption = false,
            };

            var covered = CollectCoveredDisplayFields(master);
            foreach (var field in master.Fields.Where(f => ShouldEmit(f, covered)))
                section.Fields!.Add(LayoutColumnFactory.ToField(field));

            if (section.Fields!.Count > 0)
                layout.Sections!.Add(section);
        }

        /// <summary>
        /// Adds one detail grid for each non-master table.
        /// </summary>
        private static void AddDetails(FormSchema schema, FormLayout layout)
        {
            if (schema.Tables == null) return;

            foreach (var table in schema.Tables.Where(t => t != schema.MasterTable))
            {
                if (table.Fields == null) continue;

                var grid = new LayoutGrid
                {
                    TableName = table.TableName,
                    Caption = table.DisplayName,
                    AllowActions = GridControlAllowActions.All,
                };

                // 1. Emitted fields (relation lookup editors + visible plain fields)
                var covered = CollectCoveredDisplayFields(table);
                foreach (var field in table.Fields.Where(f => ShouldEmit(f, covered)))
                    grid.Columns!.Add(LayoutColumnFactory.ToColumn(field));

                // 2. System fields required for grid binding (whitelist), hidden in layout
                foreach (var sysName in s_gridIdentityFields.Where(s =>
                    table.Fields.Contains(s) &&
                    !grid.Columns!.Any(c => StringUtilities.IsEquals(c.FieldName, s))))
                {
                    var col = LayoutColumnFactory.ToColumn(table.Fields[sysName]);
                    col.Visible = false;
                    grid.Columns!.Add(col);
                }

                if (grid.Columns!.Count > 0)
                    layout.Details!.Add(grid);
            }
        }

        /// <summary>
        /// Collects the fields expressed through a relation field's composite lookup
        /// display (its effective <c>DisplayFields</c>); they are not generated as
        /// separate layout fields.
        /// </summary>
        private static HashSet<string> CollectCoveredDisplayFields(FormTable table)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in table.Fields!.Where(f => StringUtilities.IsNotEmpty(f.RelationProgId)))
            {
                foreach (var name in field.GetDisplayFields())
                    covered.Add(name);
            }
            return covered;
        }

        /// <summary>
        /// Whether the field gets its own layout field. A relation field always renders
        /// — its lookup editor carries the display fields, and the intuitive authoring
        /// marks the raw row-id value invisible (<c>Visible=false</c>) because the raw
        /// Guid is never shown as data. Plain fields follow <see cref="FormField.Visible"/>
        /// and skip the ones a lookup display already covers; uncovered relation display
        /// fields (e.g. extra <c>ref_*</c> projections) still render on their own.
        /// </summary>
        private static bool ShouldEmit(FormField field, HashSet<string> covered)
        {
            if (StringUtilities.IsNotEmpty(field.RelationProgId)) return true;
            if (!field.Visible) return false;
            return !covered.Contains(field.FieldName);
        }
    }
}
