// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Tests;
using System.Text.Unicode;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace GB18030.Tests;

/// <summary>
/// Regex does not support surrogate pairs, which drastically reduces the number of characters in GB18030 that can be matched.
/// </summary>
public class RegexTests
{

    private readonly ITestOutputHelper _output;

    public RegexTests(ITestOutputHelper output)
    {
        _output = output;
    }


    public enum RegexEngine
    {
        Interpreter,
        Compiled,
        NonBacktracking,
        SourceGenerated,
    }

    public static IEnumerable<RegexEngine> AvailableEngines
    {
        get
        {
            yield return RegexEngine.Interpreter;
            yield return RegexEngine.Compiled;
            if (PlatformDetection.IsNetCore)
            {
                yield return RegexEngine.NonBacktracking;

                if (PlatformDetection.IsReflectionEmitSupported && // the source generator doesn't use reflection emit, but it does use Roslyn for the equivalent
                    PlatformDetection.IsNotMobile &&
                    PlatformDetection.IsNotBrowser)
                {
                    yield return RegexEngine.SourceGenerated;
                }
            }
        }
    }

    // Ranges added in GB18030-2020
    private static readonly UnicodeRange s_cjkNewRange = UnicodeRange.Create((char)0x9FF0, (char)0x9FFF);
    private static readonly UnicodeRange s_cjkExtensionANewRange = UnicodeRange.Create((char)0x4DB6, (char)0x4DBF);

    private static readonly IEnumerable<string> s_cjkNewCharacters = Enumerable.Range(s_cjkNewRange.FirstCodePoint, s_cjkNewRange.Length).Select(c => ((char)c).ToString());
    private static readonly IEnumerable<string> s_cjkExtensionANewCharacters = Enumerable.Range(s_cjkExtensionANewRange.FirstCodePoint, s_cjkExtensionANewRange.Length).Select(c => ((char)c).ToString());
    private static readonly IEnumerable<string> s_allNewCharacters = s_cjkNewCharacters.Union(s_cjkExtensionANewCharacters);

    // https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-classes-in-regular-expressions#supported-named-blocks
    private static readonly Dictionary<UnicodeRange, (string, string[])> s_rangeToRegexMap = new()
    {
        { s_cjkNewRange, ("IsCJKUnifiedIdeographs", s_cjkNewCharacters.ToArray()) },
        { s_cjkExtensionANewRange, ("IsCJKUnifiedIdeographsExtensionA", s_cjkExtensionANewCharacters.ToArray()) }
    };

    [Fact]
    public void qt()
    {
        List<char> values = new();

        foreach (string v in s_allNewCharacters)
        {
            _output.WriteLine(v + $": {CharUnicodeInfo.GetUnicodeCategory(v[0])}");
        }

        _output.WriteLine(values.Count.ToString());
    }

    public static IEnumerable<object[]> UnicodeCategories_TestData() =>
        AvailableEngines.SelectMany(engine =>
        TestHelper.s_cultures.Select(culture => new object[] { engine, culture }));

