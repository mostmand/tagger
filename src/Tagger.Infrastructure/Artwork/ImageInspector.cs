using Tagger.Application.Abstractions;

namespace Tagger.Infrastructure.Artwork;

public sealed class ImageInspector : IImageInspector
{
    public ImageInfo? Inspect(byte[] data)
    {
        if (data.Length < 2)
        {
            return null;
        }

        if (IsPng(data))
        {
            return data.Length >= 24
                ? new ImageInfo("image/png", ReadBigEndianInt32(data, 16), ReadBigEndianInt32(data, 20))
                : null;
        }

        if (IsJpeg(data))
        {
            return ReadJpeg(data);
        }

        if (IsGif(data))
        {
            return data.Length >= 10
                ? new ImageInfo("image/gif", ReadUInt16(data, 6), ReadUInt16(data, 8))
                : null;
        }

        if (IsBmp(data))
        {
            var width = data.Length >= 26 ? Math.Abs(BitConverter.ToInt32(data, 18)) : 0;
            var height = data.Length >= 26 ? Math.Abs(BitConverter.ToInt32(data, 22)) : 0;
            return new ImageInfo("image/bmp", width, height);
        }

        if (IsWebP(data))
        {
            var width = data.Length >= 30 && data[12] == 'V' && data[13] == 'P' && data[14] == '8' && data[15] == 'X'
                ? 1 + (data[24] | data[25] << 8 | data[26] << 16)
                : 1;
            var height = data.Length >= 30 && data[12] == 'V' && data[13] == 'P' && data[14] == '8' && data[15] == 'X'
                ? 1 + (data[27] | data[28] << 8 | data[29] << 16)
                : 1;
            return new ImageInfo("image/webp", width, height);
        }

        return null;
    }

    private static ImageInfo? ReadJpeg(byte[] data)
    {
        var index = 2;
        while (index + 9 < data.Length)
        {
            if (data[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = data[index + 1];
            if (marker is 0xD8 or 0xD9)
            {
                index += 2;
                continue;
            }

            var length = (data[index + 2] << 8) | data[index + 3];
            if (length < 2)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                var height = (data[index + 5] << 8) | data[index + 6];
                var width = (data[index + 7] << 8) | data[index + 8];
                return new ImageInfo("image/jpeg", width, height);
            }

            index += 2 + length;
        }

        return new ImageInfo("image/jpeg", 1, 1);
    }

    private static bool IsPng(byte[] data) =>
        data.Length >= 8 &&
        data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
        data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;

    private static bool IsJpeg(byte[] data) => data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    private static bool IsGif(byte[] data) =>
        data.Length >= 6 &&
        data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8' &&
        (data[4] == '7' || data[4] == '9') && data[5] == 'a';

    private static bool IsBmp(byte[] data) => data.Length >= 2 && data[0] == 'B' && data[1] == 'M';

    private static bool IsWebP(byte[] data) =>
        data.Length >= 12 &&
        data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
        data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P';

    private static int ReadBigEndianInt32(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static int ReadUInt16(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8);
}
