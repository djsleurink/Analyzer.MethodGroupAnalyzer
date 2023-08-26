using System.Collections.Generic;

namespace MethodGroupRecreatesDelegate
{
    internal class SupportedFrameworks
    {
        private static readonly string[] _frameworks = new string[]
        {
            ".NETFramework",
            ".NETStandard",
            ".NETCoreApp,Version=v3",
            ".NETCoreApp,Version=v4",
            ".NETCoreApp,Version=v5",
            ".NETCoreApp,Version=v6",
        };

        public static string[] Frameworks => _frameworks;
    }
}