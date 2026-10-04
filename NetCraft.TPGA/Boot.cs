using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Config;
using NetCraft.TPGA.Crypto;
using NetCraft.TPGA.Db;
using NetCraft.TPGA.Localization;
using NetCraft.TPGA.Logging;
using NetCraft.TPGA.Protocol;
using NetCraft.TPGA.Storage;
using NetCraft.TPGA.Web;

namespace NetCraft.TPGA;

//Boot 启动编排 按顺序初始化各子系统
//顺序 配置→I18n→日志→DB→证书+验证→ASP.NET
//配置两阶段加载 PreLoadLanguage 取语言后初始化 I18n 再完整 Load 修复写回
public static class Boot
{
    //RunAsync 主入口 由 Program 调用
    public static async Task RunAsync(string[] args)
    {
        //阶段0 预日志 Log 未就绪用控制台
        Console.WriteLine("NetCraft.TPGA starting...");

        var configPath = Path.Combine(AppContext.BaseDirectory, "tpga.yaml");
        var localesDir = Path.Combine(AppContext.BaseDirectory, "i18n", "locales");

        //阶段1 预加载语言 仅读 language 字段 容忍错误
        var language = ConfigManager.PreLoadLanguage(configPath);
        //阶段2 I18n 初始化 加载语言包
        I18n.Init(language, localesDir);
        //阶段3 完整加载配置 反序列化+范围校验+触发修复重写
        var config = ConfigManager.Load(configPath);
        //PreLoad 用的语言与实际不同则重新初始化 I18n
        if (!string.Equals(language, config.Language, StringComparison.OrdinalIgnoreCase))
            I18n.Init(config.Language, localesDir);
        //阶段4 日志初始化
        Log.Init(config.LogDirectory);
        Log.Info("Boot", $"config loaded language={config.Language} mainPort={config.MainPort} apiPort={config.ApiPort}");

        //阶段5 数据库 主库(管理员+api账户) 与玩家库(游戏账户+令牌+档案+加入记录) 物理隔离
        var adminDbFactory = Database.CreateFactory(config.Database);
        var playerDbFactory = Database.CreateFactory(config.PlayerDatabase);
        SchemaInitializer.InitializeAdmin(adminDbFactory);
        SchemaInitializer.InitializePlayer(playerDbFactory);
        Log.Info("Boot", $"database ready main={config.Database} player={config.PlayerDatabase}");

        //阶段6 证书加载或自签生成 configPath 空则用自动路径 password 空则随机生成回写
        var (cert, certPwd, pwdGenerated) = CertificateManager.LoadOrGenerate(config.CertificatePath, config.CertificatePassword);
        if (pwdGenerated)
        {
            config.CertificatePassword = certPwd;
            ConfigManager.Save(configPath, config);
            Log.Info("Boot", "certificate password written back to config");
        }

        //阶段7 应用层 RSA 密钥 + 管理员引导 + 仓储分配
        //管理库 admin_accounts/api_accounts 玩家库 users/access_tokens/profiles/server_joins
        var rsaKeyProvider = new RsaKeyProvider();
        var adminRepo = new AdminRepository(adminDbFactory);
        var apiAccountRepo = new ApiAccountRepository(adminDbFactory);
        var userRepository = new UserRepository(playerDbFactory);
        var tokenRepo = new TokenRepository(playerDbFactory);
        var serverJoinRepo = new ServerJoinRepository(playerDbFactory);
        var profileRepo = new ProfileRepository(playerDbFactory);
        //textures 签名密钥持久化 贴图文件存储 textures 属性构建
        var profileKeyStore = new ProfileKeyStore();
        var textureStorage = new TextureStorage();
        var profileService = new ProfileService(profileKeyStore, profileRepo);
        AdminBootstrap.EnsureDefaultAdmin(adminRepo);
        Log.Info("Boot", "auth ready");

        //阶段8 ASP.NET 启动 双端口 HTTPS 主端口 Yggdrasil api 端口 wss+后台
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(rsaKeyProvider);
        builder.Services.AddSingleton(adminRepo);
        builder.Services.AddSingleton(apiAccountRepo);
        builder.Services.AddSingleton(userRepository);
        builder.Services.AddSingleton(tokenRepo);
        builder.Services.AddSingleton(serverJoinRepo);
        builder.Services.AddSingleton(profileRepo);
        builder.Services.AddSingleton(profileKeyStore);
        builder.Services.AddSingleton(textureStorage);
        builder.Services.AddSingleton(profileService);
        builder.Services.AddSingleton(cert);
        builder.Services.AddSingleton<YggdrasilService>();
        builder.Services.AddSingleton<AuthRateLimiter>();
        builder.Services.AddSingleton<ApiTokenStore>();
        builder.Services.AddSingleton<WssHandshake>();
        builder.Services.AddSingleton<WssHandler>();
        builder.Services.AddSingleton<AdminSessionStore>();
        builder.Services.AddSingleton<PlayerSessionStore>();
        builder.Services.AddSingleton<PendingOAuthStore>();
        //OAuth 提供商动态注册 创建 registry 填好后注册 DI 供端点查询
        var oauthRegistry = new OAuthProviderRegistry();
        OAuthSetup.RegisterHandlers(builder.Services.AddAuthentication(), config, oauthRegistry);
        builder.Services.AddSingleton(oauthRegistry);
        //Kestrel 双端口绑定同证书 主端口与 api 端口均 HTTPS
        builder.WebHost.ConfigureKestrel((ctx, opt) => KestrelConfig.ConfigureKestrelHttps(ctx, opt, config, cert));
        var app = builder.Build();
        //WebSocket api 端口 wss 复用 顶层启用
        app.UseWebSockets();
        //OAuth handler 处理 /signin-{key} 回调 在路由前调用 OnTicketReceived 拦截签 session
        app.UseAuthentication();
        //按端口分流 主端口开放玩家入口+Yggdrasil api 端口管理后台+wss
        //主端口 / → player.html 玩家公共信息 api 端口 / → admin.html 管理后台
        //双端口共享 Kestrel 但路由层隔离 静态资源与 i18n 翻译两端口都提供
        app.MapWhen(ctx => ctx.Connection.LocalPort == config.MainPort, mainBranch =>
        {
            //静态资源 /assets/* /fonts/* 给 player.html
            mainBranch.UseWebUIStaticFiles();
            //玩家 session cookie 认证 拦 /api/player/* login/logout/session 公开放行
            mainBranch.UsePlayerAuth();
            mainBranch.UseRouting();
            mainBranch.UseEndpoints(endpoints =>
            {
                //主端口 Yggdrasil 验证 API
                endpoints.MapYggdrasilEndpoints();
                //贴图分发 GET /textures/{hash} 与公钥 GET /yggdrasil/public-key
                endpoints.MapTextureEndpoints();
                //皮肤披风上传 PUT/DELETE /user/profile/{uuid}/skin
                endpoints.MapSkinEndpoints();
                //玩家自服务 API login/logout/session 后续阶段加 profile/password/sessions 等
                endpoints.MapPlayerEndpoints();
                //OAuth 登录与补全注册 providers/login/register 公开端点
                endpoints.MapOAuthEndpoints();
                //i18n 翻译端点 player 端未登录也要加载翻译
                endpoints.MapI18nEndpoints();
                //玩家入口 / → player.html 主端口 GET / 按 Accept 分流 metadata json 否则 player.html
                endpoints.MapWebUI("player.html", enableMetadata: true);
            });
        });
        app.MapWhen(ctx => ctx.Connection.LocalPort == config.ApiPort, apiBranch =>
        {
            //静态资源 在认证前短路 /assets/* /fonts/* 由 EmbeddedFileProvider 提供
            apiBranch.UseWebUIStaticFiles();
            //管理后台 session cookie 认证 拦 /api/* 数据请求
            apiBranch.UseAdminAuth();
            apiBranch.UseRouting();
            apiBranch.UseEndpoints(endpoints =>
            {
                //wss 入口 api 端口客户端连接后协商公钥鉴权颁发 UUID
                endpoints.MapGet("/ws", async (HttpContext ctx, WssHandler handler) => await handler.AcceptAsync(ctx));
                //i18n 翻译端点 管理后台登录页也要加载翻译
                endpoints.MapI18nEndpoints();
                //管理后台入口 / → admin.html SPA fallback 兜底未注册 GET
                endpoints.MapWebUI("admin.html");
                //管理后台 API session 登录登出改密 + api 账户与游戏用户增删改 + 语言切换
                endpoints.MapAdminEndpoints();
            });
        });
        Log.Info("Boot", $"web started mainPort={config.MainPort} apiPort={config.ApiPort}");
        await app.RunAsync();
    }
}
