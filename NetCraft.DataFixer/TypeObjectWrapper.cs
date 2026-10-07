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

//TypeObjectWrapper wraps any Type<A> instance as Type<object>
//solves the problem that under C#'s strict generic invariance, Type<int> and NamedType<A>:Type<Pair<string,A>> cannot be cast to Type<object>
//aligning with Java type erasure semantics by delegating virtual methods to the original instance
public sealed class TypeObjectWrapper : T.Type<object>
{
    private readonly object _inner;
    private readonly Type _innerType;
    private Codec<object>? _codecCache;
    private static readonly Dictionary<Type, MethodInfo> _methodCache = new();
    private static readonly object _cacheLock = new();
    //FindTypeInChildren delegate cache keyed by (inner type,FT,FR) to avoid rebuilding the Expression repeatedly
    private static readonly ConcurrentDictionary<(Type, Type, Type), object> _findTypeInChildrenInvokerCache = new();

    public TypeObjectWrapper(object inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _innerType = inner.GetType();
    }

    public object Inner => _inner;

    //reflectively gets an instance method, with caching
    private MethodInfo GetMethod(string name, Type[]? paramTypes = null)
    {
        var key = (name, paramTypes?.Length ?? 0);
        var cacheKey = _innerType.GetHashCode() ^ name.GetHashCode() ^ (paramTypes?.Length ?? 0);
        lock (_cacheLock)
        {
            //simple cache keyed by (innerType, name, paramCount)
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

    //reflectively calls the original instance's Template method, aligning with vanilla Type.template()
    public override TypeTemplate BuildTemplate()
        => (TypeTemplate)GetMethod("Template").Invoke(_inner, null)!;

    //wraps the original Codec<A> as Codec<object>
    //the original instance is Type<A>, so Codec() must be fetched first then wrapped; passing the Type instance would make GetMethod("Parse") fail to find the method
    protected override Codec<object> BuildCodec()
        => _codecCache ??= new CodecAdapter(GetInnerCodec());

    //GetInnerCodec reflectively calls Type<A>.Codec() to get the original Codec<A> instance
    private object GetInnerCodec()
    {
        var method = GetMethod("Codec");
        return method.Invoke(_inner, null)!;
    }

    //delegates to the original instance's Equals(o, ignoreRecursionPoints, checkIndex)
    //when the other is a wrapper compare the inner instance, when it is a bare Type<A> compare directly
    public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
    {
        var method = GetMethod("Equals", new[] { typeof(object), typeof(bool), typeof(bool) });
        var unwrapped = o is TypeObjectWrapper w ? w._inner : o;
        return (bool)method.Invoke(_inner, new object?[] { unwrapped, ignoreRecursionPoints, checkIndex })!;
    }

    //Equals(object) delegates to Equals(o,true,true) so the RewriteCacheKey record compares structurally
    //without overriding, the record's EqualityComparer calls Object.Equals for reference comparison, so the cache never hits
    public override bool Equals(object? obj) => Equals(obj, true, true);

    public override int GetHashCode() => _inner.GetHashCode();
    public override string? ToString() => _inner.ToString();

    //delegates All, returning RewriteResult<object,object>
    public override RewriteResult<object, object> All(object rule, bool recurse, bool checkIndex)
    {
        var method = GetMethod("All");
        var innerResult = method.Invoke(_inner, new object?[] { rule, recurse, checkIndex })!;
        return WrapRewriteResult(innerResult);
    }

    //delegates One, returning Optional<RewriteResult<object,object>>
    public override Optional<RewriteResult<object, object>> One(object rule)
    {
        var method = GetMethod("One");
        var innerOpt = method.Invoke(_inner, new object?[] { rule })!;
        return WrapOptionalRewriteResult(innerOpt);
    }

    //delegates Everywhere, returning Optional<RewriteResult<object,object>>
    public override Optional<RewriteResult<object, object>> Everywhere(object rule, object optimizationRule, bool recurse, bool checkIndex)
    {
        var method = GetMethod("Everywhere");
        var innerOpt = method.Invoke(_inner, new object?[] { rule, optimizationRule, recurse, checkIndex })!;
        return WrapOptionalRewriteResult(innerOpt);
    }

    //delegates UpdateMu, returning a recursively wrapped Type<object>
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

    //FindTypeInChildren is a generic method <FT,FR>, invoked via reflective MakeGenericMethod on the inner object
    //matcher is Type<object>.TypeMatcher<FT,FR>, but the inner expects Type<A>.TypeMatcher<FT,FR> across generic instantiations
    //reflective Invoke performs a runtime type check that fails across generic instantiations, so an Expression Tree delegate is built to bypass it
    //the inner returns Either<TypedOptic<S,T,FT,FR>,FieldNotFoundException>; reflectively read IsLeft/Left/Right and re-wrap
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

    //BuildFindTypeInChildrenInvoker builds a delegate with DynamicMethod+IL emit
    //matcher across generic instantiations: Type<object>.TypeMatcher<FT,FR> vs Type<Pair<string,A>>.TypeMatcher<FT,FR>
    //both reflective Invoke and Expression.Convert perform a castclass runtime type check that fails across generic instantiations
    //at the IL level ldarg passes the object reference directly without a type check, and callvirt resolves the virtual method slot by method token
    //aligning with Java type erasure semantics, passing the matcher object reference straight to the inner method
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

    //WrapEitherOpticFieldNotFound reflectively converts Either<...> to Either<TypedOptic<object,object,FT,FR>,FieldNotFoundException>
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

    //Point<T> is a generic method, reflectively invoked via MakeGenericMethod(typeof(T))
    public override Optional<object> Point<T2>(DynamicOps<T2> ops)
    {
        var method = _innerType.GetMethod("Point")!.MakeGenericMethod(typeof(T2));
        var innerOpt = method.Invoke(_inner, new object?[] { ops })!;
        return WrapOptionalObject(innerOpt);
    }

    //WrapType wraps any Type<A> instance as Type<object>
    //returns it directly if already Type<object>, otherwise wraps it with TypeObjectWrapper
    private static T.Type<object> WrapType(object? typeObj)
    {
        if (typeObj == null) return null!;
        if (typeObj is T.Type<object> direct) return direct;
        return new TypeObjectWrapper(typeObj);
    }

    //WrapRewriteResult reflectively reads View and RecData and rebuilds as RewriteResult<object,object>
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

    //WrapView reflectively reads Function/OldType/NewType and rebuilds as View<object,object>
    //PointFree uses Unsafe.As to cast, aligning with Java type erasure sharing a base class virtual method slot
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

    //WrapOptionalRewriteResult reflectively converts Optional<RewriteResult<A,object>> to Optional<RewriteResult<object,object>>
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

    //WrapOptionalType reflectively converts Optional<Type<A>> to Optional<Type<object>>
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

    //WrapOptionalObject reflectively converts Optional<A> to Optional<object>
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

    //CodecAdapter reflectively calls the original Codec<A>'s EncodeStart and Parse
        //EncodeStart receives object, which is actually a boxed A
        //Parse returns DataResult<A>; reflectively read the _value field and wrap as DataResult<object>
        //reflective Invoke's CheckValue is limited by C#'s strict generic invariance: Pair<object,object> cannot be cast to Pair<string,object>
        //EncodeStart<U> is a generic method; the delegate is cached by typeof(U) and internally uses Unsafe.As to cast input to A before calling Invoke
        //aligning with Java type erasure semantics
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

            //BuildInvoker compiles the delegate (object codec, object ops, object input) -> object
            //input is cast to the method parameter type via CastTo<A>, avoiding the reflective Invoke CheckValue runtime check
            //aligning with Java type erasure semantics, letting Pair<object,object> be used as Pair<string,object>
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

            //WrapDataResult reflectively reads DataResult<A>'s private fields and builds a DataResult<object>
            //A is boxed to object, aligning with Java type erasure semantics
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
                //when T is a value type, obj is a boxed struct and must be unboxed to read the value
                //Unsafe.As<object,T>(ref local) only reinterprets the reference pointer on the stack
                //reading struct fields would read the boxing pointer instead of the struct's inner field values
                if (typeof(T).IsValueType)
                {
                    return (T)obj;
                }
                var local = obj;
                return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
            }
        }
}
