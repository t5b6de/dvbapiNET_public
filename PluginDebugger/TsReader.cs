using System;
using System.IO;

namespace PluginDebugger
{
    /// <summary>
    /// Simpler MPEG-TS Filereader, zu Testzwecken. es werden nur 188 Byte Packets unterstützt.
    /// Daten müssen schon sauber vorliegen!
    /// </summary>
    internal class TsReader : IDisposable
    {
        private FileStream _Fs;

        public TsReader(FileInfo fi)
        {
            _Fs = new FileStream(fi.FullName, FileMode.Open);
        }

        public byte[] ReadPacket(int count)
        {
            byte[] res = new byte[188 * count];
            if (_Fs.Read(res, 0, res.Length) > 0)
                return res;

            return null;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}