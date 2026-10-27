using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using GroveGames.Serialization;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseValueNode
    {
        private const int MaxItems = 200;
        private const string NullText = "null";
        private const string DateTimeFormat = "O";

        private readonly DatabaseValueNode? _parent;
        private readonly Action<object?>? _assign;
        private readonly Type _declaredType;
        private List<DatabaseValueNode>? _children;
        private bool _isTruncated;

        public string Label { get; }
        public string Path { get; }
        public object? Value { get; private set; }
        public DatabaseValueKind Kind { get; private set; }
        public Type ValueType { get; private set; }
        public bool IsEditable { get; }

        public bool IsTruncated
        {
            get
            {
                EnsureChildren();
                return _isTruncated;
            }
        }

        public IReadOnlyList<DatabaseValueNode> Children
        {
            get
            {
                EnsureChildren();
                return _children!;
            }
        }

        public string Text => Format(Value);

        private bool CanEditChildren => Value != null && (!Value.GetType().IsValueType || IsEditable);

        private DatabaseValueNode(DatabaseValueNode? parent, string label, string path, Type declaredType, object? value, bool isEditable, Action<object?>? assign)
        {
            _parent = parent;
            _assign = assign;
            _declaredType = declaredType;
            Label = label;
            Path = path;
            IsEditable = isEditable;
            Value = value;
            ValueType = typeof(object);
            Classify();
        }

        public static DatabaseValueNode CreateRoot(string label, string path, object? value, Action<object?> changed)
        {
            return new DatabaseValueNode(null, label, path, value?.GetType() ?? typeof(object), value, true, changed);
        }

        public bool TrySet(object? value)
        {
            if (!IsEditable || Kind == DatabaseValueKind.Object || Kind == DatabaseValueKind.List)
            {
                return false;
            }

            if (value == null ? !CanBeNull(ValueType) : !(Nullable.GetUnderlyingType(ValueType) ?? ValueType).IsInstanceOfType(value))
            {
                return false;
            }

            Value = value;
            _assign?.Invoke(value);
            _parent?.HandleChildChanged();
            return true;
        }

        public bool TrySetText(string text)
        {
            return TryParse(text, ValueType, out var value) && TrySet(value);
        }

        public static bool TryParse(string text, Type type, out object? value)
        {
            var underlying = Nullable.GetUnderlyingType(type);

            if (underlying != null)
            {
                if (text.Length == 0)
                {
                    value = null;
                    return true;
                }

                type = underlying;
            }

            var culture = CultureInfo.InvariantCulture;
            value = null;
            bool parsed;

            if (type == typeof(string))
            {
                value = text;
                return true;
            }

            if (type.IsEnum)
            {
                try
                {
                    value = Enum.Parse(type, text, true);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    parsed = bool.TryParse(text, out var boolean);
                    value = boolean;
                    break;
                case TypeCode.Char:
                    parsed = text.Length == 1;
                    value = parsed ? text[0] : default(char);
                    break;
                case TypeCode.SByte:
                    parsed = sbyte.TryParse(text, NumberStyles.Integer, culture, out var signedByte);
                    value = signedByte;
                    break;
                case TypeCode.Byte:
                    parsed = byte.TryParse(text, NumberStyles.Integer, culture, out var unsignedByte);
                    value = unsignedByte;
                    break;
                case TypeCode.Int16:
                    parsed = short.TryParse(text, NumberStyles.Integer, culture, out var shortValue);
                    value = shortValue;
                    break;
                case TypeCode.UInt16:
                    parsed = ushort.TryParse(text, NumberStyles.Integer, culture, out var unsignedShort);
                    value = unsignedShort;
                    break;
                case TypeCode.Int32:
                    parsed = int.TryParse(text, NumberStyles.Integer, culture, out var integer);
                    value = integer;
                    break;
                case TypeCode.UInt32:
                    parsed = uint.TryParse(text, NumberStyles.Integer, culture, out var unsignedInteger);
                    value = unsignedInteger;
                    break;
                case TypeCode.Int64:
                    parsed = long.TryParse(text, NumberStyles.Integer, culture, out var longValue);
                    value = longValue;
                    break;
                case TypeCode.UInt64:
                    parsed = ulong.TryParse(text, NumberStyles.Integer, culture, out var unsignedLong);
                    value = unsignedLong;
                    break;
                case TypeCode.Single:
                    parsed = float.TryParse(text, NumberStyles.Float, culture, out var single);
                    value = single;
                    break;
                case TypeCode.Double:
                    parsed = double.TryParse(text, NumberStyles.Float, culture, out var doubleValue);
                    value = doubleValue;
                    break;
                case TypeCode.Decimal:
                    parsed = decimal.TryParse(text, NumberStyles.Number, culture, out var decimalValue);
                    value = decimalValue;
                    break;
                case TypeCode.DateTime:
                    parsed = DateTime.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var dateTime);
                    value = dateTime;
                    break;
                default:
                    if (type == typeof(Guid))
                    {
                        parsed = Guid.TryParse(text, out var guid);
                        value = guid;
                    }
                    else if (type == typeof(TimeSpan))
                    {
                        parsed = TimeSpan.TryParse(text, culture, out var timeSpan);
                        value = timeSpan;
                    }
                    else if (type == typeof(DateTimeOffset))
                    {
                        parsed = DateTimeOffset.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var dateTimeOffset);
                        value = dateTimeOffset;
                    }
                    else
                    {
                        parsed = false;
                    }

                    break;
            }

            if (!parsed)
            {
                value = null;
            }

            return parsed;
        }

        public static string Format(object? value)
        {
            switch (value)
            {
                case null:
                    return NullText;
                case string text:
                    return text;
                case bool flag:
                    return flag ? "true" : "false";
                case Enum:
                    return value.ToString() ?? string.Empty;
                case DateTime dateTime:
                    return dateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                case DateTimeOffset dateTimeOffset:
                    return dateTimeOffset.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                case float single:
                    return single.ToString("R", CultureInfo.InvariantCulture);
                case double doubleValue:
                    return doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString() ?? string.Empty;
            }
        }

        private void Classify()
        {
            var type = Value?.GetType() ?? _declaredType;
            var scalarType = Nullable.GetUnderlyingType(_declaredType) ?? type;

            if (IsScalar(scalarType))
            {
                ValueType = Nullable.GetUnderlyingType(_declaredType) != null ? _declaredType : scalarType;
                Kind = scalarType == typeof(bool) ? DatabaseValueKind.Toggle : scalarType.IsEnum ? DatabaseValueKind.Enum : DatabaseValueKind.Text;
                return;
            }

            ValueType = type;

            if (Value == null)
            {
                Kind = DatabaseValueKind.Null;
                return;
            }

            Kind = Value is IEnumerable ? DatabaseValueKind.List : DatabaseValueKind.Object;
        }

        private void EnsureChildren()
        {
            if (_children != null)
            {
                return;
            }

            _children = new List<DatabaseValueNode>();
            _isTruncated = false;

            switch (Value)
            {
                case IDictionary dictionary when Kind == DatabaseValueKind.List:
                    AddDictionaryItems(dictionary);
                    break;
                case IList list when Kind == DatabaseValueKind.List:
                    AddListItems(list);
                    break;
                case IEnumerable enumerable when Kind == DatabaseValueKind.List:
                    AddItems(enumerable);
                    break;
                case { } target when Kind == DatabaseValueKind.Object:
                    AddMembers(target);
                    break;
            }
        }

        private void AddDictionaryItems(IDictionary dictionary)
        {
            var valueType = GetGenericArgument(dictionary.GetType(), typeof(IDictionary<,>), 1);
            var isEditable = CanEditChildren && !dictionary.IsReadOnly;

            foreach (DictionaryEntry entry in dictionary)
            {
                if (_children!.Count == MaxItems)
                {
                    _isTruncated = true;
                    return;
                }

                var key = entry.Key;
                var label = Format(key);
                _children.Add(new DatabaseValueNode(this, label, Path + "/" + label, valueType, entry.Value, isEditable, value => dictionary[key] = value));
            }
        }

        private void AddListItems(IList list)
        {
            var itemType = list.GetType().IsArray ? list.GetType().GetElementType()! : GetGenericArgument(list.GetType(), typeof(IList<>), 0);
            var isEditable = CanEditChildren && !list.IsReadOnly;

            for (var i = 0; i < list.Count; i++)
            {
                if (i == MaxItems)
                {
                    _isTruncated = true;
                    return;
                }

                var index = i;
                var label = "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                _children!.Add(new DatabaseValueNode(this, label, Path + "/" + label, itemType, list[index], isEditable, value => list[index] = value));
            }
        }

        private void AddItems(IEnumerable enumerable)
        {
            var itemType = GetGenericArgument(enumerable.GetType(), typeof(IEnumerable<>), 0);

            foreach (var item in enumerable)
            {
                if (_children!.Count == MaxItems)
                {
                    _isTruncated = true;
                    return;
                }

                var label = "[" + _children.Count.ToString(CultureInfo.InvariantCulture) + "]";
                _children.Add(new DatabaseValueNode(this, label, Path + "/" + label, itemType, item, false, null));
            }
        }

        private void AddMembers(object target)
        {
            var type = target.GetType();
            var canEdit = CanEditChildren;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsDefined(typeof(IgnoreAttribute), true))
                {
                    continue;
                }

                var isEditable = canEdit && !field.IsInitOnly && !field.IsLiteral;
                _children!.Add(new DatabaseValueNode(this, field.Name, Path + "/" + field.Name, field.FieldType, field.GetValue(target), isEditable, value => field.SetValue(target, value)));
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!IsStoredProperty(property) || property.IsDefined(typeof(IgnoreAttribute), true))
                {
                    continue;
                }

                var setter = property.GetSetMethod();
                var isEditable = canEdit && setter != null;
                _children!.Add(new DatabaseValueNode(this, property.Name, Path + "/" + property.Name, property.PropertyType, property.GetValue(target), isEditable, value => property.SetValue(target, value)));
            }
        }

        private void HandleChildChanged()
        {
            _assign?.Invoke(Value);
            _parent?.HandleChildChanged();
        }

        private static bool IsStoredProperty(PropertyInfo property)
        {
            var getter = property.GetGetMethod();

            if (getter == null || property.GetIndexParameters().Length > 0)
            {
                return false;
            }

            return property.GetSetMethod() != null || getter.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }

        private static bool IsScalar(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(Guid)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan);
        }

        private static bool CanBeNull(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private static Type GetGenericArgument(Type type, Type definition, int index)
        {
            foreach (var implemented in type.GetInterfaces())
            {
                if (implemented.IsGenericType && implemented.GetGenericTypeDefinition() == definition)
                {
                    return implemented.GetGenericArguments()[index];
                }
            }

            return typeof(object);
        }
    }
}
