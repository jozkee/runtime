// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Tests;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace GB18030.Tests;

public class RegexTests
{
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

    static Dictionary<UnicodeCategory, string> s_UnicodeCategoryRegexMap = new()
    {
        { UnicodeCategory.UppercaseLetter, "Lu" },
        { UnicodeCategory.LowercaseLetter, "Ll" },
        { UnicodeCategory.TitlecaseLetter, "Lt" },
        { UnicodeCategory.ModifierLetter, "Lm" },
        { UnicodeCategory.OtherLetter, "Lo" },

        { UnicodeCategory.NonSpacingMark, "Mn" },
        { UnicodeCategory.SpacingCombiningMark, "Mc" },
        { UnicodeCategory.EnclosingMark, "Me" },

        { UnicodeCategory.DecimalDigitNumber, "Nd" },
        { UnicodeCategory.LetterNumber, "Nl" },
        { UnicodeCategory.OtherNumber, "No" },

        { UnicodeCategory.ConnectorPunctuation, "Pc" },
        { UnicodeCategory.DashPunctuation, "Pd" },
        { UnicodeCategory.OpenPunctuation, "Ps" },
        { UnicodeCategory.ClosePunctuation, "Pe" },
        { UnicodeCategory.InitialQuotePunctuation, "Pi" },
        { UnicodeCategory.FinalQuotePunctuation, "Pf" },
        { UnicodeCategory.OtherPunctuation, "Po" },

        { UnicodeCategory.MathSymbol, "Sm" },
        { UnicodeCategory.CurrencySymbol, "Sc" },
        { UnicodeCategory.ModifierSymbol, "Sk" },
        { UnicodeCategory.OtherSymbol, "So" },

        { UnicodeCategory.SpaceSeparator, "Zs" },
        { UnicodeCategory.LineSeparator, "Zl" },
        { UnicodeCategory.ParagraphSeparator, "Zp" },

        { UnicodeCategory.Control, "Cc" },
        { UnicodeCategory.Format, "Cf" },
        { UnicodeCategory.Surrogate, "Cs" },
        { UnicodeCategory.PrivateUse, "Co" },
        { UnicodeCategory.OtherNotAssigned, "Cn" },
    };

