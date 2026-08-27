using System;
using System.IO;
using System.Linq;
using LPR381_Project.Models;

namespace LPR381_Project.Parsers
{
    public class InputParser
    {
        /// <summary>
        /// Reads the text file and maps it to the LinearModel object.
        /// </summary>
        public LinearModel ParseFile(string filePath)
        {
            // Validate file existence to prevent runtime crashes
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The specified input file was not found: {filePath}");
            }

            // Read all lines and remove any empty lines to avoid parsing errors
            string[] lines = File.ReadAllLines(filePath)
                                 .Where(l => !string.IsNullOrWhiteSpace(l))
                                 .ToArray();

            // A valid file must have at least 1 objective function, 1 constraint, and 1 sign restriction line
            if (lines.Length < 3)
            {
                throw new Exception("Invalid file format. Ensure the file contains the objective function, constraints, and sign restrictions.");
            }

            LinearModel model = new LinearModel();

            // 1. Parse the Objective Function (Line 1)
            string[] objParts = lines[0].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            model.OptimizationType = objParts[0].ToLower(); // Captures "max" or "min"

            for (int i = 1; i < objParts.Length; i++)
            {
                // C#'s double.Parse naturally handles strings like "+2" or "-5"
                if (double.TryParse(objParts[i], out double coef))
                {
                    model.ObjectiveCoefficients.Add(coef);
                }
            }

            // 2. Parse Constraints (Line 1 up to the second-to-last line)
            for (int i = 1; i < lines.Length - 1; i++)
            {
                string[] constraintParts = lines[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Constraint constraint = new Constraint();

                // The relation (=, <=, >=) and RHS are always the last two items in the line
                int relationIndex = constraintParts.Length - 2;

                // Extract technological coefficients dynamically based on array length
                for (int j = 0; j < relationIndex; j++)
                {
                    if (double.TryParse(constraintParts[j], out double cCoef))
                    {
                        constraint.Coefficients.Add(cCoef);
                    }
                }

                // Extract Relation and Right-Hand-Side
                constraint.Relation = constraintParts[relationIndex];
                if (double.TryParse(constraintParts[relationIndex + 1], out double rhs))
                {
                    constraint.RHS = rhs;
                }

                model.Constraints.Add(constraint);
            }

            // 3. Parse Sign Restrictions (The very last line of the file)
            string[] signParts = lines[lines.Length - 1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            model.SignRestrictions.AddRange(signParts);

            return model;
        }
    }
}