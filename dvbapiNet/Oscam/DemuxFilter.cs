using dvbapiNet.Dvb;
using System;
using System.Collections.Generic;

namespace dvbapiNet.Oscam
{
    /// <summary>
    /// Bildet einen Linux DVB-Filter nach. Filtert die Sections nach Anforderung des CA-Moduls.
    /// https://github.com/torvalds/linux/blob/master/include/uapi/linux/dvb/dmx.h Zeile 173 (2019/10/19)
    /// </summary>
    internal class DemuxFilter
    {
        [Flags]
        public enum Flags : int
        {
            CheckCrc = 1,
            OneShot = 2,
            ImmediateStart = 4
        }

        private byte[] _Data;

        private Flags _Flags;

        private byte[] _Mask;

        private byte[] _Mode;

        private SectionBase _Section;

        public byte[] FilterData
        {
            get
            {
                return _Data;
            }
        }

        public byte[] FilterMask
        {
            get
            {
                return _Mask;
            }
        }

        public byte[] FilterMode
        {
            get
            {
                return _Mode;
            }
        }

        public int Number
        {
            get;
        }

        public int Pid
        {
            get;
        }

        public DemuxFilter(int number, int pid, byte[] data, byte[] mask, byte[] mode, int timeout, Flags flags)
        {
            _Data = data;
            _Mask = mask;
            _Mode = mode;
            _Flags = flags;
            Pid = pid;

            Number = number;

            _Section = new SectionBase(pid, _Flags.HasFlag(Flags.CheckCrc));
        }

        public bool AddPacket(IntPtr tsPacket)
        {
            return _Section.AddPacket(tsPacket);
        }

        public int CountSections()
        {
            int c = 0;

            SectionBase s = _Section;

            while (s != null)
            {
                c++;
                s = s.NextSection;
            }

            return c;
        }

        public byte[][] GetFilteredSections()
        {
            List<byte[]> filtered = new List<byte[]>();

            SectionBase s = _Section;
            byte[] f;

            while (s != null)
            {
                if (s.Data == null)
                    break;

                if (Filter(s.Data, 0))
                {
                    f = new byte[s.SectionSize];
                    Array.Copy(s.Data, f, f.Length);
                    filtered.Add(f);
                }
                s = s.NextSection;
            }

            return filtered.ToArray();
        }

        /// <summary>
        /// Gibt die letzte Section zurück. Sinnvoll um dort weiter fortzufahren,
        /// wenn diese noch nicht abgeschlossen ist.
        /// </summary>
        /// <returns></returns>
        public SectionBase GetLastSection()
        {
            SectionBase s = _Section;

            while (s != null)
            {
                if (s.NextSection == null)
                    return s;

                s = s.NextSection;
            }

            return null;
        }

        public SectionBase GetSection(int index)
        {
            int c = 0;

            SectionBase s = _Section;

            while (s != null)
            {
                if (c++ == index)
                    return s;

                s = s.NextSection;
            }

            return null;
        }

        /// <summary>
        /// Sollte nur nach Addpacket verwendet werden.
        /// Setzt letzte unvollständige Section als nächste Section.
        /// Falls Section alle voll wird eine neue gestartet.
        /// </summary>
        public void Reset()
        {
            // prüfen ob letzte Section vollständig oder nicht
            SectionBase last = GetLastSection();

            if (last.Finished)
            {
                _Section = new SectionBase(Pid, _Flags.HasFlag(Flags.CheckCrc));
            }
            else
            {
                _Section = last;
            }
        }

        private bool Filter(byte[] toFilter, int offset)
        {
            for (int i = 0, k = 0; i < 16; i++, k++)
            {
                if (k == 1)
                    k += 2; // längenbytes skippen.

                if (((toFilter[k + offset] ^ _Mode[i]) & _Mask[i]) != (_Data[i] & _Mask[i])) // & _Mask erforderlich, da oscam hier auf data die Maske nicht vorher schon anwendet.
                    return false;
            }

            return true;
        }
    }
}
