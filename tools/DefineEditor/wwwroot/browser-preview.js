// The browser preview's "Field names" option: an empty editor shows its field name, as in the editor's own preview.
// The ids of the inputs are "<prefix>-<field name>" (FormLayoutPresenter.IdPrefix). Names written here are marked,
// so turning the option off removes them and leaves a placeholder the editor rendered itself.
window.polhemBrowserPreview = {
    showFieldNames: function (prefix, show) {
        document.querySelectorAll('[data-polhem-field-name]').forEach(function (e) {
            e.removeAttribute('placeholder');
            delete e.dataset.polhemFieldName;
        });
        if (!show) return;
        document.querySelectorAll('[id^="' + prefix + '-"]').forEach(function (e) {
            if (e.getAttribute('placeholder')) return;
            e.setAttribute('placeholder', e.id.substring(prefix.length + 1));
            e.dataset.polhemFieldName = '';
        });
    },

    // The editor and the page share no object, only the layout's order: a part is named by its position among the
    // sections or the details, and a field or a column by its position in it (PreviewTarget). The classes are the
    // ones both form presenters and grids draw. A column's header class sits on the header cell in the MudBlazor grid
    // and on an element inside it in the Fluent UI grid, so a column is found and outlined by the cell that holds it.
    followClicks: function (dotnet) {
        this.dotnet = dotnet;
        if (this.following) return;
        this.following = true;
        document.addEventListener('pointerdown', (e) => {
            var target = this.targetOf(e.target);
            if (target && this.dotnet) this.dotnet.invokeMethodAsync('SelectFromPreview', target.kind, target.index, target.item);
        }, true);
    },

    targetOf: function (element) {
        if (!(element instanceof Element)) return null;
        var detail = element.closest('.polhem-form-layout__detail');
        var cell = detail ? element.closest(this.headerCellSelector) : null;
        var item = cell ? this.headerCells(detail).indexOf(cell) : -1;
        if (item >= 0) return { kind: 'Column', index: this.indexIn('.polhem-form-layout__detail', detail), item: item };
        if (detail) return { kind: 'Grid', index: this.indexIn('.polhem-form-layout__detail', detail), item: -1 };
        var section = element.closest('.polhem-form-layout__section');
        if (!section) return null;
        var field = element.closest('.polhem-form-layout__field');
        var index = this.indexIn('.polhem-form-layout__section', section);
        if (field) {
            return { kind: 'Field', index: index,
                item: Array.prototype.indexOf.call(section.querySelectorAll('.polhem-form-layout__field'), field) };
        }
        return { kind: 'Section', index: index, item: -1 };
    },

    headerCellSelector: 'th, [role="columnheader"]',

    // The header cells of a detail grid's columns, in order: the cells that carry the header class or hold it.
    headerCells: function (detail) {
        return Array.prototype.map.call(detail.querySelectorAll('.polhem-grid__header'), (header) =>
            header.closest(this.headerCellSelector) || header);
    },

    indexIn: function (selector, element) {
        return Array.prototype.indexOf.call(document.querySelectorAll(selector), element);
    },

    outline: function (kind, index, item) {
        document.querySelectorAll('.browser-preview__selected').forEach(function (e) {
            e.classList.remove('browser-preview__selected');
        });
        var element = null;
        if (kind === 'Section' || kind === 'Field') {
            var section = document.querySelectorAll('.polhem-form-layout__section')[index];
            element = kind === 'Section' || !section ? section : section.querySelectorAll('.polhem-form-layout__field')[item];
        } else if (kind === 'Grid' || kind === 'Column') {
            var detail = document.querySelectorAll('.polhem-form-layout__detail')[index];
            element = kind === 'Grid' || !detail ? detail : this.headerCells(detail)[item];
        }
        if (element) element.classList.add('browser-preview__selected');
    }
};