    public static IEnumerable<object[]> UnicodeCategoriesInclusionsExpected_MemberData()
    {
        foreach (RegexEngine engine in AvailableEngines)
        {
            // https://learn.microsoft.com/dotnet/standard/base-types/character-classes-in-regular-expressions#supported-unicode-general-categories

            yield return new object[] { engine, "L", new[] { UnicodeCategory.UppercaseLetter, UnicodeCategory.LowercaseLetter, UnicodeCategory.TitlecaseLetter, UnicodeCategory.ModifierLetter, UnicodeCategory.OtherLetter } };
            yield return new object[] { engine, "Lu", new[] { UnicodeCategory.UppercaseLetter } };
            yield return new object[] { engine, "Ll", new[] { UnicodeCategory.LowercaseLetter } };
            yield return new object[] { engine, "Lt", new[] { UnicodeCategory.TitlecaseLetter } };
            yield return new object[] { engine, "Lm", new[] { UnicodeCategory.ModifierLetter } };
            yield return new object[] { engine, "Lo", new[] { UnicodeCategory.OtherLetter } };

            yield return new object[] { engine, "M", new[] { UnicodeCategory.NonSpacingMark, UnicodeCategory.SpacingCombiningMark, UnicodeCategory.EnclosingMark } };
            yield return new object[] { engine, "Mn", new[] { UnicodeCategory.NonSpacingMark } };
            yield return new object[] { engine, "Mc", new[] { UnicodeCategory.SpacingCombiningMark } };
            yield return new object[] { engine, "Me", new[] { UnicodeCategory.EnclosingMark } };

            yield return new object[] { engine, "N", new[] { UnicodeCategory.DecimalDigitNumber, UnicodeCategory.LetterNumber, UnicodeCategory.OtherNumber } };
            yield return new object[] { engine, "Nd", new[] { UnicodeCategory.DecimalDigitNumber } };
            yield return new object[] { engine, "Nl", new[] { UnicodeCategory.LetterNumber } };
            yield return new object[] { engine, "No", new[] { UnicodeCategory.OtherNumber } };

            yield return new object[] { engine, "P", new[] { UnicodeCategory.ConnectorPunctuation, UnicodeCategory.DashPunctuation, UnicodeCategory.OpenPunctuation, UnicodeCategory.ClosePunctuation, UnicodeCategory.InitialQuotePunctuation, UnicodeCategory.FinalQuotePunctuation, UnicodeCategory.OtherPunctuation } };
            yield return new object[] { engine, "Pc", new[] { UnicodeCategory.ConnectorPunctuation } };
            yield return new object[] { engine, "Pd", new[] { UnicodeCategory.DashPunctuation } };
            yield return new object[] { engine, "Ps", new[] { UnicodeCategory.OpenPunctuation } };
            yield return new object[] { engine, "Pe", new[] { UnicodeCategory.ClosePunctuation } };
            yield return new object[] { engine, "Pi", new[] { UnicodeCategory.InitialQuotePunctuation } };
            yield return new object[] { engine, "Pf", new[] { UnicodeCategory.FinalQuotePunctuation } };
            yield return new object[] { engine, "Po", new[] { UnicodeCategory.OtherPunctuation } };

            yield return new object[] { engine, "S", new[] { UnicodeCategory.MathSymbol, UnicodeCategory.CurrencySymbol, UnicodeCategory.ModifierSymbol, UnicodeCategory.OtherSymbol } };
            yield return new object[] { engine, "Sm", new[] { UnicodeCategory.MathSymbol } };
            yield return new object[] { engine, "Sc", new[] { UnicodeCategory.CurrencySymbol } };
            yield return new object[] { engine, "Sk", new[] { UnicodeCategory.ModifierSymbol } };
            yield return new object[] { engine, "So", new[] { UnicodeCategory.OtherSymbol } };

            yield return new object[] { engine, "Z", new[] { UnicodeCategory.SpaceSeparator, UnicodeCategory.LineSeparator, UnicodeCategory.ParagraphSeparator } };
            yield return new object[] { engine, "Zs", new[] { UnicodeCategory.SpaceSeparator } };
            yield return new object[] { engine, "Zl", new[] { UnicodeCategory.LineSeparator } };
            yield return new object[] { engine, "Zp", new[] { UnicodeCategory.ParagraphSeparator } };

            yield return new object[] { engine, "C", new[] { UnicodeCategory.Control, UnicodeCategory.Format, UnicodeCategory.Surrogate, UnicodeCategory.PrivateUse, UnicodeCategory.OtherNotAssigned } };
            yield return new object[] { engine, "Cc", new[] { UnicodeCategory.Control } };
            yield return new object[] { engine, "Cf", new[] { UnicodeCategory.Format } };
            yield return new object[] { engine, "Cs", new[] { UnicodeCategory.Surrogate } };
            yield return new object[] { engine, "Co", new[] { UnicodeCategory.PrivateUse } };
            yield return new object[] { engine, "Cn", new[] { UnicodeCategory.OtherNotAssigned } };
        }
    }

