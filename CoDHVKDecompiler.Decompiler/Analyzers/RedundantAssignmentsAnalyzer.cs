using System.Linq;
using CoDHVKDecompiler.Decompiler.IR.Expression;
using CoDHVKDecompiler.Decompiler.IR.Functions;
using CoDHVKDecompiler.Decompiler.IR.Instruction;

namespace CoDHVKDecompiler.Decompiler.Analyzers
{
    public class RedundantAssignmentsAnalyzer : IAnalyzer
    {
        /// <summary>
        /// Super simple analysis that eliminates assignments of the form:
        /// RegA = RegA
        /// 
        /// These are often generated by the TEST instruction and elimination of these simplifies things for future passes
        /// </summary>
        public void Analyze(Function f)
        {
            for (int i = 0; i < f.Instructions.Count; i++)
            {
                if (f.Instructions[i] is Assignment assn && assn.Left.Count() == 1 && !assn.Left[0].HasIndex)
                {
                    if (assn.Right is IdentifierReference {HasIndex: false} reference && assn.Left[0].Identifier == reference.Identifier)
                    {
                        f.Instructions.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }
}