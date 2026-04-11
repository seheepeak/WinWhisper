namespace WinWhisper.Common.Constants;

/// <summary>
/// Maps Windows Language IDs (LANGID) to Whisper-supported language codes.
/// </summary>
public static class LanguageMappings
{
    /// <summary>
    /// Windows LANGID to Whisper language code mapping.
    /// </summary>
    /// <remarks>
    /// References:
    /// - Windows Language IDs: https://learn.microsoft.com/en-us/windows/win32/intl/language-identifier-constants-and-strings
    /// - Whisper Languages: https://whisper-api.com/docs/languages/
    /// </remarks>
    public static readonly IReadOnlyDictionary<ushort, string> LangIdToCode = new Dictionary<ushort, string>
    {
        // Arabic
        { 1, "ar" },   // Arabic - Legacy
        { 1025, "ar" },   // Arabic - Saudi Arabia
        { 2049, "ar" },   // Arabic - Iraq
        { 3073, "ar" },   // Arabic - Egypt
        { 5121, "ar" },   // Arabic - Algeria

        // Chinese
        { 4, "zh" },      // Chinese (Simplified) - Legacy
        { 2052, "zh" },   // Chinese - PRC (Simplified)
        { 4100, "zh" },   // Chinese - Singapore
        { 1028, "zh" },   // Chinese - Taiwan (Traditional)
        { 3076, "yue" },  // Chinese - Hong Kong SAR (Cantonese)
        { 5124, "zh" },   // Chinese - Macao SAR

        // English
        { 9, "en" },      // English - Legacy
        { 1033, "en" },   // English - United States
        { 2057, "en" },   // English - United Kingdom
        { 3081, "en" },   // English - Australia
        { 4105, "en" },   // English - Canada
        { 6153, "en" },   // English - Ireland
        { 7177, "en" },   // English - South Africa
        { 10249, "en" },  // English - Belize

        // Spanish
        { 1034, "es" },   // Spanish - Spain (Traditional Sort)
        { 2058, "es" },   // Spanish - Mexico
        { 3082, "es" },   // Spanish - Spain (Modern Sort)
        { 4106, "es" },   // Spanish - Guatemala
        { 5130, "es" },   // Spanish - Costa Rica
        { 7178, "es" },   // Spanish - Dominican Republic
        { 11274, "es" },  // Spanish - Argentina
        { 16394, "es" },  // Spanish - Bolivia

        // French
        { 1036, "fr" },   // French - France
        { 2060, "fr" },   // French - Belgium
        { 3084, "fr" },   // French - Canada
        { 4108, "fr" },   // French - Switzerland
        { 5132, "fr" },   // French - Luxembourg

        // German
        { 1031, "de" },   // German - Germany
        { 2055, "de" },   // German - Switzerland
        { 3079, "de" },   // German - Austria
        { 4103, "de" },   // German - Luxembourg
        { 5127, "de" },   // German - Liechtenstein

        // Japanese
        { 1041, "ja" },   // Japanese

        // Korean
        { 1042, "ko" },   // Korean

        // Portuguese
        { 1046, "pt" },   // Portuguese - Brazil
        { 2070, "pt" },   // Portuguese - Portugal

        // Russian
        { 1049, "ru" },   // Russian

        // Italian
        { 1040, "it" },   // Italian - Italy
        { 2064, "it" },   // Italian - Switzerland

        // Dutch
        { 1043, "nl" },   // Dutch - Netherlands
        { 2067, "nl" },   // Dutch - Belgium

        // Polish
        { 1045, "pl" },   // Polish

        // Turkish
        { 1055, "tr" },   // Turkish

        // Swedish
        { 1053, "sv" },   // Swedish - Sweden
        { 2077, "sv" },   // Swedish - Finland

        // Norwegian
        { 1044, "no" },   // Norwegian - Bokmal
        { 2068, "no" },   // Norwegian - Nynorsk → no (Whisper doesn't distinguish)

        // Danish
        { 1030, "da" },   // Danish

        // Finnish
        { 1035, "fi" },   // Finnish

        // Greek
        { 1032, "el" },   // Greek

        // Czech
        { 1029, "cs" },   // Czech

        // Hungarian
        { 1038, "hu" },   // Hungarian

        // Romanian
        { 1048, "ro" },   // Romanian

        // Slovak
        { 1051, "sk" },   // Slovak

        // Croatian
        { 1050, "hr" },   // Croatian

        // Bulgarian
        { 1026, "bg" },   // Bulgarian

        // Hebrew
        { 1037, "he" },   // Hebrew

        // Thai
        { 1054, "th" },   // Thai

        // Vietnamese
        { 1066, "vi" },   // Vietnamese

        // Indonesian
        { 1057, "id" },   // Indonesian

        // Hindi
        { 1081, "hi" },   // Hindi

        // Ukrainian
        { 1058, "uk" },   // Ukrainian

        // Estonian
        { 1061, "et" },   // Estonian

        // Latvian
        { 1062, "lv" },   // Latvian

        // Lithuanian
        { 1063, "lt" },   // Lithuanian

        // Slovenian
        { 1060, "sl" },   // Slovenian

        // Azerbaijani
        { 1068, "az" },   // Azerbaijani - Latin
        { 2092, "az" },   // Azerbaijani - Cyrillic

        // Persian
        { 1065, "fa" },   // Persian

        // Urdu
        { 1056, "ur" },   // Urdu

        // Bengali
        { 1093, "bn" },   // Bengali - India
        { 2117, "bn" },   // Bengali - Bangladesh

        // Tamil
        { 1097, "ta" },   // Tamil

        // Telugu
        { 1098, "te" },   // Telugu

        // Marathi
        { 1102, "mr" },   // Marathi

        // Gujarati
        { 1095, "gu" },   // Gujarati

        // Punjabi
        { 1094, "pa" },   // Punjabi - India

        // Kannada
        { 1099, "kn" },   // Kannada

        // Malayalam
        { 1100, "ml" },   // Malayalam

        // Serbian
        { 3098, "sr" },   // Serbian - Cyrillic
        { 2074, "sr" },   // Serbian - Latin

        // Bosnian
        { 5146, "bs" },   // Bosnian - Latin
        { 8218, "bs" },   // Bosnian - Cyrillic

        // Macedonian
        { 1071, "mk" },   // Macedonian

        // Albanian
        { 1052, "sq" },   // Albanian

        // Armenian
        { 1067, "hy" },   // Armenian

        // Georgian
        { 1079, "ka" },   // Georgian

        // Kazakh
        { 1087, "kk" },   // Kazakh

        // Uzbek
        { 1091, "uz" },   // Uzbek - Latin
        { 2115, "uz" },   // Uzbek - Cyrillic

        // Catalan
        { 1027, "ca" },   // Catalan

        // Basque
        { 1069, "eu" },   // Basque

        // Galician
        { 1110, "gl" },   // Galician

        // Belarusian
        { 1059, "be" },   // Belarusian

        // Icelandic
        { 1039, "is" },   // Icelandic

        // Malay
        { 1086, "ms" },   // Malay - Malaysia
        { 2110, "ms" },   // Malay - Brunei

        // Swahili
        { 1089, "sw" },   // Swahili

        // Afrikaans
        { 1078, "af" },   // Afrikaans

        // Welsh
        { 1106, "cy" },   // Welsh

        // Nepali
        { 1121, "ne" },   // Nepali

        // Sinhala
        { 1115, "si" },   // Sinhala

        // Khmer
        { 1107, "km" },   // Khmer

        // Lao
        { 1108, "lo" },   // Lao

        // Mongolian
        { 1104, "mn" },   // Mongolian - Cyrillic
        { 2128, "mn" },   // Mongolian - Mongolian

        // Amharic
        { 1118, "am" },   // Amharic
    };
}