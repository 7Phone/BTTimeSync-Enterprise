namespace BTTimeSync.Core.Protocol;

/// <summary>
/// CRC16-CCITT 校验。
/// 多项式：0x1021
/// 初始值：0xFFFF
/// </summary>
public static class Crc16Ccitt
{
    private const ushort Polynomial = 0x1021;
    private const ushort InitialValue = 0xFFFF;

    /// <summary>
    /// 计算 CRC16-CCITT。
    /// </summary>
    /// <param name="data">需要计算的数据。</param>
    /// <returns>CRC16 校验值。</returns>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = InitialValue;

        foreach (var value in data)
        {
            crc ^= (ushort)(value << 8);

            for (var bit = 0; bit < 8; bit++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ Polynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        return crc;
    }
}