using NetCraft.Codec;
using NetCraft.Util;
using NetCraft.Util.Parsing.Packrat;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Nbt;

//SnbtGrammar的createParser规则注册对应原版createParser方法
public static partial class SnbtGrammar
{
    //CreateParser注册所有解析规则并构造Grammar<T>供TagParser使用
    public static Grammar<T> CreateParser<T>(DynamicOps<T> ops)
    {
        var trueValue = ops.CreateBoolean(true);
        var falseValue = ops.CreateBoolean(false);
        var emptyMap = ops.EmptyMap();
        var emptyList = ops.EmptyList();
        var rules = new Dictionary<CommandStringReader>();

        var sign = Atom<Sign>.Of("sign");
        rules.Put(sign,
            Terms.Alternative(
                Terms.Sequence(StringReaderTerms.Character('+'), Terms.Marker<CommandStringReader, Sign>(sign, Sign.Plus)),
                Terms.Sequence(StringReaderTerms.Character('-'), Terms.Marker<CommandStringReader, Sign>(sign, Sign.Minus))),
            scope => scope.GetOrThrow(sign));

        var integerSuffix = Atom<IntegerSuffix>.Of("integer_suffix");
        rules.Put(integerSuffix,
            Terms.Alternative(
                Terms.Sequence(StringReaderTerms.Characters('u', 'U'),
                    Terms.Alternative(
                        Terms.Sequence(StringReaderTerms.Characters('b', 'B'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Unsigned, TypeSuffix.Byte))),
                        Terms.Sequence(StringReaderTerms.Characters('s', 'S'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Unsigned, TypeSuffix.Short))),
                        Terms.Sequence(StringReaderTerms.Characters('i', 'I'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Unsigned, TypeSuffix.Int))),
                        Terms.Sequence(StringReaderTerms.Characters('l', 'L'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Unsigned, TypeSuffix.Long))))),
                Terms.Sequence(StringReaderTerms.Characters('s', 'S'),
                    Terms.Alternative(
                        Terms.Sequence(StringReaderTerms.Characters('b', 'B'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Signed, TypeSuffix.Byte))),
                        Terms.Sequence(StringReaderTerms.Characters('s', 'S'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Signed, TypeSuffix.Short))),
                        Terms.Sequence(StringReaderTerms.Characters('i', 'I'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Signed, TypeSuffix.Int))),
                        Terms.Sequence(StringReaderTerms.Characters('l', 'L'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(SignedPrefix.Signed, TypeSuffix.Long))))),
                Terms.Sequence(StringReaderTerms.Characters('b', 'B'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(null, TypeSuffix.Byte))),
                Terms.Sequence(StringReaderTerms.Characters('s', 'S'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(null, TypeSuffix.Short))),
                Terms.Sequence(StringReaderTerms.Characters('i', 'I'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(null, TypeSuffix.Int))),
                Terms.Sequence(StringReaderTerms.Characters('l', 'L'), Terms.Marker<CommandStringReader, IntegerSuffix>(integerSuffix, new IntegerSuffix(null, TypeSuffix.Long)))),
            scope => scope.GetOrThrow(integerSuffix));

        var binaryNumeral = Atom<string>.Of("binary_numeral");
        rules.Put(binaryNumeral, BinaryNumeral);
        var decimalNumeral = Atom<string>.Of("decimal_numeral");
        rules.Put(decimalNumeral, DecimalNumeral);
        var hexNumeral = Atom<string>.Of("hex_numeral");
        rules.Put(hexNumeral, HexNumeral);

        var integerLiteral = Atom<IntegerLiteral>.Of("integer_literal");
        var integerLiteralRule = rules.Put(integerLiteral,
            Terms.Sequence(
                Terms.Optional(rules.Named(sign)),
                Terms.Alternative(
                    Terms.Sequence(StringReaderTerms.Character('0'), Terms.Cut<CommandStringReader>(),
                        Terms.Alternative(
                            Terms.Sequence(StringReaderTerms.Characters('x', 'X'), Terms.Cut<CommandStringReader>(), rules.Named(hexNumeral)),
                            Terms.Sequence(StringReaderTerms.Characters('b', 'B'), rules.Named(binaryNumeral)),
                            Terms.Sequence(rules.Named(decimalNumeral), Terms.Cut<CommandStringReader>(), Terms.Fail<CommandStringReader>(ErrorLeadingZeroNotAllowed)),
                            Terms.Marker<CommandStringReader, string>(decimalNumeral, "0"))),
                    rules.Named(decimalNumeral)),
                Terms.Optional(rules.Named(integerSuffix))),
            scope =>
            {
                var suffix = scope.GetOrDefault(integerSuffix, IntegerSuffix.Empty);
                var signValue = scope.GetOrDefault(sign, Sign.Plus);
                var decimalContents = scope.Get<string>(decimalNumeral);
                if (decimalContents is not null)
                    return new IntegerLiteral(signValue, Base.Decimal, decimalContents, suffix);
                var hexContents = scope.Get<string>(hexNumeral);
                if (hexContents is not null)
                    return new IntegerLiteral(signValue, Base.Hex, hexContents, suffix);
                var binaryContents = scope.GetOrThrow(binaryNumeral);
                return new IntegerLiteral(signValue, Base.Binary, binaryContents, suffix);
            });

        var floatTypeSuffix = Atom<TypeSuffix>.Of("float_type_suffix");
        rules.Put(floatTypeSuffix,
            Terms.Alternative(
                Terms.Sequence(StringReaderTerms.Characters('f', 'F'), Terms.Marker<CommandStringReader, TypeSuffix>(floatTypeSuffix, TypeSuffix.Float)),
                Terms.Sequence(StringReaderTerms.Characters('d', 'D'), Terms.Marker<CommandStringReader, TypeSuffix>(floatTypeSuffix, TypeSuffix.Double))),
            scope => scope.GetOrThrow(floatTypeSuffix));

        var floatExponentPart = Atom<Signed<string>>.Of("float_exponent_part");
        rules.Put(floatExponentPart,
            Terms.Sequence(StringReaderTerms.Characters('e', 'E'),
                Terms.Optional(rules.Named(sign)),
                rules.Named(decimalNumeral)),
            scope => new Signed<string>(scope.GetOrDefault(sign, Sign.Plus), scope.GetOrThrow(decimalNumeral)));

        var floatWholePart = Atom<string>.Of("float_whole_part");
        var floatFractionPart = Atom<string>.Of("float_fraction_part");
        var floatLiteral = Atom<T>.Of("float_literal");
        rules.PutComplex(floatLiteral,
            Terms.Sequence(
                Terms.Optional(rules.Named(sign)),
                Terms.Alternative(
                    Terms.Sequence(rules.NamedWithAlias(decimalNumeral, floatWholePart), StringReaderTerms.Character('.'), Terms.Cut<CommandStringReader>(),
                        Terms.Optional(rules.NamedWithAlias(decimalNumeral, floatFractionPart)),
                        Terms.Optional(rules.Named(floatExponentPart)),
                        Terms.Optional(rules.Named(floatTypeSuffix))),
                    Terms.Sequence(StringReaderTerms.Character('.'), Terms.Cut<CommandStringReader>(),
                        rules.NamedWithAlias(decimalNumeral, floatFractionPart),
                        Terms.Optional(rules.Named(floatExponentPart)),
                        Terms.Optional(rules.Named(floatTypeSuffix))),
                    Terms.Sequence(rules.NamedWithAlias(decimalNumeral, floatWholePart), rules.Named(floatExponentPart), Terms.Cut<CommandStringReader>(),
                        Terms.Optional(rules.Named(floatTypeSuffix))),
                    Terms.Sequence(rules.NamedWithAlias(decimalNumeral, floatWholePart),
                        Terms.Optional(rules.Named(floatExponentPart)),
                        rules.Named(floatTypeSuffix)))),
            state =>
            {
                var scope = state.Scope;
                var wholeSign = scope.GetOrDefault(sign, Sign.Plus);
                var whole = scope.Get<string>(floatWholePart);
                var fraction = scope.Get<string>(floatFractionPart);
                var exponent = scope.Get<Signed<string>>(floatExponentPart);
                var typeSuffix = scope.Get<TypeSuffix>(floatTypeSuffix);
                return CreateFloat(ops, wholeSign, whole, fraction, exponent, typeSuffix, state);
            });

        var stringHex2 = Atom<string>.Of("string_hex_2");
        rules.Put(stringHex2, new SimpleHexLiteralParseRule(2));
        var stringHex4 = Atom<string>.Of("string_hex_4");
        rules.Put(stringHex4, new SimpleHexLiteralParseRule(4));
        var stringHex8 = Atom<string>.Of("string_hex_8");
        rules.Put(stringHex8, new SimpleHexLiteralParseRule(8));
        var stringUnicodeName = Atom<string>.Of("string_unicode_name");
        rules.Put(stringUnicodeName, new GreedyPatternParseRule(UnicodeName, ErrorInvalidCharacterName));

        var stringEscapeSequence = Atom<string>.Of("string_escape_sequence");
        rules.PutComplex(stringEscapeSequence,
            Terms.Alternative(
                Terms.Sequence(StringReaderTerms.Character('b'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "\b")),
                Terms.Sequence(StringReaderTerms.Character('s'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, " ")),
                Terms.Sequence(StringReaderTerms.Character('t'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "\t")),
                Terms.Sequence(StringReaderTerms.Character('n'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "\n")),
                Terms.Sequence(StringReaderTerms.Character('f'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "\f")),
                Terms.Sequence(StringReaderTerms.Character('r'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "\r")),
                Terms.Sequence(StringReaderTerms.Character('\\'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "\\")),
                Terms.Sequence(StringReaderTerms.Character('\''), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "'")),
                Terms.Sequence(StringReaderTerms.Character('"'), Terms.Marker<CommandStringReader, string>(stringEscapeSequence, "\"")),
                Terms.Sequence(StringReaderTerms.Character('x'), rules.Named(stringHex2)),
                Terms.Sequence(StringReaderTerms.Character('u'), rules.Named(stringHex4)),
                Terms.Sequence(StringReaderTerms.Character('U'), rules.Named(stringHex8)),
                Terms.Sequence(StringReaderTerms.Character('N'), StringReaderTerms.Character('{'), rules.Named(stringUnicodeName), StringReaderTerms.Character('}'))),
            state =>
            {
                var scope = state.Scope;
                var plainEscape = scope.GetAny<string>(stringEscapeSequence);
                if (plainEscape is not null) return plainEscape;
                var hexEscape = scope.GetAny<string>(stringHex2, stringHex4, stringHex8);
                if (hexEscape is not null)
                {
                    var codePoint = Convert.ToInt32(hexEscape, 16);
                    if (codePoint < 0 || codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
                    {
                        state.ErrorCollector.Store(state.Mark(),
                            DelayedExceptionFactories.Create(ErrorInvalidCodepointType, $"U+{codePoint:X8}"));
                        return default!;
                    }
                    return char.ConvertFromUtf32(codePoint);
                }
                var character = scope.GetOrThrow(stringUnicodeName);
                //C#无Character.codePointOf等价方法对齐Java按名字查codepoint
                //\N{name}转义是边缘功能暂不支持直接报错
                state.ErrorCollector.Store(state.Mark(), ErrorInvalidCharacterName);
                return default!;
            });

        var stringPlainContents = Atom<string>.Of("string_plain_contents");
        rules.Put(stringPlainContents, PlainStringChunk);

        var stringChunks = Atom<List<string>>.Of("string_chunks");
        var stringContents = Atom<string>.Of("string_contents");

        var singleQuotedStringChunk = rules.Put(Atom<string>.Of("single_quoted_string_chunk"),
            Terms.Alternative(
                rules.NamedWithAlias(stringPlainContents, stringContents),
                Terms.Sequence(StringReaderTerms.Character('\\'), rules.NamedWithAlias(stringEscapeSequence, stringContents)),
                Terms.Sequence(StringReaderTerms.Character('"'), Terms.Marker<CommandStringReader, string>(stringContents, "\""))),
            scope => scope.GetOrThrow(stringContents));

        var singleQuotedStringContents = Atom<string>.Of("single_quoted_string_contents");
        rules.Put(singleQuotedStringContents,
            Terms.Repeated(singleQuotedStringChunk, stringChunks),
            scope => JoinList(scope.GetOrThrow(stringChunks)));

        var doubleQuotedStringChunk = rules.Put(Atom<string>.Of("double_quoted_string_chunk"),
            Terms.Alternative(
                rules.NamedWithAlias(stringPlainContents, stringContents),
                Terms.Sequence(StringReaderTerms.Character('\\'), rules.NamedWithAlias(stringEscapeSequence, stringContents)),
                Terms.Sequence(StringReaderTerms.Character('\''), Terms.Marker<CommandStringReader, string>(stringContents, "'"))),
            scope => scope.GetOrThrow(stringContents));

        var doubleQuotedStringContents = Atom<string>.Of("double_quoted_string_contents");
        rules.Put(doubleQuotedStringContents,
            Terms.Repeated(doubleQuotedStringChunk, stringChunks),
            scope => JoinList(scope.GetOrThrow(stringChunks)));

        var quotedStringLiteral = Atom<string>.Of("quoted_string_literal");
        rules.Put(quotedStringLiteral,
            Terms.Alternative(
                Terms.Sequence(StringReaderTerms.Character('"'), Terms.Cut<CommandStringReader>(),
                    Terms.Optional(rules.NamedWithAlias(doubleQuotedStringContents, stringContents)),
                    StringReaderTerms.Character('"')),
                Terms.Sequence(StringReaderTerms.Character('\''),
                    Terms.Optional(rules.NamedWithAlias(singleQuotedStringContents, stringContents)),
                    StringReaderTerms.Character('\''))),
            scope => scope.GetOrThrow(stringContents));

        var unquotedString = Atom<string>.Of("unquoted_string");
        rules.Put(unquotedString, new UnquotedStringParseRule(1, ErrorExpectedUnquotedString));

        var literal = Atom<T>.Of("literal");
        var arguments = Atom<List<T>>.Of("arguments");
        rules.Put(arguments,
            Terms.RepeatedWithTrailingSeparator(rules.Forward(literal), arguments, StringReaderTerms.Character(',')),
            scope => scope.GetOrThrow(arguments));

        var unquotedStringOrBuiltIn = Atom<T>.Of("unquoted_string_or_builtin");
        rules.PutComplex(unquotedStringOrBuiltIn,
            Terms.Sequence(rules.Named(unquotedString),
                Terms.Optional(Terms.Sequence(
                    StringReaderTerms.Character('('),
                    rules.Named(arguments),
                    StringReaderTerms.Character(')')))),
            state =>
            {
                var scope = state.Scope;
                var contents = scope.GetOrThrow(unquotedString);
                if (contents.Length == 0 || !IsAllowedToStartUnquotedString(contents[0]))
                {
                    state.ErrorCollector.Store(state.Mark(), SnbtOperations.BuiltinIds, ErrorInvalidUnquotedStart);
                    return default!;
                }
                var list = scope.Get<List<T>>(arguments);
                if (list is not null)
                {
                    var key = new BuiltinKey(contents, list.Count);
                    if (SnbtOperations.BuiltinOperations.TryGetValue(key, out var operation))
                    {
                        return operation.Run<T>(ops, list, state);
                    }
                    state.ErrorCollector.Store(state.Mark(),
                        DelayedExceptionFactories.Create(ErrorNoSuchOperationType, key.ToString()));
                    return default!;
                }
                if (string.Equals(contents, "true", StringComparison.OrdinalIgnoreCase)) return trueValue;
                if (string.Equals(contents, "false", StringComparison.OrdinalIgnoreCase)) return falseValue;
                return ops.CreateString(contents);
            });

        var mapKey = Atom<string>.Of("map_key");
        rules.Put(mapKey,
            Terms.Alternative(rules.Named(quotedStringLiteral), rules.Named(unquotedString)),
            scope => scope.GetAnyOrThrow<string>(quotedStringLiteral, unquotedString));

        var mapEntry = Atom<MapEntry<T>>.Of("map_entry");
        var mapEntryRule = rules.PutComplex(mapEntry,
            Terms.Sequence(rules.Named(mapKey), StringReaderTerms.Character(':'), rules.Named(literal)),
            state =>
            {
                var scope = state.Scope;
                var key = scope.GetOrThrow(mapKey);
                if (key.Length == 0)
                {
                    state.ErrorCollector.Store(state.Mark(), ErrorEmptyKey);
                    return default!;
                }
                return new MapEntry<T>(key, scope.GetOrThrow(literal));
            });

        var mapEntries = Atom<List<MapEntry<T>>>.Of("map_entries");
        rules.Put(mapEntries,
            Terms.RepeatedWithTrailingSeparator(mapEntryRule, mapEntries, StringReaderTerms.Character(',')),
            scope => scope.GetOrThrow(mapEntries));

        var mapLiteral = Atom<T>.Of("map_literal");
        rules.Put(mapLiteral,
            Terms.Sequence(StringReaderTerms.Character('{'), rules.Named(mapEntries), StringReaderTerms.Character('}')),
            scope =>
            {
                var list = scope.GetOrThrow(mapEntries);
                if (list.Count == 0) return emptyMap;
                var pairs = list.Select(me => new Pair<T, T>(ops.CreateString(me.Key), me.Value));
                return ops.CreateMap(pairs);
            });

        var listEntries = Atom<List<T>>.Of("list_entries");
        rules.Put(listEntries,
            Terms.RepeatedWithTrailingSeparator(rules.Forward(literal), listEntries, StringReaderTerms.Character(',')),
            scope => scope.GetOrThrow(listEntries));

        var arrayPrefix = Atom<ArrayPrefix>.Of("array_prefix");
        rules.Put(arrayPrefix,
            Terms.Alternative(
                Terms.Sequence(StringReaderTerms.Character('B'), Terms.Marker<CommandStringReader, ArrayPrefix>(arrayPrefix, ArrayPrefix.Byte)),
                Terms.Sequence(StringReaderTerms.Character('L'), Terms.Marker<CommandStringReader, ArrayPrefix>(arrayPrefix, ArrayPrefix.Long)),
                Terms.Sequence(StringReaderTerms.Character('I'), Terms.Marker<CommandStringReader, ArrayPrefix>(arrayPrefix, ArrayPrefix.Int))),
            scope => scope.GetOrThrow(arrayPrefix));

        var intArrayEntries = Atom<List<IntegerLiteral>>.Of("int_array_entries");
        rules.Put(intArrayEntries,
            Terms.RepeatedWithTrailingSeparator(integerLiteralRule, intArrayEntries, StringReaderTerms.Character(',')),
            scope => scope.GetOrThrow(intArrayEntries));

        var listLiteral = Atom<T>.Of("list_literal");
        rules.PutComplex(listLiteral,
            Terms.Sequence(StringReaderTerms.Character('['),
                Terms.Alternative(
                    Terms.Sequence(rules.Named(arrayPrefix), StringReaderTerms.Character(';'), rules.Named(intArrayEntries)),
                    rules.Named(listEntries)),
                StringReaderTerms.Character(']')),
            state =>
            {
                var scope = state.Scope;
                var arrayType = scope.Get<ArrayPrefix>(arrayPrefix);
                if (arrayType is not null)
                {
                    var entries = scope.GetOrThrow(intArrayEntries);
                    return entries.Count == 0 ? arrayType.Create(ops) : arrayType.Create(ops, entries, state);
                }
                var list = scope.GetOrThrow(listEntries);
                return list.Count == 0 ? emptyList : ops.CreateList(list);
            });

        var literalRule = rules.PutComplex(literal,
            Terms.Alternative(
                Terms.Sequence(Terms.PositiveLookahead(NumberLookahead),
                    Terms.Alternative(rules.NamedWithAlias(floatLiteral, literal), rules.Named(integerLiteral))),
                Terms.Sequence(Terms.PositiveLookahead(StringReaderTerms.Characters('"', '\'')), Terms.Cut<CommandStringReader>(),
                    rules.Named(quotedStringLiteral)),
                Terms.Sequence(Terms.PositiveLookahead(StringReaderTerms.Character('{')), Terms.Cut<CommandStringReader>(),
                    rules.NamedWithAlias(mapLiteral, literal)),
                Terms.Sequence(Terms.PositiveLookahead(StringReaderTerms.Character('[')), Terms.Cut<CommandStringReader>(),
                    rules.NamedWithAlias(listLiteral, literal)),
                rules.NamedWithAlias(unquotedStringOrBuiltIn, literal)),
            state =>
            {
                var scope = state.Scope;
                var quotedString = scope.Get<string>(quotedStringLiteral);
                if (quotedString is not null) return ops.CreateString(quotedString);
                var integer = scope.Get<IntegerLiteral>(integerLiteral);
                if (integer is not null) return integer.Create(ops, state);
                return scope.GetOrThrow(literal);
            });

        return new Grammar<T>(rules, literalRule);
    }
}
