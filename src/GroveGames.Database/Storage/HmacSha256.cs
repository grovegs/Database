using System.Buffers.Binary;

namespace GroveGames.Database.Storage;

internal sealed class HmacSha256
{
    public const int HashLength = 32;

    private const int BlockLength = 64;
    private const byte InnerPad = 0x36;
    private const byte OuterPad = 0x5c;

    private static readonly uint[] s_roundConstants =
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    ];

    private static readonly uint[] s_initialState =
    [
        0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
    ];

    private readonly uint[] _innerState;
    private readonly uint[] _outerState;
    private readonly uint[] _state;
    private readonly uint[] _schedule;
    private readonly byte[] _block;
    private readonly byte[] _innerHash;
    private int _blockLength;
    private long _length;

    public HmacSha256(ReadOnlySpan<byte> key)
    {
        _innerState = new uint[8];
        _outerState = new uint[8];
        _state = new uint[8];
        _schedule = new uint[64];
        _block = new byte[BlockLength];
        _innerHash = new byte[HashLength];

        Span<byte> padded = stackalloc byte[BlockLength];
        padded.Clear();

        if (key.Length > BlockLength)
        {
            Begin(s_initialState);
            Append(key);
            Finish(padded.Slice(0, HashLength));
        }
        else
        {
            key.CopyTo(padded);
        }

        Span<byte> pad = stackalloc byte[BlockLength];
        Prepare(padded, pad, InnerPad, _innerState);
        Prepare(padded, pad, OuterPad, _outerState);
    }

    public void Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> hash)
    {
        Begin(_innerState);
        _length = BlockLength;
        Append(first);
        Append(second);
        Finish(_innerHash);

        Begin(_outerState);
        _length = BlockLength;
        Append(_innerHash);
        Finish(hash);
    }

    private void Prepare(ReadOnlySpan<byte> key, Span<byte> pad, byte value, uint[] state)
    {
        for (var i = 0; i < BlockLength; i++)
        {
            pad[i] = (byte)(key[i] ^ value);
        }

        Begin(s_initialState);
        Append(pad);
        _state.AsSpan().CopyTo(state);
    }

    private void Begin(uint[] state)
    {
        state.AsSpan().CopyTo(_state);
        _blockLength = 0;
        _length = 0;
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        _length += data.Length;

        if (_blockLength > 0)
        {
            var count = Math.Min(BlockLength - _blockLength, data.Length);
            data.Slice(0, count).CopyTo(_block.AsSpan(_blockLength));
            _blockLength += count;
            data = data.Slice(count);

            if (_blockLength < BlockLength)
            {
                return;
            }

            Transform(_block);
            _blockLength = 0;
        }

        while (data.Length >= BlockLength)
        {
            Transform(data.Slice(0, BlockLength));
            data = data.Slice(BlockLength);
        }

        data.CopyTo(_block);
        _blockLength = data.Length;
    }

    private void Finish(Span<byte> hash)
    {
        var bitLength = _length * 8;
        _block[_blockLength++] = 0x80;

        if (_blockLength > BlockLength - 8)
        {
            _block.AsSpan(_blockLength).Clear();
            Transform(_block);
            _blockLength = 0;
        }

        _block.AsSpan(_blockLength, BlockLength - 8 - _blockLength).Clear();
        BinaryPrimitives.WriteInt64BigEndian(_block.AsSpan(BlockLength - 8), bitLength);
        Transform(_block);

        for (var i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(hash.Slice(i * 4), _state[i]);
        }
    }

    private void Transform(ReadOnlySpan<byte> block)
    {
        var schedule = _schedule;

        for (var i = 0; i < 16; i++)
        {
            schedule[i] = BinaryPrimitives.ReadUInt32BigEndian(block.Slice(i * 4));
        }

        for (var i = 16; i < 64; i++)
        {
            var low = schedule[i - 15];
            var high = schedule[i - 2];
            var sigma0 = RotateRight(low, 7) ^ RotateRight(low, 18) ^ (low >> 3);
            var sigma1 = RotateRight(high, 17) ^ RotateRight(high, 19) ^ (high >> 10);
            schedule[i] = schedule[i - 16] + sigma0 + schedule[i - 7] + sigma1;
        }

        var state = _state;
        var a = state[0];
        var b = state[1];
        var c = state[2];
        var d = state[3];
        var e = state[4];
        var f = state[5];
        var g = state[6];
        var h = state[7];

        for (var i = 0; i < 64; i++)
        {
            var sum1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
            var choice = (e & f) ^ (~e & g);
            var first = h + sum1 + choice + s_roundConstants[i] + schedule[i];
            var sum0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
            var majority = (a & b) ^ (a & c) ^ (b & c);
            var second = sum0 + majority;
            h = g;
            g = f;
            f = e;
            e = d + first;
            d = c;
            c = b;
            b = a;
            a = first + second;
        }

        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
        state[4] += e;
        state[5] += f;
        state[6] += g;
        state[7] += h;
    }

    private static uint RotateRight(uint value, int count)
    {
        return (value >> count) | (value << (32 - count));
    }
}
