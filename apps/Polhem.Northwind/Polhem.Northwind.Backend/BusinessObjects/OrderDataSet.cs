using System.Data;
using Polhem.Core.Exceptions;

namespace Polhem.Northwind.Backend.BusinessObjects;

/// <summary>
/// Database-free <c>DataSet</c> operations for the order form: locating the master row and telling
/// whether the detail rows changed. The amounts, the total and the required-field checks are declared
/// in <c>Order.FormSchema.xml</c>; <see cref="OrderBO"/> owns the database-dependent number and status
/// rules.
/// </summary>
internal static class OrderDataSet
{
    // DataSet table names are FormTable.TableName; the master equals ProgId by framework invariant.
    public const string MasterTable = "Order";
    public const string DetailTable = "OrderDetail";

    /// <summary>Returns the single active (non-deleted) master row.</summary>
    public static DataRow MasterRow(DataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        var table = dataSet.Tables[MasterTable]
            ?? throw new UserMessageException("The order is missing its master record.");
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState != DataRowState.Deleted) { return row; }
        }
        throw new UserMessageException("The order is missing its master record.");
    }

    /// <summary>Whether any detail row is added, modified, or deleted.</summary>
    public static bool HasDetailEdits(DataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        var table = dataSet.Tables[DetailTable];
        if (table == null) { return false; }
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState != DataRowState.Unchanged) { return true; }
        }
        return false;
    }
}
