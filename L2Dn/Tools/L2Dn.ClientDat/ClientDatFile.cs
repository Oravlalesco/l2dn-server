using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using L2Dn.IO;
using L2Dn.Packages.DatDefinitions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace L2Dn.ClientDat;

internal static class ClientDatFile
{
    private const int rsaBlockSize = 128;
    private const int rsaBlockBodySize = 124;
    private const int tailSize = 20;
    private const int tailCrcOffset = 12;
    private const string lineage2Ver413 = "Lineage2Ver413";

    public static T Read<T>(string path, out string rsaKey)
    {
        string fullPath = Path.GetFullPath(path);
        EncryptionKeys.RsaDecryption413 = new RsaKeyParameters(true, EncryptionKeys.RsaModulus413,
            EncryptionKeys.RsaPrivateExponent413);
        try
        {
            T data = DatReader.Read<T>(fullPath);
            rsaKey = "NCSoft legacy";
            return data;
        }
        catch (Exception legacyException)
        {
            EncryptionKeys.RsaDecryption413 = EncryptionKeys.RsaDecryption413L2EncDec;
            try
            {
                T data = DatReader.Read<T>(fullPath);
                rsaKey = "l2encdec";
                return data;
            }
            catch (Exception l2EncDecException)
            {
                throw new InvalidDataException($"Unable to read encrypted DAT file: {fullPath}",
                    new AggregateException(legacyException, l2EncDecException));
            }
        }
    }

    public static void WriteLineage2Ver413(string path, object data)
    {
        using MemoryStream rawStream = new();
        DatWriter.Write(rawStream, data);
        byte[] compressedData = Compress(rawStream.ToArray());
        byte[] encryptedData = EncryptRsa(compressedData, EncryptionKeys.RsaEncryption413);
        byte[] header = Encoding.Unicode.GetBytes(lineage2Ver413);

        byte[] result = new byte[header.Length + encryptedData.Length + tailSize];
        header.CopyTo(result, 0);
        encryptedData.CopyTo(result, header.Length);
        uint crc = CalculateCrc32(result.AsSpan(0, header.Length + encryptedData.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(result.Length - tailSize + tailCrcOffset), crc);

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, result);
    }

    private static byte[] Compress(byte[] input)
    {
        using MemoryStream output = new();
        Span<byte> size = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(size, checked((uint)input.Length));
        output.Write(size);
        using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, true))
            zlib.Write(input);

        return output.ToArray();
    }

    private static byte[] EncryptRsa(byte[] input, RsaKeyParameters parameters)
    {
        using MemoryStream output = new();
        byte[] block = new byte[rsaBlockSize];
        int inputOffset = 0;
        while (inputOffset < input.Length)
        {
            int chunkSize = Math.Min(input.Length - inputOffset, rsaBlockBodySize);
            Array.Clear(block);
            block[3] = checked((byte)chunkSize);
            int dataOffset = rsaBlockSize - AlignToFourBytes(chunkSize);
            input.AsSpan(inputOffset, chunkSize).CopyTo(block.AsSpan(dataOffset));

            BigInteger value = new BigInteger(1, block);
            byte[] encryptedBlock = value.ModPow(parameters.Exponent, parameters.Modulus).ToByteArrayUnsigned();
            if (encryptedBlock.Length > rsaBlockSize)
                throw new InvalidOperationException("The encrypted RSA block exceeds the protocol block size.");

            output.Write(new byte[rsaBlockSize - encryptedBlock.Length]);
            output.Write(encryptedBlock);
            inputOffset += chunkSize;
        }

        return output.ToArray();
    }

    private static int AlignToFourBytes(int value) => (value + 3) & ~3;

    private static uint CalculateCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }

        return ~crc;
    }
}
