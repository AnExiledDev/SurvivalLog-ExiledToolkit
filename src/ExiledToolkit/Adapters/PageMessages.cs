using ExiledToolkit.Core;

namespace ExiledToolkit.Adapters;

/// <summary>The toast page's messages, read from the shared <see cref="PageInbox"/> and parsed for the toast ticker.</summary>
internal static class PageMessages
{
    private static readonly PageMailbox Mailbox = PageInbox.Listen(PageCommand.Prefix);

    public static bool TryTake(out PageCommand command)
    {
        if (Mailbox.TryTake(out var message))
        {
            command = PageCommand.Parse(message);

            return true;
        }

        command = new PageCommand.Unknown("");

        return false;
    }
}
