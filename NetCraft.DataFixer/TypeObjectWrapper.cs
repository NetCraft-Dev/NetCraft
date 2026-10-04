namespace NetCraft.DataFixer.Types;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetCraft.Codec;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;
using NetCraft.Util;
using T = NetCraft.DataFixer.Types;
using System.Runtime.CompilerServices;

//TypeObjectWrapper包装任意Type<A>实例为Type<object>
//解决C#严格泛型不变量下Type<int>与NamedType<A>:Type<Pair<string,A>>等无法强转Type<object>的问题
//对齐Java类型擦除语义委托虚方法到原实例
public sealed class TypeObjectWrapper : T.Type<object>
{
    private readonly object _inner;
    private readonly Type _innerType;
    private Codec<object>? _codecCache;
    private static readonly Dictionary<Type, MethodInfo> _methodCache = new();
    private static readonly object _cacheLock = new();
    //FindTypeInChildren委托缓存按(内部类型,FT,FR)避免重复构造Expression
    private static readonly ConcurrentDictionary<(Type, Type, Type), object> _findTypeInChildrenInvokerCache = new();

    public TypeObjectWrapper(object inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _innerType = inner.GetType();
    }

    public object Inner => _inner;

    //反射获取实例方法带缓存
    private MethodInfo GetMethod(string name, Type[]? paramTypes = null)
    {
        var key = (name, paramTypes?.Length ?? 0);
        var cacheKey = _innerType.GetHashCode() ^ name.GetHashCode() ^ (paramTypes?.Length ?? 0);
        lock (_cacheLock)
        {
            //简单缓存按(innerType, name, paramCount)
            foreach (var kv in _methodCache)
            {
                if (kv.Key == _innerType && kv.Value.Name == name)
                {
                    if (paramTypes == null || kv.Value.GetParameters().Length == paramTypes.Length)
                        return kv.Value;
                }
            }
            MethodInfo method = paramTypes == null
                ? _innerType.GetMethod(name)!
                : _innerType.GetMethod(name, paramTypes)!;
            _methodCache[_innerType] = method;
            return method;
        }
    }

    //反射调用原实例Template方法对齐原版Type.template()
    public override TypeTemplate BuildTemplate()
        => (TypeTemplate)GetMethod("Template").Invoke(_inner, null)!;

    //包装原Codec<A>为Codec<object>
    //原实例是Type<A>需先取Codec()再包装传Type实例会让GetMethod("Parse")找不到方法
    protected override Codec<object> BuildCodec()
        => _codecCache ??= new CodecAdapter(GetInnerCodec());

    //GetInnerCodec反射调用Type<A>.Codec()获取原Codec<A>实例
    private object GetInnerCodec()
    {
        var method = GetMethod("Codec");
        return method.Invoke(_inner, null)!;
    }

    //委托原实例Equals(o, ignoreRecursionPoints, checkIndex)
    //对方是wrapper时比较内部实例对方是裸Type<A>时直接比较
    public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
    {
        var method = GetMethod("Equals", new[] { typeof(object), typeof(bool), typeof(bool) });
        var unwrapped = o is TypeObjectWrapper w ? w._inner : o;
        return (bool)method.Invoke(_inner, new object?[] { unwrapped, ignoreRecursionPoints, checkIndex })!;
    }

    //Equals(object)委托到Equals(o,true,true)让RewriteCacheKey record按结构比较
    //不重写时record EqualityComparer调Object.Equals引用比较缓存永不命中
    public override bool Equals(object? obj) => Equals(obj, true, true);

    public override int GetHashCode() => _inner.GetHashCode();
    public override string? ToString() => _inner.ToString();

    //委托All返回RewriteResult<object,object>
    public override RewriteResult<object, object> All(object rule, bool recurse, bool checkIndex)
    {
        var method = GetMethod("All");
        var innerResult = method.Invoke(_inner, new object?[] { rule, recurse, checkIndex })!;
        return WrapRewriteResult(innerResult);
    }

