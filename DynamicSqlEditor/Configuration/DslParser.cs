using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration.Models;

namespace DynamicSqlEditor.Configuration
{
    public class DslParser
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _errors = new List<string>();

        public IEnumerable<string> Errors => _errors;

        public bool Parse(string filePath)
        {
            _sections.Clear();
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

                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        {
                            continue;
                        }

                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            currentSection = line.Substring(1, line.Length - 2).Trim();
                            if (!_sections.ContainsKey(currentSection))
                            {
                                _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            }
                        }
                        else if (currentSection != null)
                        {
                            int equalsIndex = line.IndexOf('=');
                            if (equalsIndex > 0)
                            {
                                string key = line.Substring(0, equalsIndex).Trim();
                                string value = line.Substring(equalsIndex + 1).Trim();

                                if (_sections[currentSection].ContainsKey(key))
                                {
                                    _errors.Add($"Duplicate key '{key}' in section '[{currentSection}]' at line {lineNumber}. Using last value.");
                                }
                                _sections[currentSection][key] = value;
                            }
                            else
                            {
                                _errors.Add($"Malformed key-value pair in section '[{currentSection}]' at line {lineNumber}: {line}");
                            }
                        }
                        else
                        {
                            _errors.Add($"Key-value pair found outside of any section at line {lineNumber}: {line}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Error reading or parsing file '{filePath}': {ex.Message}");
                FileLogger.Error($"Error parsing DSL file '{filePath}'", ex);
                return false;
            }

            return !_errors.Any(e => !e.Contains("Duplicate key")); // Allow duplicate key warnings
        }

        public Dictionary<string, string> GetSection(string sectionName)
        {
            _sections.TryGetValue(sectionName, out var section);
            return section ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<KeyValuePair<string, Dictionary<string, string>>> GetAllSections()
        {
            return _sections;
        }

        public static Dictionary<string, string> ParseAttributes(string valueString)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(valueString)) return attributes;

            var regex = new Regex(@"(?<key>\w+)\s*=\s*(?:""(?<value>[^""]*)""|(?<value>[^,]+))", RegexOptions.IgnoreCase);
            var matches = regex.Matches(valueString);

            foreach (Match match in matches)
            {
                string key = match.Groups["key"].Value.Trim();
                string value = match.Groups["value"].Value.Trim();
                attributes[key] = value;
            }
            return attributes;
        }
    }
}