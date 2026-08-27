using System.Collections.Generic;

namespace LPR381_Project.Models
{
    public class SimplexResult
    {
        public bool IsOptimal { get; set; }
        public double[,] FinalTableau { get; set; }
        public List<int> BasicVariables { get; set; } = new List<int>();
        public List<int> NonBasicVariables { get; set; } = new List<int>();
    }
}