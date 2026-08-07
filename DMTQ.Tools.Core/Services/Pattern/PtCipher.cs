namespace DMTQ.Tools.Core.Services.Pattern;

/// <summary>
/// Offline transformation used by encrypted DJMAX PT files.
/// </summary>
public sealed class PtCipher
{
    private static readonly uint[] MatrixA = [0, 0x9908B0DF];
    private readonly uint[] _mtState = new uint[625];
    private readonly byte[] _key1Bytes = new byte[8];
    private readonly byte[] _key2Bytes = new byte[8];
    private readonly uint[] _key1State = new uint[2];

    /// <summary>
    /// Decrypts an encrypted PT byte array. Applying the same transformation to
    /// a plaintext PT array produces the encrypted representation.
    /// </summary>
    /// <param name="input">The complete PT file bytes.</param>
    /// <returns>The transformed PT file bytes.</returns>
    public static byte[] Decrypt(ReadOnlySpan<byte> input)
        => new PtCipher().Transform(input.ToArray());

    /// <summary>Decrypts a PT file represented by an array.</summary>
    public static byte[] Decrypt(byte[] input)
        => Decrypt(input.AsSpan());

    private byte[] Transform(byte[] input)
    {
        if (input.Length < 24)
        {
            return input;
        }

        var header = new byte[24];
        Buffer.BlockCopy(input, 0, header, 0, 24);
        var data = new byte[input.Length - 24];
        Buffer.BlockCopy(input, 24, data, 0, data.Length);
        if (data.Length < 4)
        {
            return input;
        }

        var dataFlag = BitConverter.ToUInt32(data, 0);
        var encodeMode = dataFlag <= 10;

        FillData(header);
        var key2First = CalculateParam2();
        var key2Second = CalculateParam2();
        SetKey2(key2First, key2Second);
        _key1State[1] = CalculateCrc32(header);
        _key1State[0] = CalculateChecksum(header);
        SetKey1(_key1State[0], _key1State[1]);

        var plainBlock = new byte[8];
        var blockIndex = 0;
        for (var index = 0; index < data.Length; index++)
        {
            var originalByte = data[index];
            if (encodeMode)
            {
                plainBlock[blockIndex] = originalByte;
            }

            data[index] ^= (byte)(_key2Bytes[blockIndex] ^ _key1Bytes[blockIndex]);
            if (!encodeMode)
            {
                plainBlock[blockIndex] = data[index];
            }

            blockIndex++;
            if (blockIndex != 8)
            {
                continue;
            }

            UpdateKey1(plainBlock);
            key2First = CalculateParam2();
            key2Second = CalculateParam2();
            SetKey2(key2First, key2Second);
            blockIndex = 0;
        }

        var result = new byte[input.Length];
        Buffer.BlockCopy(header, 0, result, 0, 24);
        Buffer.BlockCopy(data, 0, result, 24, data.Length);
        return result;
    }

    private void SetKey1(uint first, uint second)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(first), 0, _key1Bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(second), 0, _key1Bytes, 4, 4);
    }

    private void SetKey2(uint first, uint second)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(first), 0, _key2Bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(second), 0, _key2Bytes, 4, 4);
    }

    private void UpdateKey1(byte[] plainBlock)
    {
        var first = _key1State[0];
        var second = _key1State[1];
        uint delta = 0;
        var blockFirst = BitConverter.ToUInt32(plainBlock, 0);
        var blockSecond = BitConverter.ToUInt32(plainBlock, 4);

        for (var round = 0; round < 32; round++)
        {
            delta = unchecked(delta - 1640531527);
            first = unchecked(first + ((blockSecond + (second >> 5)) ^ (delta + second) ^ (blockFirst + (second << 4))));
            second = unchecked(second + ((blockSecond + (first >> 5)) ^ (delta + first) ^ (blockFirst + (first << 4))));
        }

        _key1State[0] = first;
        _key1State[1] = second;
        SetKey1(first, second);
    }

    private void FillData(byte[] header)
    {
        _mtState[0] = 0x12BD6AA;
        for (_mtState[624] = 1; _mtState[624] < 624; _mtState[624]++)
        {
            var previous = _mtState[_mtState[624] - 1];
            _mtState[_mtState[624]] = unchecked(_mtState[624] + 1812433253 * (previous ^ (previous >> 30)));
        }

        var stateIndex = 1;
        var headerIndex = 0;
        for (var count = 624; count > 0; count--)
        {
            var headerValue = BitConverter.ToUInt32(header, 4 * headerIndex);
            var previous = _mtState[stateIndex - 1];
            _mtState[stateIndex] = unchecked((uint)headerIndex + headerValue +
                (_mtState[stateIndex] ^ (1664525 * (previous ^ (previous >> 30)))));
            stateIndex++;
            headerIndex++;
            if (stateIndex >= 624)
            {
                _mtState[0] = _mtState[623];
                stateIndex = 1;
            }

            if (headerIndex >= 6)
            {
                headerIndex = 0;
            }
        }

        for (var count = 623; count > 0; count--)
        {
            var previous = _mtState[stateIndex - 1];
            _mtState[stateIndex] = unchecked(
                (_mtState[stateIndex] ^ (1566083941 * (previous ^ (previous >> 30)))) - (uint)stateIndex);
            stateIndex++;
            if (stateIndex >= 624)
            {
                _mtState[0] = _mtState[623];
                stateIndex = 1;
            }
        }

        _mtState[0] = 0x80000000;
    }

    private uint CalculateParam2()
    {
        if (_mtState[624] >= 624)
        {
            int index;
            for (index = 0; index < 227; index++)
            {
                var value = (_mtState[index + 1] & 0x7FFFFFFF) | (_mtState[index] & 0x80000000);
                _mtState[index] = MatrixA[value & 1] ^ _mtState[index + 397] ^ (value >> 1);
            }

            while (index < 623)
            {
                var value = (_mtState[index + 1] & 0x7FFFFFFF) | (_mtState[index] & 0x80000000);
                _mtState[index] = MatrixA[value & 1] ^ _mtState[index - 227] ^ (value >> 1);
                index++;
            }

            var lastValue = (_mtState[0] & 0x7FFFFFFF) | (_mtState[623] & 0x80000000);
            _mtState[623] = MatrixA[lastValue & 1] ^ _mtState[396] ^ (lastValue >> 1);
            _mtState[624] = 0;
        }

        var valueToTemper = _mtState[_mtState[624]++];
        var tempered = valueToTemper ^ (valueToTemper >> 11);
        tempered ^= (tempered << 7) & 0x9D2C5680;
        tempered ^= (tempered << 15) & 0xEFC60000;
        return tempered ^ (tempered >> 18);
    }

    private static uint CalculateCrc32(byte[] data)
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320;
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 8; bit > 0; bit--)
            {
                value = (value & 1) == 1 ? (value >> 1) ^ polynomial : value >> 1;
            }

            table[index] = value;
        }

        uint crc = 0xFFFFFFFF;
        for (var index = 0; index < 24; index++)
        {
            var tableIndex = (byte)((crc & 0xFF) ^ data[index]);
            crc = (crc >> 8) ^ table[tableIndex];
        }

        return ~crc;
    }

    private static uint CalculateChecksum(byte[] data)
    {
        uint checksum = 0;
        for (var index = 0; index < 24; index++)
        {
            checksum += data[index];
        }

        return checksum;
    }
}
