using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Utils
{
    internal class StringUtils
    {
        public static bool IsEqual(StringBuilder sb, string str)
        {
            if (sb.Length != str.Length)
            {
                return false;
            }

            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] != str[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
