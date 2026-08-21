// <copyright file="ExperimentJsonSerializer.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Small .NET 3.5-compatible JSON serializer for experiment wire DTOs. Cities: Skylines' bundled
    /// Mono profile does not ship System.Web.Extensions, so depending on JavaScriptSerializer makes
    /// the entire mod assembly unloadable. This implementation deliberately supports the same JSON
    /// shapes used here: primitives, enums, arrays/lists, string-keyed dictionaries, and DTO properties.
    /// </summary>
    internal sealed class ExperimentJsonSerializer
    {
        private const int DefaultMaxJsonLength = 2097152;
        private const int DefaultRecursionLimit = 100;

        public ExperimentJsonSerializer()
        {
            MaxJsonLength = DefaultMaxJsonLength;
            RecursionLimit = DefaultRecursionLimit;
        }

        public int MaxJsonLength { get; set; }

        public int RecursionLimit { get; set; }

        public string Serialize(object value)
        {
            ValidateLimits();
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value, 0);
            if (builder.Length > MaxJsonLength)
            {
                throw new InvalidOperationException("The serialized JSON exceeds MaxJsonLength.");
            }

            return builder.ToString();
        }

        public T Deserialize<T>(string json)
        {
            ValidateLimits();
            if (json == null)
            {
                throw new ArgumentNullException("json");
            }

            if (json.Length > MaxJsonLength)
            {
                throw new InvalidOperationException("The JSON input exceeds MaxJsonLength.");
            }

            JsonParser parser = new JsonParser(json, RecursionLimit);
            object value = parser.Parse();
            return (T)ConvertValue(value, typeof(T), 0);
        }

        private static bool IsNumeric(Type type)
        {
            TypeCode code = Type.GetTypeCode(type);
            return code == TypeCode.Byte
                || code == TypeCode.SByte
                || code == TypeCode.Int16
                || code == TypeCode.UInt16
                || code == TypeCode.Int32
                || code == TypeCode.UInt32
                || code == TypeCode.Int64
                || code == TypeCode.UInt64
                || code == TypeCode.Single
                || code == TypeCode.Double
                || code == TypeCode.Decimal;
        }

        private static PropertyInfo[] GetSerializableProperties(Type type, bool requireSetter)
        {
            List<PropertyInfo> result = new List<PropertyInfo>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length != 0 || !property.CanRead || (requireSetter && !property.CanWrite))
                {
                    continue;
                }

                result.Add(property);
            }

            result.Sort(delegate(PropertyInfo left, PropertyInfo right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            });
            return result.ToArray();
        }

        private static object ConvertValue(object value, Type targetType, int depth)
        {
            if (depth > DefaultRecursionLimit * 4)
            {
                throw new InvalidOperationException("The converted JSON object graph is too deep.");
            }

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                return value == null ? null : ConvertValue(value, nullableType, depth + 1);
            }

            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType == typeof(object) || targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType == typeof(string))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(Guid))
            {
                return new Guid(Convert.ToString(value, CultureInfo.InvariantCulture));
            }

            if (targetType == typeof(DateTime))
            {
                return DateTime.Parse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
            }

            if (targetType.IsEnum)
            {
                if (value is string)
                {
                    return Enum.Parse(targetType, (string)value, false);
                }

                object underlying = Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture);
                return Enum.ToObject(targetType, underlying);
            }

            if (targetType == typeof(bool) || IsNumeric(targetType) || targetType == typeof(char))
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            IList sourceList = value as IList;
            if (targetType.IsArray)
            {
                if (sourceList == null)
                {
                    throw new InvalidOperationException("A JSON array was expected for " + targetType.FullName + ".");
                }

                Type elementType = targetType.GetElementType();
                Array array = Array.CreateInstance(elementType, sourceList.Count);
                for (int i = 0; i < sourceList.Count; ++i)
                {
                    array.SetValue(ConvertValue(sourceList[i], elementType, depth + 1), i);
                }

                return array;
            }

            Type listElementType = GetGenericInterfaceArgument(targetType, typeof(IList<>));
            if (listElementType != null)
            {
                if (sourceList == null)
                {
                    throw new InvalidOperationException("A JSON array was expected for " + targetType.FullName + ".");
                }

                Type concreteType = targetType.IsInterface || targetType.IsAbstract
                    ? typeof(List<>).MakeGenericType(listElementType)
                    : targetType;
                IList destination = (IList)Activator.CreateInstance(concreteType);
                foreach (object item in sourceList)
                {
                    destination.Add(ConvertValue(item, listElementType, depth + 1));
                }

                return destination;
            }

            IDictionary<string, object> sourceObject = value as IDictionary<string, object>;
            if (sourceObject == null)
            {
                throw new InvalidOperationException("A JSON object was expected for " + targetType.FullName + ".");
            }

            object destinationObject = Activator.CreateInstance(targetType);
            foreach (PropertyInfo property in GetSerializableProperties(targetType, true))
            {
                object propertyValue;
                if (sourceObject.TryGetValue(property.Name, out propertyValue))
                {
                    property.SetValue(
                        destinationObject,
                        ConvertValue(propertyValue, property.PropertyType, depth + 1),
                        null);
                }
            }

            return destinationObject;
        }

        private static Type GetGenericInterfaceArgument(Type type, Type genericInterface)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterface)
            {
                return type.GetGenericArguments()[0];
            }

            foreach (Type candidate in type.GetInterfaces())
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == genericInterface)
                {
                    return candidate.GetGenericArguments()[0];
                }
            }

            return null;
        }

        private void WriteValue(StringBuilder builder, object value, int depth)
        {
            if (depth > RecursionLimit)
            {
                throw new InvalidOperationException("The object graph exceeds RecursionLimit.");
            }

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            Type type = value.GetType();
            if (value is string || value is char || value is Guid)
            {
                WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is DateTime)
            {
                WriteString(builder, ((DateTime)value).ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (type.IsEnum)
            {
                builder.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (IsNumeric(type))
            {
                if (value is double && (double.IsNaN((double)value) || double.IsInfinity((double)value)))
                {
                    throw new InvalidOperationException("JSON cannot represent a non-finite Double.");
                }

                if (value is float && (float.IsNaN((float)value) || float.IsInfinity((float)value)))
                {
                    throw new InvalidOperationException("JSON cannot represent a non-finite Single.");
                }

                string format = value is double || value is float ? "R" : null;
                builder.Append(((IFormattable)value).ToString(format, CultureInfo.InvariantCulture));
                return;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                builder.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!(entry.Key is string))
                    {
                        throw new InvalidOperationException("Only string-keyed dictionaries can be serialized to JSON.");
                    }

                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteString(builder, (string)entry.Key);
                    builder.Append(':');
                    WriteValue(builder, entry.Value, depth + 1);
                }

                builder.Append('}');
                return;
            }

            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                builder.Append('[');
                bool first = true;
                foreach (object item in sequence)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteValue(builder, item, depth + 1);
                }

                builder.Append(']');
                return;
            }

            builder.Append('{');
            PropertyInfo[] properties = GetSerializableProperties(type, false);
            for (int i = 0; i < properties.Length; ++i)
            {
                if (i != 0)
                {
                    builder.Append(',');
                }

                WriteString(builder, properties[i].Name);
                builder.Append(':');
                WriteValue(builder, properties[i].GetValue(value, null), depth + 1);
            }

            builder.Append('}');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private void ValidateLimits()
        {
            if (MaxJsonLength <= 0)
            {
                throw new InvalidOperationException("MaxJsonLength must be positive.");
            }

            if (RecursionLimit <= 0)
            {
                throw new InvalidOperationException("RecursionLimit must be positive.");
            }
        }

        private sealed class JsonParser
        {
            private readonly string json;
            private readonly int recursionLimit;
            private int position;

            public JsonParser(string json, int recursionLimit)
            {
                this.json = json;
                this.recursionLimit = recursionLimit;
            }

            public object Parse()
            {
                SkipWhitespace();
                object result = ParseValue(0);
                SkipWhitespace();
                if (position != json.Length)
                {
                    throw Error("Unexpected trailing JSON content.");
                }

                return result;
            }

            private object ParseValue(int depth)
            {
                if (depth > recursionLimit)
                {
                    throw Error("The JSON input exceeds RecursionLimit.");
                }

                SkipWhitespace();
                if (position >= json.Length)
                {
                    throw Error("Unexpected end of JSON input.");
                }

                char token = json[position];
                if (token == '{')
                {
                    return ParseObject(depth + 1);
                }

                if (token == '[')
                {
                    return ParseArray(depth + 1);
                }

                if (token == '"')
                {
                    return ParseString();
                }

                if (token == '-' || (token >= '0' && token <= '9'))
                {
                    return ParseNumber();
                }

                if (TryConsume("true"))
                {
                    return true;
                }

                if (TryConsume("false"))
                {
                    return false;
                }

                if (TryConsume("null"))
                {
                    return null;
                }

                throw Error("Unexpected JSON token.");
            }

            private IDictionary<string, object> ParseObject(int depth)
            {
                Expect('{');
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Consume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (position >= json.Length || json[position] != '"')
                    {
                        throw Error("A JSON object property name must be a string.");
                    }

                    string name = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    result[name] = ParseValue(depth);
                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private IList ParseArray(int depth)
            {
                Expect('[');
                ArrayList result = new ArrayList();
                SkipWhitespace();
                if (Consume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder result = new StringBuilder();
                while (position < json.Length)
                {
                    char character = json[position++];
                    if (character == '"')
                    {
                        return result.ToString();
                    }

                    if (character != '\\')
                    {
                        if (character < 0x20)
                        {
                            throw Error("A JSON string contains an unescaped control character.");
                        }

                        result.Append(character);
                        continue;
                    }

                    if (position >= json.Length)
                    {
                        throw Error("An escape sequence is truncated.");
                    }

                    char escaped = json[position++];
                    switch (escaped)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ParseUnicodeEscape()); break;
                        default: throw Error("The JSON string contains an invalid escape sequence.");
                    }
                }

                throw Error("The JSON string is unterminated.");
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > json.Length)
                {
                    throw Error("A Unicode escape sequence is truncated.");
                }

                int value = 0;
                for (int i = 0; i < 4; ++i)
                {
                    char digit = json[position++];
                    value <<= 4;
                    if (digit >= '0' && digit <= '9')
                    {
                        value += digit - '0';
                    }
                    else if (digit >= 'a' && digit <= 'f')
                    {
                        value += digit - 'a' + 10;
                    }
                    else if (digit >= 'A' && digit <= 'F')
                    {
                        value += digit - 'A' + 10;
                    }
                    else
                    {
                        throw Error("A Unicode escape sequence contains a non-hexadecimal character.");
                    }
                }

                return (char)value;
            }

            private object ParseNumber()
            {
                int start = position;
                Consume('-');
                if (Consume('0'))
                {
                    if (position < json.Length && char.IsDigit(json[position]))
                    {
                        throw Error("A JSON number cannot contain a leading zero.");
                    }
                }
                else
                {
                    ConsumeDigits(true);
                }

                bool fractional = false;
                if (Consume('.'))
                {
                    fractional = true;
                    ConsumeDigits(true);
                }

                if (Consume('e') || Consume('E'))
                {
                    fractional = true;
                    Consume('+');
                    Consume('-');
                    ConsumeDigits(true);
                }

                string token = json.Substring(start, position - start);
                if (!fractional)
                {
                    long signed;
                    if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out signed))
                    {
                        return signed;
                    }

                    ulong unsigned;
                    if (ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out unsigned))
                    {
                        return unsigned;
                    }
                }

                double number;
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                    || double.IsNaN(number)
                    || double.IsInfinity(number))
                {
                    throw Error("The JSON number is invalid or outside the supported range.");
                }

                return number;
            }

            private void ConsumeDigits(bool requireOne)
            {
                int start = position;
                while (position < json.Length && char.IsDigit(json[position]))
                {
                    ++position;
                }

                if (requireOne && position == start)
                {
                    throw Error("A JSON number is missing a digit.");
                }
            }

            private bool TryConsume(string value)
            {
                if (position + value.Length > json.Length
                    || string.Compare(json, position, value, 0, value.Length, StringComparison.Ordinal) != 0)
                {
                    return false;
                }

                position += value.Length;
                return true;
            }

            private bool Consume(char value)
            {
                if (position < json.Length && json[position] == value)
                {
                    ++position;
                    return true;
                }

                return false;
            }

            private void Expect(char value)
            {
                if (!Consume(value))
                {
                    throw Error("Expected '" + value + "'.");
                }
            }

            private void SkipWhitespace()
            {
                while (position < json.Length)
                {
                    char character = json[position];
                    if (character != ' ' && character != '\t' && character != '\r' && character != '\n')
                    {
                        break;
                    }

                    ++position;
                }
            }

            private InvalidOperationException Error(string message)
            {
                return new InvalidOperationException(message + " Position " + position.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }
    }
}
