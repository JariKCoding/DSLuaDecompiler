using System.Collections.Generic;
using System.IO;
using CoDHVKDecompiler.Common.Extensions;
using CoDHVKDecompiler.Common.LuaConstant;
using CoDHVKDecompiler.Common.LuaFunction.Structures;
using CoDHVKDecompiler.Common.LuaOpCodeTable;

namespace CoDHVKDecompiler.Common.LuaFunction
{
    public class LuaFunctionT6 : LuaFunction
    {
        private readonly ILuaOpCodeTable _opCodeTable = new DefaultLuaOpCodeTable();
        
        public LuaFunctionT6(ILuaFile luaFile, BinaryReader reader) : base(luaFile, reader)
        {
        }

        protected override FunctionHeader ReadFunctionHeader()
        {
            var header = new FunctionHeader
            {
                UpvalCount = Reader.ReadInt32(),
                ParameterCount = Reader.ReadInt32(),
                UsesVarArg = Reader.ReadByte() == 1,
                RegisterCount = Reader.ReadInt32(),
                InstructionCount = Reader.ReadInt32()
            };
            // Add some padding
            var extra = 4 - (int)Reader.BaseStream.Position % 4;
            if (extra > 0 && extra < 4)
            {
                Reader.ReadBytes(extra);
            }

            return header;
        }

        protected override FunctionFooter ReadFunctionFooter()
        {
            return new FunctionFooter
            {
                Unknown1 = Reader.ReadInt32(),
                Unknown2 = Reader.ReadSingle(), // Unknown float
                SubFunctionCount = Reader.ReadInt32(),
            };
        }

        protected override Instruction ReadInstruction()
        {
            var instruction = new Instruction();
            // Reading the values attached to the instruction
            // A = 8 bits
            // C = 9 bits
            // B = 8 bits
            // OpCode = 7 bits
            instruction.A = Reader.ReadByte();

            int cValue = Reader.ReadByte();
            byte bValue = Reader.ReadByte();
            if (GetBit(bValue, 0) == 1)
            {
                instruction.ExtraCBit = true;
            }
            instruction.C = (uint) cValue;

            instruction.B = (uint) (bValue >> 1);
            byte flagsB = Reader.ReadByte();
            if (GetBit(flagsB, 0) == 1)
            {
                instruction.B += 128;
            }
            
            instruction.OpCode = LuaOpCode.HKS_OPCODE_UNK;

            if (_opCodeTable.TryGetValue(flagsB >> 1, out var opCode))
            {
                instruction.OpCode = opCode;
            }

            instruction.Bx = (uint) (instruction.B * 512 + instruction.C + (instruction.ExtraCBit ? 256 : 0));
            instruction.SBx = (int) (instruction.Bx - 65536 + 1);
            
            return instruction;
        }

        protected override List<ILuaConstant> ReadConstants()
        {
            var constants = new List<ILuaConstant>();

            var constantCount = Reader.ReadInt32();
            for (var i = 0; i < constantCount; i++)
            {
                constants.Add(new LuaConstantT6(Reader));
            }

            return constants;
        }

        protected override List<ILuaFunction> ReadChildFunctions()
        {
            var childFunctions = new List<ILuaFunction>();

            for (var i = 0; i < Footer.SubFunctionCount; i++)
            {
                childFunctions.Add(new LuaFunctionT6(LuaFile, Reader));
            }

            return childFunctions;
        }
        
        public byte GetBit(long input, int bit)
        {
            return (byte)((input >> bit) & 1);
        }
    }
}