using System.Runtime.InteropServices;
using System.Text;

namespace Scaffolding.NET.Helpers;

// 此文件的来源: https://github.com/PCL-Community/PCL.Core
// 源文件: ArgumentsBuilder.cs
// 协议: Apache-2.0
// 
// 此文件经过修改。
// 修改目的：支持跨平台处理。

internal class ArgumentsBuilder
{
    private readonly List<KeyValuePair<string, string?>> _args = [];

    /// <summary>
    /// 添加键值对参数（自动处理空格转义）
    /// </summary>
    /// <param name="key">参数名（不带前缀）</param>
    /// <param name="value">参数值</param>
    public ArgumentsBuilder Add(string key, string value)
    {
        if (key is null) throw new NullReferenceException(nameof(key));
        if (value is null) throw new NullReferenceException(nameof(value));
        _args.Add(new KeyValuePair<string, string?>(key, _handleValue(value)));
        return this;
    }

    /// <summary>
    /// 添加标志参数（无值参数）
    /// </summary>
    /// <param name="flag">标志名（不带前缀）</param>
    public ArgumentsBuilder AddFlag(string flag)
    {
        if (flag is null) throw new NullReferenceException(nameof(flag));
        _args.Add(new KeyValuePair<string, string?>(flag, null));
        return this;
    }

    /// <summary>
    /// 条件添加参数（仅当condition为true时添加）
    /// </summary>
    public ArgumentsBuilder AddIf(bool condition, string key, string value)
    {
        if (condition) Add(key, value);
        return this;
    }

    /// <summary>
    /// 条件添加标志（仅当condition为true时添加）
    /// </summary>
    public ArgumentsBuilder AddFlagIf(bool condition, string flag)
    {
        if (condition) AddFlag(flag);
        return this;
    }

    public enum PrefixStyle
    {
        /// <summary>
        /// 自动（单字符用-，多字符用--）
        /// </summary>
        Auto,
        /// <summary>
        /// 强制单横线
        /// </summary>
        SingleLine,
        /// <summary>
        /// 强制双横线
        /// </summary>
        DoubleLine
    }

    /// <summary>
    /// 构建参数字符串
    /// </summary>
    /// <param name="prefixStyle">前缀样式</param>
    public string GetResult(PrefixStyle prefixStyle = 0)
    {
        var sb = new StringBuilder();

        foreach (var arg in _args)
        {
            if (sb.Length > 0) sb.Append(' ');

            // 添加前缀
            switch (prefixStyle)
            {
                case PrefixStyle.SingleLine: // 强制单横线
                    sb.Append('-').Append(arg.Key);
                    break;
                case PrefixStyle.DoubleLine: // 强制双横线
                    sb.Append("--").Append(arg.Key);
                    break;
                default: // 自动判断
                    sb.Append(arg.Key.Length == 1 ? "-" : "--").Append(arg.Key);
                    break;
            }

            // 添加值（如果有）
            if (arg.Value is not null)
            {
                sb.Append('=')
                    .Append(arg.Value);
            }
        }

        return sb.ToString();
    }

    public override string ToString()
    {
        return GetResult();
    }

    /// <summary>
    /// 清空所有参数
    /// </summary>
    public void Clear() => _args.Clear();

    private static readonly char[] NeedQuoteChars = [' ', '=', '|', '"', '\\', '$'];

    // 转义包含空格的值（用双引号包裹）
    private static string _handleValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        // 判断是否需要引号
        var needsQuote = value.Any(ch => NeedQuoteChars.Contains(ch));
        if (!needsQuote)
            return value;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows：用双引号包裹，内部双引号和反斜杠需转义
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (var c in value)
            {
                if (c is '\\' or '"')
                    sb.Append('\\');
                sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
        else
        {
            // Unix：优先使用单引号；但如果里面有单引号，用 '\'' 分割拼接
            if (!value.Contains('\''))
                return $"'{value}'";

            var sb = new StringBuilder();
            sb.Append('\'');
            foreach (var c in value)
            {
                if (c == '\'')
                    sb.Append("'\\''"); // 结束 + 转义 + 重新开始
                else
                    sb.Append(c);
            }
            sb.Append('\'');
            return sb.ToString();
        }
    }
}