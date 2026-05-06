using AgentAI.Modules.Conversations;

namespace AgentAI.Modules.Messages
{
    public class Message
    {
        public int Id { get; set; }

        /// <summary>
        /// ServiceNow sys_id of the message record (sys_cs_message).
        /// </summary>
        public string SysId { get; set; } = string.Empty;

        // --- Relationship to Conversation (many messages per conversation) ---
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        /// <summary>
        /// Who sent the message: "user", "agent", "bot"
        /// </summary>
        public string SenderType { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the sender.
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// The message body.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Message type: "text", "attachment", "system_event", etc.
        /// </summary>
        public string MessageType { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }
        public DateTime LastSyncedAt { get; set; }
    }
}