    //委托One返回Optional<RewriteResult<object,object>>
    public override Optional<RewriteResult<object, object>> One(object rule)
    {
        var method = GetMethod("One");
        var innerOpt = method.Invoke(_inner, new object?[] { rule })!;
        return WrapOptionalRewriteResult(innerOpt);
    }

    //委托Everywhere返回Optional<RewriteResult<object,object>>
    public override Optional<RewriteResult<object, object>> Everywhere(object rule, object optimizationRule, bool recurse, bool checkIndex)
    {
        var method = GetMethod("Everywhere");
        var innerOpt = method.Invoke(_inner, new object?[] { rule, optimizationRule, recurse, checkIndex })!;
        return WrapOptionalRewriteResult(innerOpt);
    }

    //委托UpdateMu返回Type<object>递归包装
    public override Type<object> UpdateMu(RecursiveTypeFamily newFamily)
    {
        var method = GetMethod("UpdateMu");
        var innerResult = method.Invoke(_inner, new object?[] { newFamily })!;
        return WrapType(innerResult);
    }

    public override Optional<object> FindChoiceType(string name, int index)
    {
        var method = GetMethod("FindChoiceType");
        var innerOpt = method.Invoke(_inner, new object?[] { name, index })!;
        return (Optional<object>)innerOpt;
    }

    public override Optional<T.Type<object>> FindCheckedType(int index)
    {
        var method = GetMethod("FindCheckedType");
        var innerOpt = method.Invoke(_inner, new object?[] { index })!;
        return WrapOptionalType(innerOpt);
    }

    public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
    {
        var method = GetMethod("FindFieldTypeOpt");
        var innerOpt = method.Invoke(_inner, new object?[] { name })!;
        return WrapOptionalType(innerOpt);
    }

    //FindTypeInChildren是泛型方法<FT,FR>反射MakeGenericMethod调用内部对象
    //matcher是Type<object>.TypeMatcher<FT,FR>跨泛型实例化内部期望Type<A>.TypeMatcher<FT,FR>
    //反射Invoke会做运行时类型检查跨泛型实例化失败用Expression Tree构造委托绕过
    //内部返回Either<TypedOptic<S,T,FT,FR>,FieldNotFoundException>反射取IsLeft/Left/Right重新包装
    public override Either<TypedOptic<object, object, FT, FR>, T.Type<object>.FieldNotFoundException> FindTypeInChildren<FT, FR>(
        T.Type<FT> type, T.Type<FR> resultType, T.Type<object>.TypeMatcher<FT, FR> matcher, bool recurse)
    {
        var cacheKey = (_innerType, typeof(FT), typeof(FR));
        var invoker = _findTypeInChildrenInvokerCache.GetOrAdd(cacheKey, _ =>
        {
            var method = _innerType.GetMethod("FindTypeInChildren")!.MakeGenericMethod(typeof(FT), typeof(FR));
            return BuildFindTypeInChildrenInvoker<FT, FR>(method);
        });
        var typedInvoker = (System.Func<object, T.Type<FT>, T.Type<FR>, object, bool, object>)invoker;
        var innerResult = typedInvoker(_inner, type, resultType, matcher!, recurse)!;
        return WrapEitherOpticFieldNotFound<FT, FR>(innerResult);
    }

