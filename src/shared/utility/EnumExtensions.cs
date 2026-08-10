using System.ComponentModel;
using System.Reflection;

namespace shared.utility;

public static class EnumExtensions
{
    public static string ToDescription(this Enum value)
    {
        Type type = value.GetType();

        MemberInfo[] memberInfo = type.GetMember(value.ToString());

        if (memberInfo.Length > 0)
        {
            object[] attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes.Length > 0)
            {
                return ((DescriptionAttribute)attributes[0]).Description;
            }
        }

        return value.ToString();
    }
}
