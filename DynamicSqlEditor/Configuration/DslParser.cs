// File: DynamicSqlEditor/Configuration/DslParser.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DynamicSqlEditor.Common; // Assuming FileLogger is here

namespace DynamicSqlEditor.Configuration
{
    public class DslParser
    {
        // _sections persists across multiple Parse calls unless Clear() is called.
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        // _errors are cleared at the beginning of each Parse call.
        private readonly List<string> _errors = new List<string>();

        public IEnumerable<string> Errors => _errors;

        /// <summary>
        /// Parses the specified DSL file and merges its settings into the current state.
        /// Settings from this file will overwrite existing settings with the same section and key.
        /// Errors specific to this parse operation are cleared and then populated.
        /// </summary>
        /// <param name="filePath">The path to the DSL configuration file.</param>
        /// <returns>True if parsing completed without critical errors (duplicate key warnings are allowed), false otherwise.</returns>
        public bool Parse(string filePath)
        {
            // DO NOT CLEAR _sections here - this allows accumulation/overwriting across multiple calls.
            // _sections.Clear();

            // Clear errors specific to *this* parse operation.
            _errors.Clear();

            if (!File.Exists(filePath))
            {
                _errors.Add($"Configuration file not found: {filePath}");
                return false;
            }

            string currentSection = null;
            int lineNumber = 0;

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        line = line.Trim();

                        // Skip blank lines and comments
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        {
                            continue;
                        }

                        // Section header
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            currentSection = line.Substring(1, line.Length - 2).Trim();
                            // *** KEY CHANGE FOR ACCUMULATION ***
                            // Ensure the section dictionary exists, but DO NOT create a new one if it already does.
                            if (!_sections.ContainsKey(currentSection))
                            {
                                _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            }
                        }
                        // Key-value pair within a section
                        else if (currentSection != null)
                        {
                            int equalsIndex = line.IndexOf('=');
                            if (equalsIndex > 0)
                            {
                                string key = line.Substring(0, equalsIndex).Trim();
                                string value = line.Substring(equalsIndex + 1).Trim();

                                // Check for duplicates within the current file parse context (optional, but good for warnings)
                                // Note: This check is against the state *before* adding the new key.
                                // If the key already exists from a *previous* file parse, this won't trigger a warning here,
                                // which is correct behavior for overwriting.
                                if (_sections.ContainsKey(currentSection) && _sections[currentSection].ContainsKey(key))
                                {
                                    // Log difference based on whether it's overwriting from a *previous* file or just a duplicate in *this* file.
                                    // For simplicity now, we keep the original warning logic.
                                    _errors.Add($"Duplicate key '{key}' in section '[{currentSection}]' at line {lineNumber} (File: '{Path.GetFileName(filePath)}'). Using last value.");
                                }

                                // *** KEY CHANGE FOR ACCUMULATION ***
                                // This assignment will ADD the key/value if the key is new within the section,
                                // or UPDATE the value if the key already exists (either from earlier in this file or from a previous Parse call).
                                _sections[currentSection][key] = value;
                            }
                            else
                            {
                                _errors.Add($"Malformed key-value pair in section '[{currentSection}]' at line {lineNumber} (File: '{Path.GetFileName(filePath)}'): {line}");
                            }
                        }
                        // Orphan key-value pair (outside any section)
                        else
                        {
                            _errors.Add($"Key-value pair found outside of any section at line {lineNumber} (File: '{Path.GetFileName(filePath)}'): {line}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Error reading or parsing file '{filePath}': {ex.Message}");
                FileLogger.Error($"Error parsing DSL file '{filePath}'", ex); // Assuming FileLogger exists
                return false;
            }

            // Allow duplicate key warnings, but fail on other errors.
            return !_errors.Any(e => !e.Contains("Duplicate key"));
        }

        /// <summary>
        /// Gets the dictionary of key-value pairs for a specific section.
        /// Returns an empty dictionary if the section is not found.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <returns>A dictionary containing the settings for the section.</returns>
        public Dictionary<string, string> GetSection(string sectionName)
        {
            _sections.TryGetValue(sectionName, out var section);
            // Return a copy to prevent external modification? For now, return direct reference.
            return section ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets all parsed sections and their settings.
        /// </summary>
        /// <returns>An enumerable collection of section names and their corresponding settings dictionaries.</returns>
        public IEnumerable<KeyValuePair<string, Dictionary<string, string>>> GetAllSections()
        {
            return _sections;
        }

        /// <summary>
        /// Clears all parsed sections and errors, resetting the parser state.
        /// </summary>
        public void Clear()
        {
            _sections.Clear();
            _errors.Clear();
            FileLogger.Info("DslParser state cleared."); // Optional logging
        }


        /// <summary>
        /// Parses a string containing attributes in the format: Key1="Value1", Key2=Value2, Key3="Value With Spaces".
        /// Handles quoted and unquoted values.
        /// </summary>
        /// <param name="valueString">The string containing attributes.</param>
        /// <returns>A dictionary of attribute keys and values.</returns>
        public static Dictionary<string, string> ParseAttributes(string valueString)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(valueString)) return attributes;

            // Regex to match Key="Value" or Key=Value, separated by commas (or just whitespacetolerant)
            // Handles optional quotes around values. Captures Key and Value.
            // Matches Key=(QuotedValue | UnquotedValue) followed by optional comma/whitespace
            var regex = new Regex(@"(?<key>\w+)\s*=\s*(?:""(?<value>[^""]*)""|(?<value>[^,""]+))", RegexOptions.IgnoreCase);

            // Explanation:
            // (?<key>\w+)          : Match and capture the key (word characters)
            // \s*=\s*              : Match '=' surrounded by optional whitespace
            // (?:                   : Start non-capturing group for value options
            //   ""(?<value>[^""]*)"" : Match a quoted value. Capture content inside quotes into 'value' group. Handles empty quotes "".
            // |                     : OR
            //   (?<value>[^,""]+)  : Match an unquoted value (any character except comma or quote). Capture into 'value' group. Requires at least one char.
            // )                     : End non-capturing group

            var matches = regex.Matches(valueString);

            foreach (Match match in matches)
            {
                string key = match.Groups["key"].Value.Trim();
                // Get the captured value, which correctly handles quoted vs unquoted via the regex groups
                string value = match.Groups["value"].Value.Trim();
                attributes[key] = value;
            }
            return attributes;
        }
    }
}
