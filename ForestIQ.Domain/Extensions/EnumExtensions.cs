using System;
using System.Linq;
using System.Runtime.Serialization;

namespace ForestIQ.Domain.Extensions
{
    public static class EnumExtensions
    {
        public static string ToEnumString<TEnum>(this TEnum val) where TEnum : struct, Enum
        {
            var fieldInfo = val.GetType().GetField(val.ToString());
            if (fieldInfo != null)
            {
                var attr = fieldInfo.GetCustomAttributes(typeof(EnumMemberAttribute), false).FirstOrDefault() as EnumMemberAttribute;
                if (attr != null && attr.Value != null)
                {
                    return attr.Value;
                }
            }
            return val.ToString();
        }

        public static TEnum ToEnum<TEnum>(this string str) where TEnum : struct, Enum
        {
            var enumType = typeof(TEnum);
            foreach (var name in Enum.GetNames(enumType))
            {
                var fieldInfo = enumType.GetField(name);
                if (fieldInfo != null)
                {
                    var attr = fieldInfo.GetCustomAttributes(typeof(EnumMemberAttribute), false).FirstOrDefault() as EnumMemberAttribute;
                    if (attr != null && attr.Value == str)
                    {
                        return (TEnum)Enum.Parse(enumType, name);
                    }
                }
            }
            if (Enum.TryParse<TEnum>(str, true, out var result))
            {
                return result;
            }
            return default;
        }
    }
}
