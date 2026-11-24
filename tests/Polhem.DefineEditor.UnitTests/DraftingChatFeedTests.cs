using System.ComponentModel;
using Polhem.Chat;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// The feed that turns what a drafting session reports into the conversation the chat views show. Each piece is applied
/// as it arrives (no redraw interval), so the entries can be read after each call.
/// </summary>
public sealed class DraftingChatFeedTests
{
    private static DraftingChatFeed Create()
        => new((name, subject) => $"{name}:{subject}", action => action(), TimeSpan.Zero);

    [Fact]
    [DisplayName("Leading whitespace opens no reply, and the pieces of a reply grow one entry")]
    public void AppendReply_LeadingWhitespace_OpensNoReply()
    {
        var feed = Create();
        feed.Begin("Hi");

        feed.AppendReply("  \n");
        Assert.Single(feed.Conversation.Entries);

        feed.AppendReply("Hel");
        feed.AppendReply("lo");
        Assert.Equal("Hello", Assert.IsType<AssistantReplyEntry>(feed.Conversation.Entries[^1]).Text);
    }

    [Fact]
    [DisplayName("Each request is counted, and its reply starts a new paragraph when nothing came between it and the previous reply")]
    public void BeginRequest_ConsecutiveReplies_AreSeparated()
    {
        var feed = Create();
        feed.Begin("Hi");
        feed.BeginRequest();
        feed.AppendReply("One.");
        feed.BeginRequest();
        feed.AppendReply("Two.");

        Assert.Equal("One.\n\nTwo.", Assert.Single(feed.Conversation.Entries.OfType<AssistantReplyEntry>()).Text);
        Assert.Equal(2, feed.Conversation.CurrentTurn!.Calls);
    }

    [Fact]
    [DisplayName("A tool shows with its title while it runs, completes, or fails with the reason it was refused")]
    public void StartAndEndTool_ShowsStatusAndRefusal()
    {
        var feed = Create();
        feed.Begin("Hi");

        feed.StartTool("upsert_field", "leave_type");
        var call = Assert.IsType<ToolCallEntry>(feed.Conversation.Entries[^1]);
        Assert.Equal("upsert_field:leave_type", call.Title);
        Assert.Equal(ToolCallStatus.Running, call.Status);
        feed.EndTool(refusal: null);
        Assert.Equal(ToolCallStatus.Completed, call.Status);

        feed.StartTool("remove_field", "missing");
        feed.EndTool("There is no field 'missing'.");
        var refused = Assert.IsType<ToolCallEntry>(feed.Conversation.Entries[^1]);
        Assert.Equal(ToolCallStatus.Failed, refused.Status);
        Assert.Equal("There is no field 'missing'.", refused.ErrorMessage);
    }

    [Fact]
    [DisplayName("The tokens of each call add up in the turn's summary, and entries of the application's own come before it")]
    public void Complete_UsageAndEntries_EndTheTurn()
    {
        var feed = Create();
        var note = new ErrorEntry("note");
        feed.Begin("Hi");
        feed.AddUsage(100, 10, 80);
        feed.AddUsage(50, 5, 0);

        feed.Complete(note);

        var entries = feed.Conversation.Entries;
        Assert.Same(note, entries[^2]);
        var usage = Assert.IsType<TurnSummaryEntry>(entries[^1]).Usage!;
        Assert.Equal(150, usage.InputTokenCount);
        Assert.Equal(15, usage.OutputTokenCount);
        Assert.Equal(80, usage.CachedInputTokenCount);
        Assert.False(feed.IsRunning);
    }

    [Fact]
    [DisplayName("A stopped turn keeps what arrived, marks the reply as stopped and shows the note after the summary")]
    public void Stop_KeepsTheReplyAndShowsTheNote()
    {
        var feed = Create();
        feed.Begin("Hi");
        feed.AppendReply("Partial");

        feed.Stop("The draft is as it was.");

        var entries = feed.Conversation.Entries;
        Assert.True(Assert.IsType<AssistantReplyEntry>(entries[1]).IsStopped);
        Assert.Equal(TurnOutcome.Stopped, Assert.IsType<TurnSummaryEntry>(entries[2]).Outcome);
        Assert.Equal("The draft is as it was.", Assert.IsType<ErrorEntry>(entries[3]).Message);
    }

    [Fact]
    [DisplayName("A failed turn shows the message it is given, and the next turn can begin")]
    public void Fail_ShowsTheMessageAndAllowsTheNextTurn()
    {
        var feed = Create();
        feed.Begin("Hi");

        feed.Fail("The network is down.", new HttpRequestException("socket closed"));

        Assert.Equal("The network is down.", Assert.Single(feed.Conversation.Entries.OfType<ErrorEntry>()).Message);
        feed.Begin("Again");
        Assert.True(feed.IsRunning);
    }
}