    public static IEnumerable<object[]> NamedBlocksInclusionsExpected_MemberData()
    {
        foreach (RegexEngine engine in AvailableEngines)
        {
            yield return new object[] { engine, @"\p{IsBasicLatin}", new[] { 0x0000, 0x007F } };
            yield return new object[] { engine, @"\p{IsLatin-1Supplement}", new[] { 0x0080, 0x00FF } };
            yield return new object[] { engine, @"\p{IsLatinExtended-A}", new[] { 0x0100, 0x017F } };
            yield return new object[] { engine, @"\p{IsLatinExtended-B}", new[] { 0x0180, 0x024F } };
            yield return new object[] { engine, @"\p{IsIPAExtensions}", new[] { 0x0250, 0x02AF } };
            yield return new object[] { engine, @"\p{IsSpacingModifierLetters}", new[] { 0x02B0, 0x02FF } };
            yield return new object[] { engine, @"\p{IsCombiningDiacriticalMarks}", new[] { 0x0300, 0x036F } };
            yield return new object[] { engine, @"\p{IsGreek}", new[] { 0x0370, 0x03FF } };
            yield return new object[] { engine, @"\p{IsCyrillic}", new[] { 0x0400, 0x04FF } };
            yield return new object[] { engine, @"\p{IsCyrillicSupplement}", new[] { 0x0500, 0x052F } };
            yield return new object[] { engine, @"\p{IsArmenian}", new[] { 0x0530, 0x058F } };
            yield return new object[] { engine, @"\p{IsHebrew}", new[] { 0x0590, 0x05FF } };
            yield return new object[] { engine, @"\p{IsArabic}", new[] { 0x0600, 0x06FF } };
            yield return new object[] { engine, @"\p{IsSyriac}", new[] { 0x0700, 0x074F } };
            yield return new object[] { engine, @"\p{IsThaana}", new[] { 0x0780, 0x07BF } };
            yield return new object[] { engine, @"\p{IsDevanagari}", new[] { 0x0900, 0x097F } };
            yield return new object[] { engine, @"\p{IsBengali}", new[] { 0x0980, 0x09FF } };
            yield return new object[] { engine, @"\p{IsGurmukhi}", new[] { 0x0A00, 0x0A7F } };
            yield return new object[] { engine, @"\p{IsGujarati}", new[] { 0x0A80, 0x0AFF } };
            yield return new object[] { engine, @"\p{IsOriya}", new[] { 0x0B00, 0x0B7F } };
            yield return new object[] { engine, @"\p{IsTamil}", new[] { 0x0B80, 0x0BFF } };
            yield return new object[] { engine, @"\p{IsTelugu}", new[] { 0x0C00, 0x0C7F } };
            yield return new object[] { engine, @"\p{IsKannada}", new[] { 0x0C80, 0x0CFF } };
            yield return new object[] { engine, @"\p{IsMalayalam}", new[] { 0x0D00, 0x0D7F } };
            yield return new object[] { engine, @"\p{IsSinhala}", new[] { 0x0D80, 0x0DFF } };
            yield return new object[] { engine, @"\p{IsThai}", new[] { 0x0E00, 0x0E7F } };
            yield return new object[] { engine, @"\p{IsLao}", new[] { 0x0E80, 0x0EFF } };
            yield return new object[] { engine, @"\p{IsTibetan}", new[] { 0x0F00, 0x0FFF } };
            yield return new object[] { engine, @"\p{IsMyanmar}", new[] { 0x1000, 0x109F } };
            yield return new object[] { engine, @"\p{IsGeorgian}", new[] { 0x10A0, 0x10FF } };
            yield return new object[] { engine, @"\p{IsHangulJamo}", new[] { 0x1100, 0x11FF } };
            yield return new object[] { engine, @"\p{IsEthiopic}", new[] { 0x1200, 0x137F } };
            yield return new object[] { engine, @"\p{IsCherokee}", new[] { 0x13A0, 0x13FF } };
            yield return new object[] { engine, @"\p{IsUnifiedCanadianAboriginalSyllabics}", new[] { 0x1400, 0x167F } };
            yield return new object[] { engine, @"\p{IsOgham}", new[] { 0x1680, 0x169F } };
            yield return new object[] { engine, @"\p{IsRunic}", new[] { 0x16A0, 0x16FF } };
            yield return new object[] { engine, @"\p{IsTagalog}", new[] { 0x1700, 0x171F } };
            yield return new object[] { engine, @"\p{IsHanunoo}", new[] { 0x1720, 0x173F } };
            yield return new object[] { engine, @"\p{IsBuhid}", new[] { 0x1740, 0x175F } };
            yield return new object[] { engine, @"\p{IsTagbanwa}", new[] { 0x1760, 0x177F } };
            yield return new object[] { engine, @"\p{IsKhmer}", new[] { 0x1780, 0x17FF } };
            yield return new object[] { engine, @"\p{IsMongolian}", new[] { 0x1800, 0x18AF } };
            yield return new object[] { engine, @"\p{IsLimbu}", new[] { 0x1900, 0x194F } };
            yield return new object[] { engine, @"\p{IsTaiLe}", new[] { 0x1950, 0x197F } };
            yield return new object[] { engine, @"\p{IsKhmerSymbols}", new[] { 0x19E0, 0x19FF } };
            yield return new object[] { engine, @"\p{IsPhoneticExtensions}", new[] { 0x1D00, 0x1D7F } };
            yield return new object[] { engine, @"\p{IsLatinExtendedAdditional}", new[] { 0x1E00, 0x1EFF } };
            yield return new object[] { engine, @"\p{IsGreekExtended}", new[] { 0x1F00, 0x1FFF } };
            yield return new object[] { engine, @"\p{IsGeneralPunctuation}", new[] { 0x2000, 0x206F } };
            yield return new object[] { engine, @"\p{IsSuperscriptsandSubscripts}", new[] { 0x2070, 0x209F } };
            yield return new object[] { engine, @"\p{IsCurrencySymbols}", new[] { 0x20A0, 0x20CF } };
            yield return new object[] { engine, @"\p{IsCombiningDiacriticalMarksforSymbols}", new[] { 0x20D0, 0x20FF } };
            yield return new object[] { engine, @"\p{IsLetterlikeSymbols}", new[] { 0x2100, 0x214F } };
            yield return new object[] { engine, @"\p{IsNumberForms}", new[] { 0x2150, 0x218F } };
            yield return new object[] { engine, @"\p{IsArrows}", new[] { 0x2190, 0x21FF } };
            yield return new object[] { engine, @"\p{IsMathematicalOperators}", new[] { 0x2200, 0x22FF } };
            yield return new object[] { engine, @"\p{IsMiscellaneousTechnical}", new[] { 0x2300, 0x23FF } };
            yield return new object[] { engine, @"\p{IsControlPictures}", new[] { 0x2400, 0x243F } };
            yield return new object[] { engine, @"\p{IsOpticalCharacterRecognition}", new[] { 0x2440, 0x245F } };
            yield return new object[] { engine, @"\p{IsEnclosedAlphanumerics}", new[] { 0x2460, 0x24FF } };
            yield return new object[] { engine, @"\p{IsBoxDrawing}", new[] { 0x2500, 0x257F } };
            yield return new object[] { engine, @"\p{IsBlockElements}", new[] { 0x2580, 0x259F } };
            yield return new object[] { engine, @"\p{IsGeometricShapes}", new[] { 0x25A0, 0x25FF } };
            yield return new object[] { engine, @"\p{IsMiscellaneousSymbols}", new[] { 0x2600, 0x26FF } };
            yield return new object[] { engine, @"\p{IsDingbats}", new[] { 0x2700, 0x27BF } };
            yield return new object[] { engine, @"\p{IsMiscellaneousMathematicalSymbols-A}", new[] { 0x27C0, 0x27EF } };
            yield return new object[] { engine, @"\p{IsSupplementalArrows-A}", new[] { 0x27F0, 0x27FF } };
            yield return new object[] { engine, @"\p{IsBraillePatterns}", new[] { 0x2800, 0x28FF } };
            yield return new object[] { engine, @"\p{IsSupplementalArrows-B}", new[] { 0x2900, 0x297F } };
            yield return new object[] { engine, @"\p{IsMiscellaneousMathematicalSymbols-B}", new[] { 0x2980, 0x29FF } };
            yield return new object[] { engine, @"\p{IsSupplementalMathematicalOperators}", new[] { 0x2A00, 0x2AFF } };
            yield return new object[] { engine, @"\p{IsMiscellaneousSymbolsandArrows}", new[] { 0x2B00, 0x2BFF } };
            yield return new object[] { engine, @"\p{IsCJKRadicalsSupplement}", new[] { 0x2E80, 0x2EFF } };
            yield return new object[] { engine, @"\p{IsKangxiRadicals}", new[] { 0x2F00, 0x2FDF } };
            yield return new object[] { engine, @"\p{IsIdeographicDescriptionCharacters}", new[] { 0x2FF0, 0x2FFF } };
            yield return new object[] { engine, @"\p{IsCJKSymbolsandPunctuation}", new[] { 0x3000, 0x303F } };
            yield return new object[] { engine, @"\p{IsHiragana}", new[] { 0x3040, 0x309F } };
            yield return new object[] { engine, @"\p{IsKatakana}", new[] { 0x30A0, 0x30FF } };
            yield return new object[] { engine, @"\p{IsBopomofo}", new[] { 0x3100, 0x312F } };
            yield return new object[] { engine, @"\p{IsHangulCompatibilityJamo}", new[] { 0x3130, 0x318F } };
            yield return new object[] { engine, @"\p{IsKanbun}", new[] { 0x3190, 0x319F } };
            yield return new object[] { engine, @"\p{IsBopomofoExtended}", new[] { 0x31A0, 0x31BF } };
            yield return new object[] { engine, @"\p{IsKatakanaPhoneticExtensions}", new[] { 0x31F0, 0x31FF } };
            yield return new object[] { engine, @"\p{IsEnclosedCJKLettersandMonths}", new[] { 0x3200, 0x32FF } };
            yield return new object[] { engine, @"\p{IsCJKCompatibility}", new[] { 0x3300, 0x33FF } };
            yield return new object[] { engine, @"\p{IsCJKUnifiedIdeographsExtensionA}", new[] { 0x3400, 0x4DBF } };
            yield return new object[] { engine, @"\p{IsYijingHexagramSymbols}", new[] { 0x4DC0, 0x4DFF } };
            yield return new object[] { engine, @"\p{IsCJKUnifiedIdeographs}", new[] { 0x4E00, 0x9FFF } };
            yield return new object[] { engine, @"\p{IsYiSyllables}", new[] { 0xA000, 0xA48F } };
            yield return new object[] { engine, @"\p{IsYiRadicals}", new[] { 0xA490, 0xA4CF } };
            yield return new object[] { engine, @"\p{IsHangulSyllables}", new[] { 0xAC00, 0xD7AF } };
            yield return new object[] { engine, @"\p{IsHighSurrogates}", new[] { 0xD800, 0xDB7F } };
            yield return new object[] { engine, @"\p{IsHighPrivateUseSurrogates}", new[] { 0xDB80, 0xDBFF } };
            yield return new object[] { engine, @"\p{IsLowSurrogates}", new[] { 0xDC00, 0xDFFF } };
            yield return new object[] { engine, @"\p{IsPrivateUse}", new[] { 0xE000, 0xF8FF } };
            yield return new object[] { engine, @"\p{IsCJKCompatibilityIdeographs}", new[] { 0xF900, 0xFAFF } };
            yield return new object[] { engine, @"\p{IsAlphabeticPresentationForms}", new[] { 0xFB00, 0xFB4F } };
            yield return new object[] { engine, @"\p{IsArabicPresentationForms-A}", new[] { 0xFB50, 0xFDFF } };
            yield return new object[] { engine, @"\p{IsVariationSelectors}", new[] { 0xFE00, 0xFE0F } };
            yield return new object[] { engine, @"\p{IsCombiningHalfMarks}", new[] { 0xFE20, 0xFE2F } };
            yield return new object[] { engine, @"\p{IsCJKCompatibilityForms}", new[] { 0xFE30, 0xFE4F } };
            yield return new object[] { engine, @"\p{IsSmallFormVariants}", new[] { 0xFE50, 0xFE6F } };
            yield return new object[] { engine, @"\p{IsArabicPresentationForms-B}", new[] { 0xFE70, 0xFEFF } };
            yield return new object[] { engine, @"\p{IsHalfwidthandFullwidthForms}", new[] { 0xFF00, 0xFFEF } };
            yield return new object[] { engine, @"\p{IsSpecials}", new[] { 0xFFF0, 0xFFFF } };
            yield return new object[] { engine, @"\p{IsRunic}\p{IsHebrew}", new[] { 0x0590, 0x05FF, 0x16A0, 0x16FF } };
            yield return new object[] { engine, @"abx-z\p{IsRunic}\p{IsHebrew}", new[] { 0x0590, 0x05FF, 0x16A0, 0x16FF, 'a', 'a', 'b', 'b', 'x', 'x', 'y', 'z' } };
        }
    }

