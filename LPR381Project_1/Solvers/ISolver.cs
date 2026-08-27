using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    public interface ISolver
    {
        // returns the final state of the algorithm
        SimplexResult Solve(LinearModel model, string outputFilePath);
    }
}