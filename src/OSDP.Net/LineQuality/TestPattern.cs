namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// Test pattern identifiers defined by the OSDP Line Quality Test Procedure, section 3.6.
    /// </summary>
    /// <remarks>
    /// Each pattern stresses a different characteristic of the RS-485 physical layer. The patterns
    /// are only meaningful when sent in clear text; a secure channel would randomize the payload
    /// and destroy the bit structure the patterns exist to exercise.
    /// </remarks>
    public enum TestPattern : byte
    {
        /// <summary>All bytes 0x00. Establishes a DC balance baseline.</summary>
        AllZeros = 0x00,

        /// <summary>All bytes 0xFF. Produces maximum current draw.</summary>
        AllOnes = 0x01,

        /// <summary>All bytes 0xAA (10101010). Worst case for signal transitions.</summary>
        AlternatingA = 0x02,

        /// <summary>All bytes 0x55 (01010101). Complement of <see cref="AlternatingA"/>.</summary>
        Alternating5 = 0x03,

        /// <summary>Byte[i] = i AND 0xFF. Covers the full range of byte values.</summary>
        Sequential = 0x04,

        /// <summary>Byte[i] = 1 shifted left by (i MOD 8). Isolates individual bits.</summary>
        WalkingOne = 0x05
    }
}
