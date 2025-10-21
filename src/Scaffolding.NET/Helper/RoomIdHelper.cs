using System.Security.Cryptography;
using System.Text;

namespace Scaffolding.NET.Helper;

public static class RoomIdHelper
{
    private const string MapChars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>
    /// 判断房间码是否合法。
    /// </summary>
    /// <param name="roomId">房间码。</param>
    /// <returns>如果房间码合法那么返回 true，否则返回 false。</returns>
    public static bool IsValidRoomId(ReadOnlySpan<char> roomId)
    {
        // 标准的房间码格式为 U/NNNN-NNNN-SSSS-SSSS
        // 检验房间码长度
        if (roomId.Length != 21) return false;
        // 检验开头
        if (!roomId.StartsWith("U/")) return false;

        // 缓冲区
        Span<byte> buffer = stackalloc byte[16];
        
        var byteIndex = 0;
        for (var i = 2; i < roomId.Length; i++)
        {
            var c = roomId[i];
            if (c is '-' or '/') continue;

            var value = MapChars.IndexOf(c);
            if (value < 0) return false;

            buffer[byteIndex++] = (byte)value;
        }

        return IsDivisibleBy7(buffer);
    }

    /// <summary>
    /// 生成房间码。
    /// </summary>
    /// <returns>房间码。</returns>
    internal static string GenerateRoomId()
    {
        var roomId = string.Empty;
        while (!IsValidRoomId(roomId))
        {
            roomId = $"U/{CreateRandomString(4)}-{CreateRandomString(4)}-{CreateRandomString(4)}-{CreateRandomString(4)}";
        }

        return roomId;
    }

    /// <summary>
    /// 判断是否能被 7 整除。
    /// </summary>
    /// <param name="bytes">要判断的字节数组</param>
    /// <returns>如果能被 7 整除返回 true，否则返回 false。</returns>
    private static bool IsDivisibleBy7(ReadOnlySpan<byte> bytes)
    {
        var remainder = 0;
        var baseValue = 1; // 34^0 % 7 = 1

        foreach (var b in bytes)
        {
            // 假定 b 已经在 0..33 范围内

            remainder = (remainder + (b * baseValue) % 7) % 7;
            baseValue = (baseValue * 34) % 7;
        }

        return remainder == 0;
    }

    /// <summary>
    /// 以密码学安全的方式创建随机字符串。
    /// </summary>
    /// <param name="length">随机字符串长度</param>
    /// <returns>符合 Scaffolding 房间码要求的随机字符串。</returns>
    private static string CreateRandomString(int length)
    {
        var result = new StringBuilder(length);
        var buffer = new byte[1];

        using var rng = RandomNumberGenerator.Create();

        while (result.Length < length)
        {
            rng.GetBytes(buffer);
            var value = buffer[0];
            if (value >= 34 * (256 / 34)) continue; // 舍弃不均匀部分
            result.Append(MapChars[value % 34]);
        }

        return result.ToString();
    }
}