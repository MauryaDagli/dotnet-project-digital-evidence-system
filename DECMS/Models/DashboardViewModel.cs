using System.Collections.Generic;

namespace DECMS.Models;

public class DashboardViewModel
{
    public int TotalCases { get; set; }
    public int ActiveCases { get; set; }
    public int ClosedCases { get; set; }
    public int TotalEvidence { get; set; }
    public int EvidencePendingAnalysis { get; set; }
    public List<CustodyRecord> RecentCustodyTransfers { get; set; } = new List<CustodyRecord>();
}