    [Theory]
    [MemberData(nameof(UnicodeCategories_TestData))]
    [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2522617")]
    public async Task UnicodeCategory_InclusionAsync(RegexEngine engine, CultureInfo culture)
    {
        Regex r = await GetRegexAsync(engine, @"\p{Lo}", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.Matches(r, element);

        r = await GetRegexAsync(engine, @"[\p{Lo}]", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.Matches(r, element);

        r = await GetRegexAsync(engine, @"\p{L}", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.Matches(r, element);

        r = await GetRegexAsync(engine, @"[\p{L}]", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.Matches(r, element);
    }

    [Theory]
    [MemberData(nameof(UnicodeCategories_TestData))]
    [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2522617")]
    public async Task UnicodeCategory_ExclusionAsync(RegexEngine engine, CultureInfo culture)
    {
        Regex r = await GetRegexAsync(engine, @"\P{Lo}", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.DoesNotMatch(r, element);

        r = await GetRegexAsync(engine, @"[^\p{Lo}]", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.DoesNotMatch(r, element);

        r = await GetRegexAsync(engine, @"\P{L}", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.DoesNotMatch(r, element);

        r = await GetRegexAsync(engine, @"[^\p{L}]", RegexOptions.None, culture);
        foreach (string element in s_allNewCharacters)
            Assert.DoesNotMatch(r, element);
    }

    public static IEnumerable<object[]> NamedBlock_TestData() =>
        s_rangeToRegexMap.SelectMany(rangeKvp =>
        AvailableEngines.SelectMany(engine =>
        TestHelper.s_cultures.Select(culture => new object[] { rangeKvp.Key, engine, culture })));

    [Theory]
    [MemberData(nameof(NamedBlock_TestData))]
    public async Task NamedBlock_InclusionAsync(UnicodeRange range, RegexEngine engine, CultureInfo culture)
    {
        (string namedBlock, string[] charactersInRange) = s_rangeToRegexMap[range];

        Regex r = await GetRegexAsync(engine, $@"\p{{{namedBlock}}}", RegexOptions.None, culture);
        foreach (string element in charactersInRange)
            Assert.Matches(r, element);

        r = await GetRegexAsync(engine, $@"[\p{{{namedBlock}}}]", RegexOptions.None, culture);
        foreach (string element in charactersInRange)
            Assert.Matches(r, element);
    }

    [Theory]
    [MemberData(nameof(NamedBlock_TestData))]
    public async Task NamedBlock_ExclusionAsync(UnicodeRange range, RegexEngine engine, CultureInfo culture)
    {
        (string namedBlock, string[] charactersInRange) = s_rangeToRegexMap[range];

        Regex r = await GetRegexAsync(engine, $@"\P{{{namedBlock}}}", RegexOptions.None, culture);
        foreach (string element in charactersInRange)
        {
            Assert.DoesNotMatch(r, element);
        }

        r = await GetRegexAsync(engine, $@"[^\p{{{namedBlock}}}]", RegexOptions.None, culture);
        foreach (string element in charactersInRange)
        {
            Assert.DoesNotMatch(r, element);
        }
    }

    public static async Task<Regex> GetRegexAsync(RegexEngine engine, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options, CultureInfo culture)
    {
        if (engine == RegexEngine.SourceGenerated)
        {
            return await RegexGeneratorHelper.SourceGenRegexAsync(pattern, culture, options);
        }

        using (new System.Tests.ThreadCultureChange(culture))
        {
            return await GetRegexAsync(engine, pattern, options);
        }
    }

    public static async Task<Regex> GetRegexAsync(RegexEngine engine, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions? options = null, TimeSpan? matchTimeout = null)
    {
        if (options is null)
        {
            Assert.Null(matchTimeout);
        }

        if (engine == RegexEngine.SourceGenerated)
        {
            return await RegexGeneratorHelper.SourceGenRegexAsync(pattern, null, options, matchTimeout);
        }

        return
            options is null ? new Regex(pattern, OptionsFromEngine(engine)) :
            matchTimeout is null ? new Regex(pattern, options.Value | OptionsFromEngine(engine)) :
            new Regex(pattern, options.Value | OptionsFromEngine(engine), matchTimeout.Value);
    }

    public static RegexOptions OptionsFromEngine(RegexEngine engine) => engine switch
    {
        RegexEngine.Interpreter => RegexOptions.None,
        RegexEngine.Compiled => RegexOptions.Compiled,
        RegexEngine.SourceGenerated => RegexOptions.Compiled,
        RegexEngine.NonBacktracking => RegexOptionNonBacktracking,
        _ => throw new ArgumentException($"Unknown engine: {engine}"),
    };

    /// <summary>RegexOptions.NonBacktracking.</summary>
    /// <remarks>Defined here to be able to reference the value by name even on .NET Framework test builds.</remarks>
    public const RegexOptions RegexOptionNonBacktracking = (RegexOptions)0x400;
}
