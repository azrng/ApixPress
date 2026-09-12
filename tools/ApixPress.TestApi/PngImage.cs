using System.IO.Compression;
using System.Text;

namespace ApixPress.TestApi;

/// <summary>程序内生成合法 PNG 图片（RGB 渐变），避免在仓库里存二进制测试图片。</summary>
internal static class PngImage
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CrcTable = CreateCrcTable();

    public static byte[] CreateRgb(int width, int height)
    {
        // 每行行首有 1 字节过滤器类型（0 = 无过滤）
        var stride = 1 + width * 3;
        var raw = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            raw[y * stride] = 0;
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + 1 + x * 3;
                raw[offset] = (byte)(x * 255 / (width - 1));
                raw[offset + 1] = (byte)(y * 255 / (height - 1));
                raw[offset + 2] = (byte)((x + y) * 255 / (width + height - 2));
            }
        }

        using var idat = new MemoryStream();
        // PNG 要求 zlib 封装：2 字节头 + deflate 数据 + adler32 校验
        idat.WriteByte(0x78);
        idat.WriteByte(0x01);
        using (var deflate = new DeflateStream(idat, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        WriteBigEndianUInt32(idat, Adler32(raw));

        using var output = new MemoryStream();
        output.Write(Signature);
        output.Write(Chunk("IHDR", PackIhdr(width, height)));
        output.Write(Chunk("IDAT", idat.ToArray()));
        output.Write(Chunk("IEND", []));
        return output.ToArray();
    }

    // IHDR 固定 13 字节：宽高各 4 字节，其后位深/颜色类型/压缩/过滤/隔行各 1 字节
    private static byte[] PackIhdr(int width, int height)
    {
        var data = new byte[13];
        data[0] = (byte)(width >> 24);
        data[1] = (byte)(width >> 16);
        data[2] = (byte)(width >> 8);
        data[3] = (byte)width;
        data[4] = (byte)(height >> 24);
        data[5] = (byte)(height >> 16);
        data[6] = (byte)(height >> 8);
        data[7] = (byte)height;
        data[8] = 8;
        data[9] = 2;
        return data;
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        using var chunk = new MemoryStream();
        WriteBigEndianUInt32(chunk, (uint)data.Length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        chunk.Write(typeBytes);
        chunk.Write(data);
        WriteBigEndianUInt32(chunk, Crc32([.. typeBytes, .. data]));
        return chunk.ToArray();
    }

    private static void WriteBigEndianUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        stream.Write(bytes);
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var byteValue in data)
        {
            a = (a + byteValue) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var byteValue in data)
        {
            crc = CrcTable[(crc ^ byteValue) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var entry = (uint)i;
            for (var bit = 0; bit < 8; bit++)
            {
                entry = (entry & 1) != 0 ? 0xEDB88320 ^ (entry >> 1) : entry >> 1;
            }

            table[i] = entry;
        }

        return table;
    }
}
