using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DiscUtils.CoreCompat
{
    internal static class ReflectionHelper
    {
        public static Attribute? GetCustomAttribute(Type type, Type attributeType, bool inherit)
        {
            return Attribute.GetCustomAttribute(type, attributeType);
        }

        public static Assembly GetAssembly(Type type)
        {
            return type.Assembly;
        }
    }
}