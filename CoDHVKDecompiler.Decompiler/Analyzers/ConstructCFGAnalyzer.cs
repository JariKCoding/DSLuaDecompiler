using System;
using System.Collections.Generic;
using System.Linq;
using CoDHVKDecompiler.Decompiler.CFG;
using CoDHVKDecompiler.Decompiler.IR.Functions;
using CoDHVKDecompiler.Decompiler.IR.Instruction;

namespace CoDHVKDecompiler.Decompiler.Analyzers
{
    public class ConstructCfgAnalyzer : IAnalyzer
    {
        public void Analyze(Function f)
        {
            f.IsControlFlowGraph = true;
            
            BasicBlock.IdCounter = 0;
            f.StartBlock = new BasicBlock();
            f.EndBlock = new BasicBlock();
            f.Blocks.Add(f.StartBlock);
            
            // These are used to connect jmps to their destinations later
            var labelBasicBlockMap = new Dictionary<Label, BasicBlock>();
            bool cullNextReturn = false;
            
            // First pass: Build all the basic blocks using labels, jmps, and rets as boundries
            var currentBlock = f.StartBlock;
            for (int i = 0; i < f.Instructions.Count; i++)
            {
                // Unconditional jumps just start a new basic block
                if (f.Instructions[i] is Jump {Conditional: false} jmp)
                {
                    currentBlock.Instructions.Add(jmp);
                    jmp.Block = currentBlock;
                    currentBlock = new BasicBlock();
                    f.Blocks.Add(currentBlock);
                    if (i + 1 < f.Instructions.Count && f.Instructions[i + 1] is Label l)
                    {
                        labelBasicBlockMap.Add(l, currentBlock);
                        i++;
                    }
                }
                // Conditional jumps has the following block as a successor
                else if (f.Instructions[i] is Jump {Conditional: true} jmp2)
                {
                    currentBlock.Instructions.Add(jmp2);
                    jmp2.Block = currentBlock;
                    var newBlock = new BasicBlock();
                    currentBlock.Successors.Add(newBlock);
                    newBlock.Predecessors.Add(currentBlock);
                    currentBlock = newBlock;
                    f.Blocks.Add(currentBlock);
                    if (i + 1 < f.Instructions.Count && f.Instructions[i + 1] is Label l)
                    {
                        if (l == jmp2.Dest)
                        {
                            // Empty if statement. Generate a dummy block so the true block and else block are different
                            currentBlock.Instructions.Add(new Jump(l));
                            currentBlock = new BasicBlock();
                            f.Blocks.Add(currentBlock);
                        }
                        labelBasicBlockMap.Add(l, currentBlock);
                        i++;
                    }
                }
                // Returns simply go directly to the end block, and starts a new basic block if not at the end
                else if (f.Instructions[i] is Return ret)
                {
                    currentBlock.Instructions.Add(ret);
                    ret.Block = currentBlock;
                    currentBlock.Successors.Add(f.EndBlock);
                    f.EndBlock.Predecessors.Add(currentBlock);
                    if (i + 1 < f.Instructions.Count)
                    {
                        currentBlock = new BasicBlock();
                        f.Blocks.Add(currentBlock);
                    }
                    if (i + 1 < f.Instructions.Count && f.Instructions[i + 1] is Label l)
                    {
                        labelBasicBlockMap.Add(l, currentBlock);
                        i++;
                    }
                }
                // Alternate return analysis for lua 5.0
                else if (f.Instructions[i] is Return ret2)
                {
                    // If a tailCall was done, an extra return that's not needed will always be generated by the Lua 5.0 compiler
                    if (!cullNextReturn)
                    {
                        if (ret2.IsTailReturn)
                        {
                            cullNextReturn = true;
                        }
                        currentBlock.Instructions.Add(ret2);
                        ret2.Block = currentBlock;
                    }
                    else
                    {
                        cullNextReturn = false;
                    }
                }
                // Other labels just start a new fallthrough basic block
                else if (f.Instructions[i] is Label l2)
                {
                    var newBlock = new BasicBlock();
                    currentBlock.Successors.Add(newBlock);
                    newBlock.Predecessors.Add(currentBlock);
                    currentBlock = newBlock;
                    f.Blocks.Add(currentBlock);
                    labelBasicBlockMap.Add(l2, currentBlock);
                }
                // Otherwise add instruction to the block
                else
                {
                    currentBlock.Instructions.Add(f.Instructions[i]);
                    f.Instructions[i].Block = currentBlock;
                }
            }
            
            // Second pass: Connect jumps to their basic blocks
            foreach (var t in f.Blocks)
            {
                if (t.Instructions.Any() && t.Instructions.Last() is Jump jmp)
                {
                    t.Successors.Add(labelBasicBlockMap[jmp.Dest]);
                    labelBasicBlockMap[jmp.Dest].Predecessors.Add(t);
                    jmp.BlockDest = labelBasicBlockMap[jmp.Dest];
                }
            }
            
            // Third pass: Remove unreachable blocks
            for (int b = 0; b < f.Blocks.Count(); b++)
            {
                // Begin block has no predecessors but shouldn't be removed because :)
                if (f.Blocks[b] == f.StartBlock)
                {
                    continue;
                }
                if (!f.Blocks[b].Predecessors.Any())
                {
                    foreach (var block in f.Blocks[b].Successors)
                    {
                        block.Predecessors.Remove(f.Blocks[b]);
                    }
                    f.Blocks.RemoveAt(b);
                    b--;
                }
            }
            
            // Forth pass: Merge blocks that have a single successor and that successor has a single predecessor
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int b = 0; b < f.Blocks.Count(); b++)
                {
                    if (f.Blocks[b].Successors.Count() == 1 && f.Blocks[b].Successors[0].Predecessors.Count() == 1 &&
                        (f.Blocks[b].Instructions.Last() is Jump || (b + 1 < f.Blocks.Count() && f.Blocks[b].Successors[0] == f.Blocks[b + 1])))
                    {
                        var curr = f.Blocks[b];
                        var succ = f.Blocks[b].Successors[0];
                        if (f.Blocks[b].Instructions.Last() is Jump)
                        {
                            curr.Instructions.RemoveAt(curr.Instructions.Count() - 1);
                        }
                        foreach (var inst in succ.Instructions)
                        {
                            inst.Block = curr;
                        }
                        curr.Instructions.AddRange(succ.Instructions);
                        curr.Successors = succ.Successors;
                        foreach (var s in succ.Successors)
                        {
                            for (int p = 0; p < s.Predecessors.Count(); p++)
                            {
                                if (s.Predecessors[p] == succ)
                                {
                                    s.Predecessors[p] = curr;
                                }
                            }
                        }
                        f.Blocks.Remove(succ);
                        b = Math.Max(0, b - 2);
                        changed = true;
                    }
                }
            }
            
            // Dangling no successor blocks should go to the end block (implicit return)
            for (int b = 0; b < f.Blocks.Count(); b++)
            {
                if (f.Blocks[b] == f.EndBlock)
                {
                    continue;
                }
                if (!f.Blocks[b].Successors.Any())
                {
                    f.Blocks[b].Successors.Add(f.EndBlock);
                }
            }

            f.Blocks.Add(f.EndBlock);
        }
    }
}