    [Theory]
    [MemberData(nameof(NamedBlocksInclusionsExpected_MemberData))]
    public async Task NamedBlocksInclusionsExpected(RegexEngine engine, string set, int[] ranges)
    {
        var included = new HashSet<char>();
        for (int i = 0; i < ranges.Length - 1; i += 2)
        {
            ComputeIncludedSet(c => c >= ranges[i] && c <= ranges[i + 1], included);
        }

        await ValidateSetAsync(engine, $"[{set}]", RegexOptions.None, included, null!);
        await ValidateSetAsync(engine, $"[^{set}]", RegexOptions.None, null!, included);
    }

    ////private static readonly HashSet<string> UniqueCharacterSet

    // hashset of unique elements made with all test data
    public void UnicodeCategory_Inclusion_Exclusion(string element)
    {
        // Get UC
        UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(element, 0);

        // Get regex format for UC
        string regexFormat = s_UnicodeCategoryRegexMap[uc];

        // assert match UC in regex format.

        // assert not match UC in regex format [^\p{UC}].
    }

    private static HashSet<char> ComputeIncludedSet(Func<char, bool> func)
    {
        var included = new HashSet<char>();
        ComputeIncludedSet(func, included);
        return included;
    }

    private static void ComputeIncludedSet(Func<char, bool> func, HashSet<char> included)
    {
        for (int i = 0; i <= char.MaxValue; i++)
        {
            if (func((char)i))
            {
                included.Add((char)i);
            }
        }
    }

    private static async Task ValidateSetAsync(RegexEngine engine, string regex, RegexOptions options, HashSet<char> included, HashSet<char> excluded, bool validateEveryChar = false)
    {
        Assert.True((included != null) ^ (excluded != null));

        Regex r = await GetRegexAsync(engine, regex, options);

        if (validateEveryChar)
        {
            for (int i = 0; i <= char.MaxValue; i++)
            {
                bool actual = r.IsMatch(((char)i).ToString());
                bool expected = included != null ? included.Contains((char)i) : !excluded!.Contains((char)i);
                if (actual != expected)
                {
                    Fail(i);
                }
            }
        }
        else if (included != null)
        {
            foreach (char c in included)
            {
                if (!r.IsMatch(c.ToString()))
                {
                    Fail(c);
                }
            }
        }
        else
        {
            foreach (char c in excluded!)
            {
                if (r.IsMatch(c.ToString()))
                {
                    Fail(c);
                }
            }
        }

        void Fail(int c) => throw new XunitException($"Set=\"{regex}\", Options=\"{options}\", {c:X4} => '{(char)c}'");
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
