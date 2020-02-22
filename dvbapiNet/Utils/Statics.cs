using System.Text;

namespace dvbapiNet.Utils
{
    internal static class Statics
    {
        public static string GetStringFromC(int offset, byte[] data, int maxLen)
        {
            int i = 0;

            for (; i < data.Length && i < maxLen; i++)
            {
                if (data[i + offset] == 0)
                    break;
            }

            return Encoding.UTF8.GetString(data, offset, i);
        }

        public static string GetStringFromC(int offset, byte[] data)
        {
            return GetStringFromC(offset, data, data.Length);
        }

        public static string GetStringFromC(byte[] data)
        {
            return GetStringFromC(0, data, data.Length);
        }
    }
}
