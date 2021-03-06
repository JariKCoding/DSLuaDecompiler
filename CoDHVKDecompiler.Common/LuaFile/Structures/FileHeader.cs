namespace CoDHVKDecompiler.Common.Structures
{
    public class FileHeader
    {
        public string Magic { get; set; }
        public byte LuaVersion { get; set; }
        public byte CompilerVersion { get; set; }
        public byte Endianness { get; set; }
        public byte SizeOfInt { get; set; }
        public byte SizeOfSizeT { get; set; }
        public byte SizeOfInstruction { get; set; }
        public byte SizeOfLuaNumber { get; set; }
        public byte IntegralFlag { get; set; }
        public byte GameByte { get; set; }
        public int ConstantTypeCount { get; set; }
    }
}