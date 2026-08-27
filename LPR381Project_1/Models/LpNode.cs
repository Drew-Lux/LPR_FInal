using System.Collections.Generic;

namespace LPR381_Project.Models
{
    public class LpNode
    {
        public int NodeID { get; set; }
        public LinearModel Model { get; set; }
        public double ParentZ { get; set; }

        // Track the branching history for output purposes
        public string BranchHistory { get; set; } = "Root";
    }
}