using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NetCraft.TPGA.Config;

namespace NetCraft.TPGA.Web;

//KestrelConfig Kestrel 双端口 HTTPS 配置
//主端口 MainPort 跑 Yggdrasil 验证 API
//api 端口 ApiPort 跑 wss 与管理后台 同证书
//双端口共享同 X509Certificate2 TLS 握手后路由层共享
//类名避开 Microsoft.AspNetCore.Hosting.WebHostBuilder 重名
public static class KestrelConfig
{
    //ConfigureKestrelHttps 双端口绑定同证书 HTTPS
    //HTTP/1.1 用于 Yggdrasil API 阶段5 起 ApiPort 同时启用 WebSocket
    public static void ConfigureKestrelHttps(WebHostBuilderContext ctx, KestrelServerOptions options, TpgaConfig config, X509Certificate2 cert)
    {
        options.ListenAnyIP(config.MainPort, l => l.UseHttps(cert));
        options.ListenAnyIP(config.ApiPort, l =>
        {
            l.UseHttps(cert);
            //wss 复用同端口 后续 WssHandler 接管 阶段5 启用
            l.Protocols = HttpProtocols.Http1AndHttp2;
        });
    }
}
