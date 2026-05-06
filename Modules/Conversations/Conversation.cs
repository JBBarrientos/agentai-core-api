using AgentAI.Modules.Messages;

namespace AgentAI.Modules.Conversations
{
public class Conversation
    {
        public int Id { get; set; }

        /// <summary>
        /// ServiceNow sys_id of the conversation record (sys_cs_conversation).
        /// </summary>
        public string SysId { get; set; } = string.Empty;

        // --- Relationship to Ticket (many conversations per ticket) ---
        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        /// <summary>
        /// Channel through which the conversation took place.
        /// e.g. "virtual_agent", "live_agent", "email"
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Conversation status: "active", "closed", "transferred", etc.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        // --- Audit timestamps ---
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime LastSyncedAt { get; set; }

        // --- Navigation ---
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
