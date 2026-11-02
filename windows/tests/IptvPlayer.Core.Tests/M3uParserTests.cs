using IptvPlayer.Core;

namespace IptvPlayer.Core.Tests;

public class M3uParserTests
{
    [Fact]
    public void ParsesExtendedAttributes()
    {
        const string text = """
            #EXTM3U x-tvg-url="http://example.com/epg.xml"
            #EXTINF:-1 tvg-id="news.es" tvg-logo="http://example.com/logo.png" group-title="Noticias",Canal Noticias, HD
            http://example.com/live/1.m3u8
            #EXTINF:-1,Sin grupo
            http://example.com/live/2.ts
            """;

        var ch = M3uParser.Parse(text);
        Assert.Equal(2, ch.Count);
        Assert.Equal("Canal Noticias, HD", ch[0].Name);
        Assert.Equal("news.es", ch[0].TvgId);
        Assert.Equal("http://example.com/logo.png", ch[0].Logo);
        Assert.Equal("Noticias", ch[0].Group);
        Assert.Equal("http://example.com/live/1.m3u8", ch[0].Url);
        Assert.Equal("Sin grupo", ch[1].Name);
        Assert.Null(ch[1].Group);
    }

    [Fact]
    public void ParsesHeadersAndExtgrp()
    {
        const string text = """
            #EXTM3U
            #EXTINF:-1,Uno
            #EXTGRP:Deportes
            #EXTVLCOPT:http-user-agent=MiAgente/1.0
            http://example.com/a.m3u8
            #EXTINF:-1 group-title="Cine",Dos
            http://example.com/b.m3u8|User-Agent=Otro&Referer=http://ref.example.com/
            """;

        var ch = M3uParser.Parse(text);
        Assert.Equal("Deportes", ch[0].Group);
        Assert.Equal("MiAgente/1.0", ch[0].Headers["User-Agent"]);
        // Las cabeceras de un canal no se arrastran al siguiente.
        Assert.Single(ch[0].Headers);
        Assert.Equal("http://example.com/b.m3u8", ch[1].Url);
        Assert.Equal("Otro", ch[1].Headers["User-Agent"]);
        Assert.Equal("http://ref.example.com/", ch[1].Headers["Referer"]);
    }

    [Fact]
    public void HandlesBomCrlfAndMissingNames()
    {
        var text = "﻿#EXTM3U\r\n#EXTINF:-1 tvg-name=\"Por atributo\",\r\nhttp://example.com/a.ts\r\nhttp://example.com/live/canal.m3u8\r\n";
        var ch = M3uParser.Parse(text);
        Assert.Equal(2, ch.Count);
        Assert.Equal("Por atributo", ch[0].Name);
        Assert.Equal("canal.m3u8", ch[1].Name);
    }

    [Fact]
    public void XtreamBuildsPlaylistUrl()
    {
        var url = new PlaylistSource.Xtream("example.com:8080/", "user", "p&ss").PlaylistUrl();
        Assert.Equal("http://example.com:8080/get.php?username=user&password=p%26ss&type=m3u_plus&output=ts", url);
        var https = new PlaylistSource.Xtream("https://tv.example.com", "a b", "x").PlaylistUrl();
        Assert.Equal("https://tv.example.com/get.php?username=a%20b&password=x&type=m3u_plus&output=ts", https);
    }

    [Fact]
    public void ParsesLargePlaylistQuickly()
    {
        var sb = new System.Text.StringBuilder("#EXTM3U\n");
        for (var i = 0; i < 50_000; i++)
            sb.Append($"#EXTINF:-1 tvg-logo=\"http://l/{i}.png\" group-title=\"G{i % 40}\",Canal {i}\nhttp://example.com/{i}.ts\n");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ch = M3uParser.Parse(sb.ToString());
        Assert.Equal(50_000, ch.Count);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"tardó {sw.ElapsedMilliseconds} ms");
    }
}
