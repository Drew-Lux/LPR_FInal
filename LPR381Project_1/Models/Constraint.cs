using System.Collections.Generic;

namespace LPR381_Project.Models
{
    public class Constraint
    {
        // Holds technological coefficients for this constraint
        public List<double> Coefficients { get; set; } = new List<double>();

        // Relation: "<=", ">=", or "="
        public string Relation { get; set; }

        // The right-hand-side value
        public double RHS { get; set; }
    }
}