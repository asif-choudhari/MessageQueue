namespace MessageQueue.Internal;

/// <summary>
/// Application-property names stamped on outgoing messages and read back when routing
/// incoming ones.
/// </summary>
internal static class MessageHeaders
{
    /// <summary>Full CLR type name of the message payload.</summary>
    public const string MessageType = "MessageType";
}
