using ExiledToolkit.Core;

namespace ExiledToolkit.Adapters;

/// <summary>The toast page's messages, read from the shared <see cref="PageInbox"/> and parsed for the toast ticker.</summary>
internal static class PageMessages
{
    private static readonly PageMailbox Mailbox = PageInbox.Listen(PageCommand.Prefix);

    /// <summary>Handed out when there is no message, which is most frames, so an empty look allocates nothing.</summary>
    private static readonly PageCommand Nothing = new PageCommand.Unknown("");

    public static bool TryTake(out PageCommand command)
    {
        if (Mailbox.TryTake(out var message))
        {
            command = PageCommand.Parse(message);

            return true;
        }

        command = Nothing;

        return false;
    }
}
