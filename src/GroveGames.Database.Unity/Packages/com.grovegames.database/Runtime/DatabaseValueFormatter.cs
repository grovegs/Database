using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace GroveGames.Database.Unity
{
    public static class DatabaseValueFormatter
    {
        private const int MaxDepth = 8;
        private const int MaxItems = 200;
        private const string Indent = "  ";

        public static string Format(object? value)
        {
            var builder = new StringBuilder();
            Append(builder, value, 0);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, object? value, int depth)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    return;
                case string text:
                    builder.Append('"').Append(text).Append('"');
                    return;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    return;
                case Enum:
                    builder.Append(value);
                    return;
                case IFormattable formattable:
                    builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                    return;
            }

            if (depth >= MaxDepth)
            {
                builder.Append("...");
                return;
            }

            if (value is IDictionary dictionary)
            {
                AppendDictionary(builder, dictionary, depth);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                AppendList(builder, enumerable, depth);
                return;
            }

            AppendObject(builder, value, depth);
        }

        private static void AppendDictionary(StringBuilder builder, IDictionary dictionary, int depth)
        {
            if (dictionary.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append('{');
            var count = 0;

            foreach (DictionaryEntry entry in dictionary)
            {
                if (count == MaxItems)
                {
                    AppendLine(builder, depth + 1).Append("...");
                    break;
                }

                if (count > 0)
                {
                    builder.Append(',');
                }

                AppendLine(builder, depth + 1);
                builder.Append(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)).Append(": ");
                Append(builder, entry.Value, depth + 1);
                count++;
            }

            AppendLine(builder, depth).Append('}');
        }

        private static void AppendList(StringBuilder builder, IEnumerable enumerable, int depth)
        {
            builder.Append('[');
            var count = 0;

            foreach (var item in enumerable)
            {
                if (count == MaxItems)
                {
                    AppendLine(builder, depth + 1).Append("...");
                    break;
                }

                if (count > 0)
                {
                    builder.Append(',');
                }

                AppendLine(builder, depth + 1);
                Append(builder, item, depth + 1);
                count++;
            }

            if (count > 0)
            {
                AppendLine(builder, depth);
            }

            builder.Append(']');
        }

        private static void AppendObject(StringBuilder builder, object value, int depth)
        {
            var type = value.GetType();
            builder.Append('{');
            var count = 0;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                AppendMember(builder, field.Name, field.GetValue(value), depth, ref count);
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                AppendMember(builder, property.Name, property.GetValue(value), depth, ref count);
            }

            if (count > 0)
            {
                AppendLine(builder, depth);
            }

            builder.Append('}');
        }

        private static void AppendMember(StringBuilder builder, string name, object? value, int depth, ref int count)
        {
            if (count > 0)
            {
                builder.Append(',');
            }

            AppendLine(builder, depth + 1);
            builder.Append(name).Append(": ");
            Append(builder, value, depth + 1);
            count++;
        }

        private static StringBuilder AppendLine(StringBuilder builder, int depth)
        {
            builder.Append('\n');

            for (var i = 0; i < depth; i++)
            {
                builder.Append(Indent);
            }

            return builder;
        }
    }
}
