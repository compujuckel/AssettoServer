/*
   xxHash - Extremely Fast Hash algorithm
   Development source file for `xxh3`
   Copyright (C) 2019-present, Yann Collet.

   BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions are
   met:

       * Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
       * Redistributions in binary form must reproduce the above
   copyright notice, this list of conditions and the following disclaimer
   in the documentation and/or other materials provided with the
   distribution.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
   OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
   SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
   LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
   DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
   THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
   (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
   OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

   You can contact the author at :
   - xxHash source repository : https://github.com/Cyan4973/xxHash
*/

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace AssettoServer.Shared.Utils;

/// <summary>
/// CSP's XXH3 variant for Online Event keys. This is not the standard XXH3 implementation.
/// </summary>
public static class CspXXHash3
{
    private const ulong Prime64_1 = 11400714785074694791; /* 0b1001111000110111011110011011000110000101111010111100101010000111 */
    private const ulong Prime64_2 = 14029467366897019727; /* 0b1100001010110010101011100011110100100111110101001110101101001111 */
    private const ulong Prime64_3 = 1609587929392839161;  /* 0b0001011001010110011001111011000110011110001101110111100111111001 */
    private const ulong Prime64_4 = 9650029242287828579;  /* 0b1000010111101011110010100111011111000010101100101010111001100011 */
    private const ulong Prime64_5 = 2870177450012600261;  /* 0b0010011111010100111010110010111100010110010101100110011111000101 */

    private static ReadOnlySpan<uint> Key =>
    [
        0xb8fe6c39, 0x23a44bbe, 0x7c01812c, 0xf721ad1c,
        0xded46de9, 0x839097db, 0x7240a4a4, 0xb7b3671f,
        0xcb79e64e, 0xccc0e578, 0x825ad07d, 0xccff7221,
        0xb8084674, 0xf743248e, 0xe03590e6, 0x813a264c,
        0x3c2852bb, 0x91c300cb, 0x88d0658b, 0x1b532ea3,
        0x71644897, 0xa20df94e, 0x3819ef46, 0xa9deacd8,
        0xa8fa763f, 0xe39c343f, 0xf9dcbbc7, 0xc70b4f1d,
        0x8a51e04b, 0xcdb45931, 0xc89f7ec9, 0xd9787364,
        0xeac5ac83, 0x34d3ebc3, 0xc581a0ff, 0xfa1363eb,
        0x170ddd51, 0xb7f0da49, 0xd3165526, 0x29d4689e,
        0x2b16be58, 0x7d47a1fc, 0x8ff8b8d1, 0x7ad031ce,
        0x45cb3a8f, 0x95160428, 0xafd7fbca, 0xbb4b407e
    ];

    /// <summary>
    /// Only use this for CSP Online Event key generation. Use System.IO.Hashing for other hashes.
    /// </summary>
    public static long Hash64(ReadOnlySpan<byte> data)
    {
        unchecked
        {
            int length = data.Length;
            ReadOnlySpan<uint> key = Key;
            ulong hash;

            if (length <= 3)
            {
                if (length == 0)
                    return 0;

                uint first = (uint)data[0] + ((uint)data[length >> 1] << 8);
                uint last = (uint)length + ((uint)data[length - 1] << 2);
                hash = Avalanche((ulong)(first + key[0]) * (last + key[1]));
            }
            else if (length <= 8)
            {
                uint first = Read32(data, 0) + key[0];
                uint last = Read32(data, length - 4) + key[1];
                hash = Avalanche(Prime64_1 * (ulong)length + (ulong)first * last);
            }
            else if (length <= 16)
            {
                ulong first = Read64(data, 0) + Key64(key, 0);
                ulong last = Read64(data, length - 8) + Key64(key, 2);
                hash = Avalanche(Prime64_1 * (ulong)length + Mul128(first, last));
            }
            else if (length > 128)
            {
                hash = HashLong(data, key);
            }
            else
            {
                ulong acc = Prime64_1 * (ulong)length;
                if (length > 32)
                {
                    if (length > 64)
                    {
                        if (length > 96)
                        {
                            acc += Mix16B(data, 48, key, 24);
                            acc += Mix16B(data, length - 64, key, 28);
                        }

                        acc += Mix16B(data, 32, key, 16);
                        acc += Mix16B(data, length - 48, key, 20);
                    }

                    acc += Mix16B(data, 16, key, 8);
                    acc += Mix16B(data, length - 32, key, 12);
                }

                acc += Mix16B(data, 0, key, 0);
                acc += Mix16B(data, length - 16, key, 4);
                hash = Avalanche(acc);
            }

            return (long)hash;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Read32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Read64(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Key64(ReadOnlySpan<uint> key, int offset) =>
        key[offset] | ((ulong)key[offset + 1] << 32);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mul128(ulong left, ulong right)
    {
        UInt128 product = (UInt128)left * right;
        return unchecked((ulong)product + (ulong)(product >> 64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Avalanche(ulong hash)
    {
        unchecked
        {
            hash ^= hash >> 29;
            hash *= Prime64_3;
            return hash ^ (hash >> 32);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix16B(ReadOnlySpan<byte> data, int offset, ReadOnlySpan<uint> key, int keyOffset) =>
        Mul128(Read64(data, offset) ^ Key64(key, keyOffset),
            Read64(data, offset + 8) ^ Key64(key, keyOffset + 2));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong HashLong(ReadOnlySpan<byte> data, ReadOnlySpan<uint> key)
    {
        unchecked
        {
            Span<ulong> acc = stackalloc ulong[8]
            {
                0, Prime64_1, Prime64_2, Prime64_3, Prime64_4, Prime64_5, 0, 0
            };

            const int stripeLength = 64;
            const int stripesPerBlock = 16;
            const int blockLength = stripeLength * stripesPerBlock;
            int blocks = data.Length / blockLength;

            for (int block = 0; block < blocks; block++)
            {
                for (int stripe = 0; stripe < stripesPerBlock; stripe++)
                    Accumulate512(acc, data, block * blockLength + stripe * stripeLength, key, stripe * 2);

                ScrambleAcc(acc, key);
            }

            int remainingStripes = (data.Length % blockLength) / stripeLength;
            for (int stripe = 0; stripe < remainingStripes; stripe++)
                Accumulate512(acc, data, blocks * blockLength + stripe * stripeLength, key, stripe * 2);

            if ((data.Length & (stripeLength - 1)) != 0)
                Accumulate512(acc, data, data.Length - stripeLength, key, remainingStripes * 2);

            ulong result = (ulong)data.Length * Prime64_1;
            for (int i = 0; i < 8; i += 2)
                result += Mul128(acc[i] ^ Key64(key, i * 2), acc[i + 1] ^ Key64(key, i * 2 + 2));

            return Avalanche(result);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate512(Span<ulong> acc, ReadOnlySpan<byte> data, int offset,
        ReadOnlySpan<uint> key, int keyOffset)
    {
        unchecked
        {
            for (int i = 0; i < 8; i++)
            {
                int word = offset + i * 8;
                uint left = Read32(data, word) + key[keyOffset + i * 2];
                uint right = Read32(data, word + 4) + key[keyOffset + i * 2 + 1];
                acc[i] += (ulong)left * right + Read64(data, word);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ScrambleAcc(Span<ulong> acc, ReadOnlySpan<uint> key)
    {
        for (int i = 0; i < 8; i++)
        {
            ulong value = acc[i] ^ (acc[i] >> 47);
            acc[i] = (ulong)(uint)value * key[32 + i * 2]
                ^ (ulong)(uint)(value >> 32) * key[33 + i * 2];
        }
    }
}
