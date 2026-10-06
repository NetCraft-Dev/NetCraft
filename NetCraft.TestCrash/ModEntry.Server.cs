using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Logging;
using NetCraft.ModApi.Extension;
using NetCraft.ModApi.Wrapper;

namespace NetCraft.TestCrash;

//ModEntry server side
public sealed partial class ModEntry
{
    //Set by the command, consumed by the next LevelTick subscription
    //Kept static because subscriptions are registered once at startup
    private static bool _crashOnTick;

    //InitServer server-side subscriptions and command registration
    //Subscribe returns an unsubscribe handle; ignore it when subscribing once at startup
    static partial void InitServer()
    {
        ServerEvents.Started.Subscribe(_ => Log.Info("NetCraft.TestCrash server started"));

        //Player joined; the join packet sequence has finished so player state is safe to read
        ServerEvents.PlayerJoin.Subscribe(args =>
            Log.Info($"NetCraft.TestCrash player joined {args.ProfileName}"));

        //The kernel took a snapshot right before writing to disk; keep this callback cheap and safe
        ServerEvents.ChunkSaved.Subscribe(args =>
            Log.Debug($"NetCraft.TestCrash chunk saved {args.X},{args.Z}"));

        //Fires every level tick; used to inject a fatal exception into the tick loop on purpose
        //NcEvent.Publish does not catch, so this propagates into MinecraftServer.Tick and then
        //into the Run() catch, which writes a crash report, stops the server and exits the loop
        ServerEvents.LevelTick.Subscribe(_ =>
        {
            if (_crashOnTick)
            {
                _crashOnTick = false;
                throw new InvalidOperationException("TestCrash: fatal exception from LevelTick");
            }
        });

        //Command registration happens after every built-in command is in place
        //1-4 throw managed exceptions that bubble into the console command catch; 5 arms a fatal
        //exception on the next LevelTick so the server main loop crash handling can be observed
        ServerEvents.CommandRegister.Subscribe(args =>
            args.Register("netcraft.testcrash", "Throw a managed exception (1-4) or crash the server (5)", builder =>
                builder.Then(
                    RequiredArgumentBuilder<CommandSourceStack, int>
                        .Argument("kind", IntegerArgumentType.Integer(1, 5))
                        .Executes(context =>
                        {
                            //The typed argument, already validated to 1-5 by IntegerArgumentType
                            var kind = context.GetArgument<int>("kind");
                            var source = context.GetSource();

                            //5 is special: it does not throw here, it arms the next tick to throw
                            //from inside the server tick loop, which is the fatal path
                            if (kind == 5)
                            {
                                _crashOnTick = true;
                                source.SendSuccess("armed: the next LevelTick will throw");
                                return 1;
                            }

                            //Deliberately no try/catch: every branch throws and the exception
                            //propagates out of the command into the kernel
                            switch (kind)
                            {
                                case 1:
                                    ThrowInvalidOperation();
                                    break;
                                case 2:
                                    ThrowArgumentNull();
                                    break;
                                case 3:
                                    ThrowNullReference();
                                    break;
                                case 4:
                                    ThrowCustom();
                                    break;
                            }

                            //Only reached if the switch fell through, which cannot happen for 1-4
                            return 0;
                        }))));
    }

    //1: plain business exception, the baseline catchable managed exception
    static void ThrowInvalidOperation() =>
        throw new InvalidOperationException("TestCrash: invalid operation requested");

    //2: argument exception, exercises the parameter validation path
    static void ThrowArgumentNull() =>
        throw new ArgumentNullException("testArg", "TestCrash: argument must not be null");

    //3: null dereference, the classic runtime managed exception
    static void ThrowNullReference()
    {
        string? s = null;
        _ = s!.Length; // throws NullReferenceException
    }

    //4: user-defined exception, tests non-BCL exception handling
    static void ThrowCustom() =>
        throw new TestCrashException("TestCrash: custom exception triggered");
}

//A mod-defined exception type, used to verify the kernel handles non-BCL exception types
public sealed class TestCrashException : Exception
{
    public TestCrashException(string message) : base(message) { }
}