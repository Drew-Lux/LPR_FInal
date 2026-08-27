using System.Collections.Generic;

namespace LPR381_Project.Models
{
    public class LinearModel
    {
        // "max" or "min"
        public string OptimizationType { get; set; }

        // Objective function coefficients in order
        public List<double> ObjectiveCoefficients { get; set; } = new List<double>();

        // All constraints parsed from the file
        public List<Constraint> Constraints { get; set; } = new List<Constraint>();

        // Sign restrictions: "+", "-", "urs", "int", "bin"
        public List<string> SignRestrictions { get; set; } = new List<string>();
    }
}