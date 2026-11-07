using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>Undo and redo of a document's edits, kept as snapshots by <see cref="ObjectTreeDocumentViewModelBase"/>.</summary>
    public sealed class UndoTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-undo-" + Guid.NewGuid().ToString("N"));
        private readonly string _path;
        private readonly FormSchemaDocumentViewModel _document;

        public UndoTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            table.Fields!.Add("order_date", "Order Date", FieldDbType.Date);
            table.Fields!.Add("total", "Total", FieldDbType.Decimal);
            _path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, _path);
            _document = FormSchemaDocumentViewModel.Load(_path, SolutionContext.Empty);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private ObjectTreeNode TableNode =>
            _document.RootNode.Children.First(c => c.IsFolder && c.Value is FormTableCollection).Children[0];

        private ObjectTreeNode FieldNode(string fieldName) =>
            TableNode.Children.First(c => c.Value is FormField f && f.FieldName == fieldName);

        private string[] FieldOrder() => _document.Schema.Tables!["Order"].Fields!.Select(f => f.FieldName).ToArray();

        private void EditCaption(string fieldName, string caption)
        {
            _document.SelectedTreeNode = FieldNode(fieldName);
            ((FormField)_document.SelectedTreeNode.Value).Caption = caption;
            _document.OnPropertyEdited();
        }

        [Fact]
        [DisplayName("A newly opened document has nothing to undo or redo")]
        public void NewDocument_HasNoHistory()
        {
            Assert.False(_document.CanUndo);
            Assert.False(_document.CanRedo);
            Assert.False(_document.UndoCommand.CanExecute(null));
        }

        [Fact]
        [DisplayName("Undo puts a property edit back and the document is clean again; Redo applies it again")]
        public void UndoRedo_PropertyEdit_RestoresValueAndDirtyState()
        {
            EditCaption("total", "Grand Total");
            Assert.True(_document.IsDirty);

            _document.UndoCommand.Execute(null);
            Assert.Equal("Total", _document.Schema.Tables!["Order"].Fields!["total"].Caption);
            Assert.False(_document.IsDirty);
            Assert.True(_document.CanRedo);

            _document.RedoCommand.Execute(null);
            Assert.Equal("Grand Total", _document.Schema.Tables!["Order"].Fields!["total"].Caption);
            Assert.True(_document.IsDirty);
        }

        [Fact]
        [DisplayName("Undo removes an added field from the schema and the tree")]
        public void Undo_AddField_RemovesIt()
        {
            _document.SelectedTreeNode = TableNode;
            _document.AddFieldCommand.Execute(null);
            Assert.Equal(4, FieldOrder().Length);

            _document.UndoCommand.Execute(null);

            Assert.Equal(["sys_id", "order_date", "total"], FieldOrder());
            Assert.Equal(3, TableNode.Children.Count(c => c.Value is FormField));
            Assert.Same(_document.Schema.Tables!["Order"], TableNode.Value);
        }

        [Fact]
        [DisplayName("Delete asks nothing, and Undo brings the deleted field back where it was")]
        public void Undo_Delete_BringsTheFieldBack()
        {
            _document.SelectedTreeNode = FieldNode("order_date");
            _document.DeleteCommand.Execute(null);
            Assert.Equal(["sys_id", "total"], FieldOrder());
            Assert.Contains("order_date", _document.StatusText, StringComparison.Ordinal);

            Assert.Same(TableNode, _document.SelectedTreeNode);

            _document.UndoCommand.Execute(null);

            Assert.Equal(["sys_id", "order_date", "total"], FieldOrder());
            // The form and its master table share the label "Order - Order"; the table stays selected.
            Assert.Same(TableNode, _document.SelectedTreeNode);
        }

        [Fact]
        [DisplayName("Undo puts a moved field back in its place and keeps it selected")]
        public void Undo_Move_RestoresOrderAndSelection()
        {
            _document.SelectedTreeNode = FieldNode("sys_id");
            _document.MoveDownCommand.Execute(null);
            Assert.Equal(["order_date", "sys_id", "total"], FieldOrder());

            _document.UndoCommand.Execute(null);

            Assert.Equal(["sys_id", "order_date", "total"], FieldOrder());
            Assert.Equal("sys_id", Assert.IsType<FormField>(_document.SelectedTreeNode!.Value).FieldName);
        }

        [Fact]
        [DisplayName("A new edit after Undo drops the edits that could have been redone")]
        public void NewEdit_AfterUndo_ClearsRedo()
        {
            EditCaption("total", "A");
            EditCaption("total", "B");
            _document.UndoCommand.Execute(null);
            Assert.True(_document.CanRedo);

            EditCaption("sys_id", "C");

            Assert.False(_document.CanRedo);
            _document.UndoCommand.Execute(null);
            Assert.Equal("A", _document.Schema.Tables!["Order"].Fields!["total"].Caption);
        }

        [Fact]
        [DisplayName("An edit that changes nothing leaves the document clean and adds no step")]
        public void Edit_WithoutChange_StaysClean()
        {
            _document.SelectedTreeNode = FieldNode("total");

            _document.OnPropertyEdited();

            Assert.False(_document.IsDirty);
            Assert.False(_document.CanUndo);
        }

        [Fact]
        [DisplayName("The document is clean exactly at the step that was saved")]
        public async Task Save_ThenUndoAndRedo_TracksTheSavedStep()
        {
            EditCaption("total", "Saved");
            await _document.SaveCommand.ExecuteAsync(null);
            Assert.False(_document.IsDirty);

            _document.UndoCommand.Execute(null);
            Assert.True(_document.IsDirty);

            _document.RedoCommand.Execute(null);
            Assert.False(_document.IsDirty);
        }

        [Fact]
        [DisplayName("Undo keeps the tree's expanded nodes and selects the node at the same place")]
        public void Undo_KeepsExpansionAndSelection()
        {
            TableNode.IsExpanded = false;
            EditCaption("order_date", "Ordered On");

            _document.UndoCommand.Execute(null);

            Assert.False(TableNode.IsExpanded);
            Assert.Equal("order_date", Assert.IsType<FormField>(_document.SelectedTreeNode!.Value).FieldName);
        }

        [Fact]
        [DisplayName("The Undo and Redo buttons name their keys in the tooltips")]
        public void ToolTips_NameTheKeys()
        {
            Assert.Equal($"{LocalizationService.Current["Action_Undo"]} ({TreeShortcuts.Undo})", _document.UndoToolTip);
            Assert.Equal($"{LocalizationService.Current["Action_Redo"]} ({TreeShortcuts.Redo})", _document.RedoToolTip);
        }
    }
}
