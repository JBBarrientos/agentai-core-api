using AgentAI.Modules.Conversations;

public class Ticket
{
    public int Id { get; set; }

    // --- ServiceNow identifiers ---

    /// <summary>
    /// ServiceNow sys_id (GUID). Used to fetch/update the record via REST API.
    /// e.g. "a83820b58f723300e7e16c7827bdeed2"
    /// </summary>
    public string SysId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable ticket number. e.g. "INC0000025"
    /// </summary>
    public string Number { get; set; } = string.Empty;

    // --- Display fields (frontend) ---

    /// <summary>
    /// short_description in ServiceNow — the ticket title/summary.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// description in ServiceNow — the full ticket body.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Incident state as integer: 1=New, 2=In Progress, 3=On Hold,
    /// 4=Resolved, 5=Closed, 6=Canceled
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// Human-readable label derived from State (e.g. "In Progress").
    /// Can be computed or stored from sysparm_display_value=true.
    /// </summary>
    public string StateLabel { get; set; } = string.Empty;

    /// <summary>
    /// Priority: 1=Critical, 2=High, 3=Moderate, 4=Low
    /// </summary>
    public int Priority { get; set; }

    public string PriorityLabel { get; set; } = string.Empty;

    /// <summary>
    /// Name of the ServiceNow caller/requester shown in the incident form.
    /// </summary>
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>
    /// Email of the ServiceNow caller/requester.
    /// </summary>
    public string CreatedByEmail { get; set; } = string.Empty;

    // --- Audit timestamps ---

    public DateTime OpenedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    // --- Local tracking ---

    /// <summary>When this record was last synced from ServiceNow.</summary>
    public DateTime LastSyncedAt { get; set; }
<<<<<<< HEAD
}
=======

    // --- Conversations ---
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
>>>>>>> origin/main