    //BuildFindTypeInChildrenInvoker用DynamicMethod+IL emit构造委托
    //matcher跨泛型实例化Type<object>.TypeMatcher<FT,FR> vs Type<Pair<string,A>>.TypeMatcher<FT,FR>
    //反射Invoke和Expression.Convert都做castclass运行时类型检查跨泛型实例化失败
    //IL层面ldarg直接传对象引用不做类型检查callvirt按method token解析虚方法槽
    //对齐Java类型擦除语义让matcher对象引用直接传给内部方法
    private static System.Func<object, T.Type<FT>, T.Type<FR>, object, bool, object> BuildFindTypeInChildrenInvoker<FT, FR>(System.Reflection.MethodInfo method)
    {
        var dynamicMethod = new System.Reflection.Emit.DynamicMethod(
            "FindTypeInChildrenInvoker_" + typeof(FT).Name + "_" + typeof(FR).Name,
            typeof(object),
            new[] { typeof(object), typeof(T.Type<FT>), typeof(T.Type<FR>), typeof(object), typeof(bool) },
            true);
        var il = dynamicMethod.GetILGenerator();
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        il.Emit(System.Reflection.Emit.OpCodes.Castclass, method.DeclaringType!);
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_2);
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_3);
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, (byte)4);
        il.Emit(System.Reflection.Emit.OpCodes.Callvirt, method);
        il.Emit(System.Reflection.Emit.OpCodes.Ret);
        return (System.Func<object, T.Type<FT>, T.Type<FR>, object, bool, object>)dynamicMethod.CreateDelegate(
            typeof(System.Func<object, T.Type<FT>, T.Type<FR>, object, bool, object>));
    }

    //WrapEitherOpticFieldNotFound反射Either<...>转Either<TypedOptic<object,object,FT,FR>,FieldNotFoundException>
    private static Either<TypedOptic<object, object, FT, FR>, T.Type<object>.FieldNotFoundException> WrapEitherOpticFieldNotFound<FT, FR>(object innerEither)
    {
        var eitherType = innerEither.GetType()!;
        var isLeftProp = eitherType.GetProperty("IsLeft")!;
        var isLeft = (bool)isLeftProp.GetValue(innerEither)!;
        if (isLeft)
        {
            var getLeftMethod = eitherType.GetMethod("GetLeft")!;
            var leftOpt = getLeftMethod.Invoke(innerEither, null)!;
            var optionalType = leftOpt.GetType();
            var innerPresent = (bool)optionalType.GetProperty("IsPresent")!.GetValue(leftOpt)!;
            if (!innerPresent)
            {
                return Either<TypedOptic<object, object, FT, FR>, T.Type<object>.FieldNotFoundException>.Right(new T.Type<object>.FieldNotFoundException("Empty left in wrapper"));
            }
            var innerGet = optionalType.GetMethod("Get")!;
            var innerOptic = innerGet.Invoke(leftOpt, null)!;
            var opticObj = (object)innerOptic;
            var opticCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, FT, FR>>(ref opticObj);
            return Either<TypedOptic<object, object, FT, FR>, T.Type<object>.FieldNotFoundException>.Left(opticCast);
        }
        var getRightMethod = eitherType.GetMethod("GetRight")!;
        var rightOpt = getRightMethod.Invoke(innerEither, null)!;
        var optionalType2 = rightOpt.GetType();
        var innerPresent2 = (bool)optionalType2.GetProperty("IsPresent")!.GetValue(rightOpt)!;
        if (!innerPresent2)
        {
            return Either<TypedOptic<object, object, FT, FR>, T.Type<object>.FieldNotFoundException>.Right(new T.Type<object>.FieldNotFoundException("Empty right in wrapper"));
        }
        var innerGet2 = optionalType2.GetMethod("Get")!;
        var rightObj = innerGet2.Invoke(rightOpt, null)!;
        if (rightObj.GetType().Name == "Continue")
        {
            return Either<TypedOptic<object, object, FT, FR>, T.Type<object>.FieldNotFoundException>.Right(new T.Type<object>.Continue());
        }
        return Either<TypedOptic<object, object, FT, FR>, T.Type<object>.FieldNotFoundException>.Right(new T.Type<object>.FieldNotFoundException(rightObj.ToString()!));
    }

    //Point<T>是泛型方法反射MakeGenericMethod(typeof(T))
    public override Optional<object> Point<T2>(DynamicOps<T2> ops)
    {
        var method = _innerType.GetMethod("Point")!.MakeGenericMethod(typeof(T2));
        var innerOpt = method.Invoke(_inner, new object?[] { ops })!;
        return WrapOptionalObject(innerOpt);
    }

    //WrapType把任意Type<A>实例包装为Type<object>
    //已是Type<object>直接返回否则用TypeObjectWrapper包装
    private static T.Type<object> WrapType(object? typeObj)
    {
        if (typeObj == null) return null!;
        if (typeObj is T.Type<object> direct) return direct;
        return new TypeObjectWrapper(typeObj);
    }

    //WrapRewriteResult反射取View和RecData重新构造为RewriteResult<object,object>
    private static RewriteResult<object, object> WrapRewriteResult(object innerResult)
    {
        var resultType = innerResult.GetType();
        var viewProp = resultType.GetProperty("ViewValue")!;
        var recDataProp = resultType.GetProperty("RecDataValue")!;
        var innerView = viewProp.GetValue(innerResult)!;
        var innerRecData = (BitSet)recDataProp.GetValue(innerResult)!;
        var newView = WrapView(innerView);
        return RewriteResult<object, object>.Create(newView, innerRecData);
    }

    //WrapView反射取Function/OldType/NewType重新构造为View<object,object>
    //PointFree用Unsafe.As强转对齐Java类型擦除共享基类虚方法槽
    private static View<object, object> WrapView(object innerView)
    {
        var viewType = innerView.GetType();
        var functionProp = viewType.GetProperty("Function")!;
        var oldTypeProp = viewType.GetProperty("OldTypeValue")!;
        var newTypeProp = viewType.GetProperty("NewTypeValue")!;
        var innerFunctionObj = functionProp.GetValue(innerView)!;
        var innerOldType = oldTypeProp.GetValue(innerView);
        var innerNewType = newTypeProp.GetValue(innerView);
        var newFunction = Unsafe.As<object, PointFree<System.Func<object, object>>>(ref innerFunctionObj);
        var newOldType = WrapType(innerOldType);
        var newNewType = WrapType(innerNewType);
        return View<object, object>.Create(newFunction, newOldType, newNewType);
    }

    //WrapOptionalRewriteResult反射Optional<RewriteResult<A,object>>转Optional<RewriteResult<object,object>>
    private static Optional<RewriteResult<object, object>> WrapOptionalRewriteResult(object innerOpt)
    {
        var optType = innerOpt.GetType();
        var isPresentProp = optType.GetProperty("IsPresent")!;
        var isPresent = (bool)isPresentProp.GetValue(innerOpt)!;
        if (!isPresent) return Optional<RewriteResult<object, object>>.Empty();
        var getMethod = optType.GetMethod("Get")!;
        var innerResult = getMethod.Invoke(innerOpt, null)!;
        return Optional<RewriteResult<object, object>>.Of(WrapRewriteResult(innerResult));
    }

    //WrapOptionalType反射Optional<Type<A>>转Optional<Type<object>>
    private static Optional<T.Type<object>> WrapOptionalType(object innerOpt)
    {
        var optType = innerOpt.GetType();
        var isPresentProp = optType.GetProperty("IsPresent")!;
        var isPresent = (bool)isPresentProp.GetValue(innerOpt)!;
        if (!isPresent) return Optional<T.Type<object>>.Empty();
        var getMethod = optType.GetMethod("Get")!;
        var innerType = getMethod.Invoke(innerOpt, null)!;
        return Optional<T.Type<object>>.Of(WrapType(innerType));
    }

    //WrapOptionalObject反射Optional<A>转Optional<object>
    private static Optional<object> WrapOptionalObject(object innerOpt)
    {
        var optType = innerOpt.GetType();
        var isPresentProp = optType.GetProperty("IsPresent")!;
        var isPresent = (bool)isPresentProp.GetValue(innerOpt)!;
        if (!isPresent) return Optional<object>.Empty();
        var getMethod = optType.GetMethod("Get")!;
        var innerValue = getMethod.Invoke(innerOpt, null);
        return Optional<object>.OfNullable(innerValue);
    }

    //CodecAdapter反射调用原Codec<A>的EncodeStart和Parse
        //EncodeStart接收object实际是A类型装箱
        //Parse返回DataResult<A>反射取_value字段包装为DataResult<object>
        //反射Invoke的CheckValue受C#严格泛型不变量限制Pair<object,object>不能转Pair<string,object>
        //EncodeStart<U>是泛型方法按typeof(U)缓存委托委托内部用Unsafe.As把input强转为A类型后调Invoke
        //对齐Java类型擦除语义
        private sealed class CodecAdapter : ScalarCodec<object>
        {
            private readonly object _codec;
            private readonly Type _codecType;
            private readonly Type _inputType;
            private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Func<object, object, object, object>> _encodeCache = new();
            private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Func<object, object, object, object>> _parseCache = new();

            public CodecAdapter(object codec)
            {
                _codec = codec;
                _codecType = codec.GetType();
                var encodeMethod = _codecType.GetMethod("EncodeStart")!;
                _inputType = encodeMethod.GetParameters()[1].ParameterType;
            }

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object input)
            {
                var invoker = _encodeCache.GetOrAdd(typeof(U), u =>
                {
                    var method = _codecType.GetMethod("EncodeStart")!.MakeGenericMethod(u);
                    return BuildInvoker(method, _inputType);
                });
                var result = invoker(_codec, ops, input);
                return (DataResult<U>)result;
            }

            public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
            {
                var invoker = _parseCache.GetOrAdd(typeof(U), u =>
                {
                    var method = _codecType.GetMethod("Parse")!.MakeGenericMethod(u);
                    var parseInputType = method.GetParameters()[1].ParameterType;
                    return BuildInvoker(method, parseInputType);
                });
                var result = invoker(_codec, ops, input!);
                return WrapDataResult(result);
            }

            //BuildInvoker编译委托(object codec, object ops, object input) -> object
            //input用CastTo<A>强转为方法参数类型避免反射Invoke的CheckValue运行时检查
            //对齐Java类型擦除语义让Pair<object,object>当Pair<string,object>用
            private static System.Func<object, object, object, object> BuildInvoker(System.Reflection.MethodInfo method, Type inputType)
            {
                var codecParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "codec");
                var opsParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "ops");
                var inputParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "input");
                var castMethod = typeof(CodecAdapter)
                    .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(inputType);
                var castInput = System.Linq.Expressions.Expression.Call(castMethod, inputParam);
                var call = System.Linq.Expressions.Expression.Call(
                    System.Linq.Expressions.Expression.Convert(codecParam, method.DeclaringType!),
                    method,
                    System.Linq.Expressions.Expression.Convert(opsParam, method.GetParameters()[0].ParameterType),
                    castInput);
                return System.Linq.Expressions.Expression.Lambda<System.Func<object, object, object, object>>(
                    System.Linq.Expressions.Expression.Convert(call, typeof(object)),
                    codecParam, opsParam, inputParam).Compile();
            }

            //WrapDataResult反射取DataResult<A>私有字段构造DataResult<object>
            //A装箱为object对齐Java类型擦除语义
            private static DataResult<object> WrapDataResult(object? dataResult)
            {
                if (dataResult == null) return DataResult<object>.Error(() => "null result");
                var type = dataResult.GetType();
                var successField = type.GetField("_success", BindingFlags.NonPublic | BindingFlags.Instance);
                var valueField = type.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
                var errorField = type.GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance);
                var success = (bool)successField!.GetValue(dataResult)!;
                var value = valueField!.GetValue(dataResult);
                var error = (string?)errorField!.GetValue(dataResult);
                if (success) return DataResult<object>.Success(value!);
                return DataResult<object>.Error(() => error ?? "unknown error", value);
            }

            private static T CastTo<T>(object obj)
            {
                if (obj == null) return default!;
                //T 是值类型时 obj 是装箱 struct 必须 unbox 取值
                //Unsafe.As<object,T>(ref local) 只 reinterpret 栈上引用指针
                //struct 字段会读到装箱指针而非 struct 内部字段值
                if (typeof(T).IsValueType)
                {
                    return (T)obj;
                }
                var local = obj;
                return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
            }
        }
}
