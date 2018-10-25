namespace JK.NuGetTools.Cli.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class ConsoleUtils
    {
        public static IEnumerable<string> ToEnumerable(string[] providedValue, string defaultValue)
        {
            return providedValue == null ? (defaultValue ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries) : providedValue;
        }

        public static T ToEnum<T>(string providedValue, string defaultValue, bool ignoreCase = true)
            where T : Enum
        {
            return (T)Enum.Parse(typeof(T), providedValue ?? defaultValue, ignoreCase);
        }

        public static IEnumerable<Regex> ToRegexEnumerable(string[] filterValue, string defaultValueString)
        {
            return ToEnumerable(filterValue, defaultValueString).Select(i => new Regex(i));
        }
    }
}