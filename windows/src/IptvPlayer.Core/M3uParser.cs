using System.Text.RegularExpressions;

namespace IptvPlayer.Core;

/// <summary>
/// Parser de listas M3U / M3U8 "extendidas" como las que usan los proveedores IPTV.
/// Soporta:
///  - <c>#EXTINF:-1 tvg-id="..." tvg-logo="..." group-title="...",Nombre</c>
///  - <c>#EXTGRP:Grupo</c>
///  - <c>#EXTVLCOPT:http-user-agent=...</c> y <c>#EXTVLCOPT:http-referrer=...</c>
///  - Cabeceras estilo Kodi en la URL: <c>http://host/stream|User-Agent=xxx&amp;Referer=yyy</c>
/// </summary>
public static class M3uParser
{
    private static readonly Regex AttrRegex = new("([\\w-]+)=\"([^\"]*)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static List<Channel> Parse(string text)
    {
        var channels = new List<Channel>();
        string? name = null;
        var attrs = new Dictionary<string, string>();
        string? group = null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Reset()
        {
            name = null;
            attrs = new Dictionary<string, string>();
            group = null;
            headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var reader = new StringReader(text);
        string? rawLine;
        while ((rawLine = reader.ReadLine()) != null)
        {
            var line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                Reset();
                var colon = line.IndexOf(':');
                var info = colon >= 0 ? line[(colon + 1)..] : "";
                var commaIdx = FirstCommaOutsideQuotes(info);
                var meta = commaIdx >= 0 ? info[..commaIdx] : info;
                name = commaIdx >= 0 ? info[(commaIdx + 1)..].Trim() : null;
                foreach (Match m in AttrRegex.Matches(meta))
                    attrs[m.Groups[1].Value.ToLowerInvariant()] = m.Groups[2].Value;
            }
            else if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
            {
                group = line[(line.IndexOf(':') + 1)..].Trim();
            }
            else if (line.StartsWith("#EXTVLCOPT:", StringComparison.OrdinalIgnoreCase))
            {
                var opt = line[(line.IndexOf(':') + 1)..];
                var eq = opt.IndexOf('=');
                var key = (eq >= 0 ? opt[..eq] : opt).Trim().ToLowerInvariant();
                var value = eq >= 0 ? opt[(eq + 1)..].Trim() : "";
                switch (key)
                {
                    case "http-user-agent": headers["User-Agent"] = value; break;
                    case "http-referrer":
                    case "http-referer": headers["Referer"] = value; break;
                }
            }
            else if (line.StartsWith('#'))
            {
                // #EXTM3U y otras directivas
            }
            else
            {
                var (url, urlHeaders) = SplitKodiHeaders(line);
                var finalName = NotBlank(name) ?? NotBlank(attrs.GetValueOrDefault("tvg-name")) ?? LastSegment(url);
                var allHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
                foreach (var (k, v) in urlHeaders) allHeaders[k] = v;
                channels.Add(new Channel(
                    Name: finalName,
                    Url: url,
                    Logo: NotBlank(attrs.GetValueOrDefault("tvg-logo")),
                    Group: NotBlank(attrs.GetValueOrDefault("group-title") ?? group),
                    TvgId: NotBlank(attrs.GetValueOrDefault("tvg-id")),
                    Headers: allHeaders));
                Reset();
            }
        }
        return channels;
    }

    private static string? NotBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static string LastSegment(string url)
    {
        var slash = url.LastIndexOf('/');
        return slash >= 0 ? url[(slash + 1)..] : url;
    }

    /// <summary>Primera coma fuera de comillas: separa los atributos del nombre (que puede tener comas).</summary>
    private static int FirstCommaOutsideQuotes(string s)
    {
        var inQuotes = false;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) return i;
        }
        return -1;
    }

    private static (string Url, Dictionary<string, string> Headers) SplitKodiHeaders(string line)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pipe = line.IndexOf('|');
        if (pipe < 0) return (line, headers);
        foreach (var part in line[(pipe + 1)..].Split('&'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var k = part[..eq].Trim();
            var v = part[(eq + 1)..].Trim();
            if (k.Length > 0) headers[k] = v;
        }
        return (line[..pipe], headers);
    }
}
