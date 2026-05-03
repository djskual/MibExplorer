namespace MibExplorer.Models.Coding;

public sealed class CodingByte
{
    public int Index { get; init; }

    public byte Value { get; init; }

    public string Hex => Value.ToString("X2");

    public string Binary => Convert.ToString(Value, 2).PadLeft(8, '0');

    public bool Bit0 => (Value & 0x01) != 0;
    public bool Bit1 => (Value & 0x02) != 0;
    public bool Bit2 => (Value & 0x04) != 0;
    public bool Bit3 => (Value & 0x08) != 0;
    public bool Bit4 => (Value & 0x10) != 0;
    public bool Bit5 => (Value & 0x20) != 0;
    public bool Bit6 => (Value & 0x40) != 0;
    public bool Bit7 => (Value & 0x80) != 0;
}