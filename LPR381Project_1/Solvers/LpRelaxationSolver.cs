using System;
using System.Collections.Generic;
using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    // Result of solving the continuous LP relaxation of a model (integer/binary
    // restrictions are ignored except that "bin" variables get an implicit 0<=x<=1 bound).
    public class LpRelaxationResult
    {
        public bool IsFeasible { get; set; }
        public bool IsUnbounded { get; set; }
        public double Z { get; set; }
        public double[] Variables { get; set; }
        public double[,] Tableau { get; set; }
        public List<int> BasicVariables { get; set; }
        public List<int> NonBasicVariables { get; set; }
        public int NumDecisionVars { get; set; }
        public int TotalVars { get; set; }

        // Definitions used to substitute an extra (slack/surplus) column back into
        // pure decision-variable terms: extraVar = ExtraConstant[col] + sum(ExtraCoefficients[col][j] * x_j)
        public Dictionary<int, double[]> ExtraCoefficients { get; set; }
        public Dictionary<int, double> ExtraConstant { get; set; }
    }

    // A general-purpose Big-M Simplex solver for LP relaxations, supporting mixed
    // <=, >=, = constraints. Used by Branch & Bound and Cutting Plane, both of which
    // need to solve sub-problems that include >= constraints (branching bounds / cuts).
    public static class LpRelaxationSolver
    {
        private const double BigM = 1_000_000.0;
        private const double Tolerance = 1e-7;

        public static LpRelaxationResult Solve(LinearModel model)
        {
            int n = model.ObjectiveCoefficients.Count;

            // Work on a copy of the constraints, adding implicit bin bounds (0 <= x <= 1)
            var constraints = new List<Constraint>();
            foreach (var c in model.Constraints)
            {
                constraints.Add(new Constraint { Coefficients = new List<double>(c.Coefficients), Relation = c.Relation, RHS = c.RHS });
            }
            for (int j = 0; j < n; j++)
            {
                if (model.SignRestrictions[j] == "bin")
                {
                    var bound = new Constraint { Relation = "<=", RHS = 1.0, Coefficients = new List<double>(new double[n]) };
                    bound.Coefficients[j] = 1.0;
                    constraints.Add(bound);
                }
            }

            // Normalize so every RHS is non-negative
            foreach (var c in constraints)
            {
                if (c.RHS < 0)
                {
                    for (int j = 0; j < c.Coefficients.Count; j++) c.Coefficients[j] = -c.Coefficients[j];
                    c.RHS = -c.RHS;
                    c.Relation = c.Relation == "<=" ? ">=" : c.Relation == ">=" ? "<=" : "=";
                }
            }

            int m = constraints.Count;
            var slackCol = new int[m];
            var surplusCol = new int[m];
            var artificialCol = new int[m];
            for (int i = 0; i < m; i++) slackCol[i] = surplusCol[i] = artificialCol[i] = -1;

            int extraCount = 0;
            for (int i = 0; i < m; i++)
            {
                if (constraints[i].Relation == "<=") { slackCol[i] = n + extraCount++; }
                else if (constraints[i].Relation == ">=") { surplusCol[i] = n + extraCount++; artificialCol[i] = n + extraCount++; }
                else { artificialCol[i] = n + extraCount++; }
            }

            int totalVars = n + extraCount;
            int cols = totalVars + 2; // Z column + RHS column
            int rows = m + 1;

            double[,] t = new double[rows, cols];

            double[] cost = new double[totalVars];
            for (int j = 0; j < n; j++)
                cost[j] = model.OptimizationType == "max" ? model.ObjectiveCoefficients[j] : -model.ObjectiveCoefficients[j];
            for (int i = 0; i < m; i++)
                if (artificialCol[i] != -1) cost[artificialCol[i]] = -BigM;

            var basic = new List<int>();
            for (int i = 0; i < m; i++)
            {
                var c = constraints[i];
                for (int j = 0; j < c.Coefficients.Count; j++) t[i + 1, j + 1] = c.Coefficients[j];
                if (slackCol[i] != -1) t[i + 1, slackCol[i] + 1] = 1.0;
                if (surplusCol[i] != -1) t[i + 1, surplusCol[i] + 1] = -1.0;
                if (artificialCol[i] != -1) t[i + 1, artificialCol[i] + 1] = 1.0;
                t[i + 1, cols - 1] = c.RHS;

                basic.Add(artificialCol[i] != -1 ? artificialCol[i] : slackCol[i]);
            }

            t[0, 0] = 1;
            for (int j = 0; j < totalVars; j++) t[0, j + 1] = -cost[j];

            // Canonicalize row0 so basic columns have a zero reduced cost
            for (int i = 0; i < m; i++)
            {
                int bcol = basic[i];
                double factor = t[0, bcol + 1];
                if (Math.Abs(factor) > 1e-12)
                    for (int j = 0; j < cols; j++) t[0, j] -= factor * t[i + 1, j];
            }

            var nonBasic = new List<int>();
            for (int j = 0; j < totalVars; j++) if (!basic.Contains(j)) nonBasic.Add(j);

            bool unbounded = false;
            for (int iter = 0; iter < 2000; iter++)
            {
                int enterCol = -1;
                double best = -Tolerance;
                for (int j = 1; j < cols - 1; j++)
                {
                    if (t[0, j] < best) { best = t[0, j]; enterCol = j; }
                }
                if (enterCol == -1) break; // optimal

                int leaveRow = -1;
                double minRatio = double.MaxValue;
                for (int i = 1; i < rows; i++)
                {
                    if (t[i, enterCol] > Tolerance)
                    {
                        double ratio = t[i, cols - 1] / t[i, enterCol];
                        if (ratio < minRatio - 1e-9) { minRatio = ratio; leaveRow = i; }
                    }
                }
                if (leaveRow == -1) { unbounded = true; break; }

                double pivot = t[leaveRow, enterCol];
                for (int j = 0; j < cols; j++) t[leaveRow, j] /= pivot;
                for (int i = 0; i < rows; i++)
                {
                    if (i == leaveRow) continue;
                    double factor = t[i, enterCol];
                    if (Math.Abs(factor) > 1e-12)
                        for (int j = 0; j < cols; j++) t[i, j] -= factor * t[leaveRow, j];
                }

                int enteringVar = enterCol - 1;
                int leavingVar = basic[leaveRow - 1];
                basic[leaveRow - 1] = enteringVar;
                nonBasic.Remove(enteringVar);
                nonBasic.Add(leavingVar);
            }

            var result = new LpRelaxationResult
            {
                Tableau = t,
                BasicVariables = basic,
                NonBasicVariables = nonBasic,
                NumDecisionVars = n,
                TotalVars = totalVars
            };

            if (unbounded)
            {
                result.IsFeasible = false;
                result.IsUnbounded = true;
                return result;
            }

            // Infeasible if an artificial variable remains basic with a positive value
            for (int i = 0; i < m; i++)
            {
                if (artificialCol[i] != -1 && basic.Contains(artificialCol[i]))
                {
                    int row = basic.IndexOf(artificialCol[i]) + 1;
                    if (t[row, cols - 1] > 1e-6)
                    {
                        result.IsFeasible = false;
                        return result;
                    }
                }
            }

            result.IsFeasible = true;

            double[] allVars = new double[totalVars];
            for (int i = 0; i < basic.Count; i++) allVars[basic[i]] = t[i + 1, cols - 1];

            result.Variables = new double[n];
            for (int j = 0; j < n; j++) result.Variables[j] = allVars[j];

            double z = t[0, cols - 1];
            result.Z = model.OptimizationType == "max" ? z : -z;

            // Build substitution definitions for every extra (slack/surplus/artificial) column,
            // so callers (e.g. Gomory cut generation) can rewrite a tableau row purely in
            // terms of the original decision variables.
            result.ExtraCoefficients = new Dictionary<int, double[]>();
            result.ExtraConstant = new Dictionary<int, double>();
            for (int i = 0; i < m; i++)
            {
                var c = constraints[i];
                double[] decisionCoefs = new double[n];
                for (int j = 0; j < n; j++) decisionCoefs[j] = j < c.Coefficients.Count ? c.Coefficients[j] : 0.0;

                if (slackCol[i] != -1)
                {
                    // slack = RHS - sum(a_j * x_j)
                    double[] def = new double[n];
                    for (int j = 0; j < n; j++) def[j] = -decisionCoefs[j];
                    result.ExtraCoefficients[slackCol[i]] = def;
                    result.ExtraConstant[slackCol[i]] = c.RHS;
                }
                if (surplusCol[i] != -1)
                {
                    // surplus = sum(a_j * x_j) - RHS
                    double[] def = new double[n];
                    for (int j = 0; j < n; j++) def[j] = decisionCoefs[j];
                    result.ExtraCoefficients[surplusCol[i]] = def;
                    result.ExtraConstant[surplusCol[i]] = -c.RHS;
                }
                if (artificialCol[i] != -1)
                {
                    // Artificial variables are 0 at any feasible solution
                    result.ExtraCoefficients[artificialCol[i]] = new double[n];
                    result.ExtraConstant[artificialCol[i]] = 0.0;
                }
            }

            return result;
        }
    }
}
