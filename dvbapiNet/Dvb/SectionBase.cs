using dvbapiNet.Log.Locale;
using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

namespace dvbapiNet.Dvb
{
    public class SectionBase
    {
        /// <summary>
        /// Gibt an, ob alle Bytes "empfangen" wurden.
        /// </summary>
        private bool _Completed;

        private int _ContCtr;

        private bool _DoCrc;

        private byte[] _AllowedTables;

        /// <summary>
        /// Gibt an, ob alles abgeschlossen ist, einschließlich korrektem CRC.
        /// </summary>
        private bool _Finished;

        private byte _PacketCtr;

        /// <summary>
        /// Pid über die Pakete angenommen werden sollen. -1 für ignorieren.
        /// 0 für PAT, 1 für CAT, 17 für SDT, usw.
        /// </summary>
        private int _Pid;

        private int _Ptr;

        /// <summary>
        /// enthält die Daten einer Section.
        /// </summary>
        private byte[] _SectionData;

        public bool Accepts184ByteTs
        {
            get
            {
                return _AllowedTables != null;
            }
        }

        public int CurrentNext
        {
            get
            {
                if (_Finished)
                {
                    return _SectionData[5] & 0x01;
                }
                else
                {
                    return -1;
                }
            }
        }

        public byte[] Data
        {
            get
            {
                if (_Finished)
                    return _SectionData;

                return null;
            }
        }

        public bool Finished
        {
            get
            {
                return _Finished;
            }
        }

        public int LastSectionNumber
        {
            get
            {
                if (_Finished)
                {
                    return _SectionData[7];
                }
                else
                {
                    return -1;
                }
            }
        }

        public SectionBase NextSection
        {
            get;
            private set;
        }

        public int SectionNumber
        {
            get
            {
                if (_Finished)
                {
                    return _SectionData[6];
                }
                else
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// Gibt die Gesamtgröße der Section an, inkl Table-ID und Größenangabe.
        /// Der Speicher der benötigt wird, um eine komplette Section abzubilden.
        /// </summary>
        public int SectionSize
        {
            get
            {
                if (_Ptr > 2)
                {
                    int u = _SectionData[1];
                    int l = _SectionData[2];

                    u &= 0x0f;
                    l |= u << 8;

                    // Section ist immer mind. 3 bytes groß, table ID 1byte,
                    // und größenangabe (2 byte)
                    return l + 3;
                }
                else
                {
                    return -1;
                }
            }
        }

        public int SyntaxIndicator
        {
            get
            {
                if (_Finished)
                {
                    return (_SectionData[1] & 0x80) >> 7;
                }
                else
                {
                    return -1;
                }
            }
        }

        public int TableId
        {
            get
            {
                if (_Finished)
                {
                    return _SectionData[0];
                }
                else
                {
                    return -1;
                }
            }
        }

        public int Version
        {
            get
            {
                if (_Finished)
                {
                    return (_SectionData[5] >> 1) & 0x1f;
                }
                else
                {
                    return -1;
                }
            }
        }

        protected byte[] SectionData
        {
            get
            {
                return _SectionData;
            }
        }

        public SectionBase(int pid, bool crc)
        {
            _SectionData = new byte[4096];
            _DoCrc = crc;

            Reset(); // lädt auch Reset von abgeleiteter Klasse.
            _Pid = pid;
        }

        /// <summary>
        /// Instantiiert SectionBase im 184 Byte TS-Packet Modus.
        /// </summary>
        /// <param name="pid">pid der TS-Packets</param>
        /// <param name="allowedTables">Byte-Array mit den erlaubten Table-IDs</param>
        public SectionBase(int pid, byte[] allowedTables)
        {
            _SectionData = new byte[4096];
            _DoCrc = false; // wird später anhand table-ID ermittelt.
            _Pid = pid;

            if (allowedTables == null)
            {
                switch (_Pid)
                {
                    case 0x00: // PAT
                        _AllowedTables = new byte[] { 0x00 };
                        break;

                    case 0x01: // CAT
                        _AllowedTables = new byte[] { 0x01 };
                        break;

                    case 0x10: // NIT / ST
                        _AllowedTables = new byte[] { 0x40, 0x41 };
                        break;

                    case 0x11: // SDT/BAT/ST
                        _AllowedTables = new byte[] { 0x42, 0x46, 0x4a };
                        break;
                }
            }
            else
            {
                _AllowedTables = allowedTables;
            }

            if (allowedTables == null || allowedTables.Length == 0)
            {
                throw new Exception(MessageProvider.FormatMessage(Message.SectionBaseTableReqd));
            }

            Reset();
        }

        public SectionBase(byte[] source, int offset, int pid, bool crc)
        {
            _SectionData = new byte[4096];
            _DoCrc = crc;

            int u = source[1 + offset];
            int l = source[2 + offset];

            u &= 0x0f;
            l |= u << 8;
            l += 3;

            Array.Copy(source, offset, SectionData, 0, l);
            _Pid = pid;

            _Ptr = l;

            _Completed = true;
            _Finished = !_DoCrc || SectionCrc.Compare(_SectionData, l, 0x00000000);
        }

        public virtual bool AddPacket(IntPtr tsPacket)
        {
            if (Accepts184ByteTs)
                return AddPacket184(tsPacket, 0);

            return AddPacket(tsPacket, 0);
        }

        public virtual bool AddPacket(byte[] tsPacket)
        {
            if (Accepts184ByteTs)
                return AddPacket184(tsPacket, 0);

            return AddPacket(tsPacket, 0);
        }

        public virtual void Reset()
        {
            for (int i = 0; i < _SectionData.Length; i++)
            {
                _SectionData[i] = 0xff;
            }

            _PacketCtr = 0;
            _Ptr = 0;
            _Completed = false;
            _Finished = false;
        }

        public void UpdatePid(int pid)
        {
            if (pid >= -1 && pid <= 0x1fff)
            {
                _Pid = pid;
                Reset();
            }
        }

        public void WriteToStream(Stream s)
        {
            if (SectionSize > 0)
                s.Write(_SectionData, 0, SectionSize);
        }

        /// <summary>
        /// Fügt ein Paket der Section hinzu.
        /// </summary>
        /// <param name="tsPacket">MPEG Transport-Strom-Paket</param>
        /// <param name="plOffset">0 wenn erste Section in dem Packet. größer 0 wenn weitere packers vorhanden.</param>
        /// <returns>True wenn letzte benötigte Paket eingelesen und Prüfsumme korrekt berechnet wurde.</returns>
        private bool AddPacket(IntPtr tsPacket, int plOffset)
        {
            lock (this)
            {
                if (_Finished)
                    return true;

                // Dann sind Daten fehlerhaft, also Neustart:
                if (_Completed && !_Finished)
                    Reset();

                int tmp = IPAddress.NetworkToHostOrder(Marshal.ReadInt16(tsPacket, 1));

                int err = tmp & 0x8000;
                int start = tmp & 0x4000;
                int prio = tmp & 0x2000;
                int pid = tmp & 0x1fff;

                tmp = Marshal.ReadByte(tsPacket, 3);

                int scrambling = (tmp & 0xc0) >> 6; // 0 = unscrambled, 2 = even CW, 3 = odd cw, 1 = ?
                int adaption = (tmp & 0x30) >> 4; // 0 = invalid, 1 = payload only, 2 = a. only, 3 = a folow pl
                int contCtr = tmp & 0x0f;

                int plPointer = Marshal.ReadByte(tsPacket, 4);
                // nur von Wichtigkeit, wenn PL Start.
                // Ansonsten kann das letzte Packet auch in PL Start haben, der Start aber später erst anfängt.
                // PDF ISO/IEC 13818-1 : 2000 (E) Seite 19 (reader: 37) Punkt 2.4.3.3 payload_unit_start_indicator.
                // wenn adaption field gesetzt gilt dies auch

                // bei nicht passender Pid raus:
                if (pid != _Pid && _Pid != -1)
                    return false;

                // Bei fehlerhaften Packets raus:
                if (err != 0)
                    return false;

                // Wenn noch kein Payload-Start und kein Paket eingelesen wurde, dann raus, ebenso wenn adaption-control 0 ist.
                if ((_PacketCtr == 0 && start == 0) || adaption == 0)
                {
                    return false;
                }

                // wenn schon Packets vorhanden, und wieder PL Start, dann Reset, neuer Anlauf.
                // entfernt. Siehe oben plPointer Kommentar.
                //if (_PacketCtr && start)
                //{
                //	Reset();
                //}

                // start und !_packetCtr
                // !start und _packetCtr <-- hier contCtr prüfen!

                if (start == 0 && _PacketCtr != 0 &&
                    ((_ContCtr + 1) & 0x0f) != contCtr)
                {
                    // Dann fehlt ein oder mehrere packets. Reset und raus.
                    Reset();
                    return false;
                }

                // hier dürfte nun nichts falsches mehr sein. Wenn doch was schief geht, stimmt CRC am ende nicht.

                // nichts zu kopieren, raus:
                if (adaption == 2)
                {
                    return false;
                }

                // start des Payloads ermitteln, wenn Adaption-Field vorhanden
                //int plOffset = 0;

                // wenn != 0, dann ist das ein Nested Call für weitere Sections pro packet, z.b. bei EMM.
                if (plOffset == 0 && (adaption == 3 || start != 0))
                {
                    plOffset += 1 + plPointer;
                }

                int len = (188 - 4) - plOffset;
                int remain;

                // länge aus Header ermitteln, damit packetize später stimmt, sofern erstes packet...
                if (_PacketCtr == 0)
                {
                    int u = Marshal.ReadByte(tsPacket, 4 + plOffset + 1);
                    int l = Marshal.ReadByte(tsPacket, 4 + plOffset + 2);

                    u &= 0x0f;
                    l |= u << 8;

                    remain = l + 3;
                }
                else
                {
                    remain = SectionSize - _Ptr;
                }

                if (remain < len)
                    len = remain;

                if (_Ptr + len > _SectionData.Length)
                    len -= _SectionData.Length - _Ptr; // einkürzen, damit in Buffer passt.

                if (len <= 0)
                {
                    Reset();
                    return false; // fehler
                }

                Marshal.Copy(tsPacket + (4 + plOffset), SectionData, _Ptr, len);

                // zähler setzen:
                _ContCtr = contCtr;
                _PacketCtr++;

                _Ptr += len;

                int size = SectionSize;

                // wenn rest vom vorherige section oder adaption field zu lang ist, könnte teil der Section-Length fehlen
                // daher immer auf size prüfen.
                _Completed = size > 3 && (_Ptr >= size);

                if (_Completed)
                {
                    // dann CRC prüfen, wenn gefordert, ECMs sind z.B. ohne.:
                    _Finished = !_DoCrc || SectionCrc.Compare(_SectionData, size, 0x00000000);

                    if (_Finished && start != 0 && _SectionData[size] != 0xff) // prüfen ob weitere Sections vorhanden sind:
                    {
                        NextSection = new SectionBase(_Pid, _DoCrc);
                        NextSection.AddPacket(tsPacket, plOffset + size); // offsetstart + Section-Länge = start nächste Section
                    }
                }

                return _Finished;
            }
        }

        /// <summary>
        /// Fügt ein Paket ohne Header der Section hinzu.
        /// </summary>
        /// <param name="tsPacket">MPEG Transport-Strom-Paket ohne Header</param>
        /// <param name="plOffset">0 wenn erste Section in dem Packet. größer 0 wenn weitere packers vorhanden.</param>
        /// <returns>True wenn letzte benötigte Paket eingelesen und Prüfsumme korrekt berechnet wurde.</returns>
        private bool AddPacket184(IntPtr tsPacket, int plOffset)
        {
            if (_AllowedTables == null)
                throw new InvalidOperationException("Section wurde nicht korrekt initialisiert um 184 Byte Packets aufzunehmen.");

            lock (this)
            {
                if (_Finished)
                    return true;

                // Dann sind Daten fehlerhaft, also Neustart:
                if (_Completed && !_Finished)
                    Reset();

                int plPointer = Marshal.ReadByte(tsPacket, 0);
                // nur von Wichtigkeit, wenn PL Start, nicht wenn plOffset gesetzt ist.
                // Ansonsten kann das letzte Packet auch in PL Start haben, der Start aber später erst anfängt.
                // PDF ISO/IEC 13818-1 : 2000 (E) Seite 19 (reader: 37) Punkt 2.4.3.3 payload_unit_start_indicator.
                // wenn adaption field gesetzt gilt dies auch

                // start des Payloads ermitteln, wenn Adaption-Field vorhanden
                //int plOffset = 0;

                // wenn != 0, dann ist das ein Nested Call für weitere Sections pro packet, z.b. bei EMM.

                if (_PacketCtr == 0 && plOffset == 0)
                {
                    plOffset = 1 + plPointer;
                }

                if (plOffset > 180) // dann kann was nicht stimmen.
                {
                    Reset();
                    return false;
                }

                if (_PacketCtr == 0)
                {
                    // check erlaubte Tabelle:
                    byte tableId = Marshal.ReadByte(tsPacket, plOffset);
                    bool syntax = (Marshal.ReadByte(tsPacket, plOffset + 1) & 0x80) > 0;

                    bool isTable = false;

                    for (int i = 0; i < _AllowedTables.Length; i++)
                    {
                        if (_AllowedTables[i] == tableId)
                        {
                            isTable = true;
                            break;
                        }
                    }

                    if ((tableId < 0x80 && !syntax) || !isTable) // bei ECM/EMM wird die section Syntax teilweise deaktiviert.
                    {
                        Reset();
                        return false;
                    }
                }

                int len = (184) - plOffset;
                int remain;

                // länge aus Header ermitteln, damit packetize später stimmt, sofern erstes packet...
                if (_PacketCtr == 0)
                {
                    int u = Marshal.ReadByte(tsPacket, plOffset + 1);
                    int l = Marshal.ReadByte(tsPacket, plOffset + 2);

                    u &= 0x0f;
                    l |= u << 8;

                    remain = l + 3;
                }
                else
                {
                    remain = SectionSize - _Ptr;
                }

                if (remain < len)
                    len = remain;

                if (_Ptr + len > _SectionData.Length)
                    len -= _SectionData.Length - _Ptr; // einkürzen, damit in Buffer passt.

                if (len <= 0)
                {
                    Reset();
                    return false; // fehler
                }

                Marshal.Copy(tsPacket + plOffset, SectionData, _Ptr, len);
                _PacketCtr++;

                _Ptr += len;

                int size = SectionSize;
                _DoCrc = _SectionData[0] < 0x80; // Table ID, kein CRC auf ECM/EMM

                if (size > 1023) // max len = 1024 nach en300468
                {
                    Reset();
                    return false;
                }

                // wenn rest vom vorherige section zu lang ist, könnte teil der Section-Length fehlen
                // daher immer auf size prüfen.
                _Completed = size > 5 && (_Ptr >= size);

                if (_Completed)
                {
                    // dann CRC prüfen, wenn gefordert, ECMs sind z.B. ohne.:
                    _Finished = !_DoCrc || SectionCrc.Compare(_SectionData, size, 0x00000000);

                    if (_Finished && _SectionData[size] != 0xff) // prüfen ob weitere Sections vorhanden sind:
                    {
                        NextSection = new SectionBase(_Pid, _AllowedTables);
                        NextSection.AddPacket184(tsPacket, plOffset + size); // offsetstart + Section-Länge = start nächste Section
                    }
                }

                return _Finished;
            }
        }

        /// <summary>
        /// Fügt ein Paket aus einem Byte[] der Section hinzu
        /// </summary>
        /// <param name="tsPacket">MPEG Transport-Strom-Paket</param>
        /// <param name="plOffset">0 wenn erste Section in dem Packet. größer 0 wenn weitere packers vorhanden.</param>
        /// <returns>True wenn letzte benötigte Paket eingelesen und Prüfsumme korrekt berechnet wurde.</returns>
        private bool AddPacket(byte[] tsPacket, int plOffset)
        {
            lock (this)
            {
                if (_Finished)
                    return true;

                if (_Completed && !_Finished)
                    Reset();

                int err = tsPacket[1] & 0x80;
                int start = tsPacket[1] & 0x40;
                int prio = tsPacket[1] & 0x20;
                int pid = ((tsPacket[1] << 8) | tsPacket[2]) & 0x1fff;

                int scrambling = (tsPacket[3] & 0xc0) >> 6;
                int adaption = (tsPacket[3] & 0x30) >> 4;
                int contCtr = tsPacket[3] & 0x0f;

                int plPointer = tsPacket[4];

                if (pid != _Pid && _Pid != -1)
                    return false;

                if (err != 0)
                    return false;

                if ((_PacketCtr == 0 && start == 0) || adaption == 0)
                {
                    return false;
                }

                if (start == 0 && _PacketCtr != 0 &&
                    ((_ContCtr + 1) & 0x0f) != contCtr)
                {
                    Reset();
                    return false;
                }

                if (adaption == 2)
                {
                    return false;
                }

                if (plOffset == 0 && (adaption == 3 || start != 0))
                {
                    plOffset += 1 + plPointer;
                }

                int len = (188 - 4) - plOffset;
                int remain;

                if (_PacketCtr == 0)
                {
                    int u = tsPacket[4 + plOffset + 1];
                    int l = tsPacket[4 + plOffset + 2];

                    u &= 0x0f;
                    l |= u << 8;

                    remain = l + 3;
                }
                else
                {
                    remain = SectionSize - _Ptr;
                }

                if (remain < len)
                    len = remain;

                if (_Ptr + len > _SectionData.Length)
                    len -= _SectionData.Length - _Ptr;

                if (len <= 0)
                {
                    Reset();
                    return false;
                }

                Array.Copy(tsPacket, 4 + plOffset, SectionData, _Ptr, len);

                _ContCtr = contCtr;
                _PacketCtr++;

                _Ptr += len;

                int size = SectionSize;

                _Completed = size > 3 && (_Ptr >= size);

                if (_Completed)
                {
                    _Finished = !_DoCrc || SectionCrc.Compare(_SectionData, size, 0x00000000);

                    if (_Finished && start != 0 && _SectionData[size] != 0xff)
                    {
                        NextSection = new SectionBase(_Pid, _DoCrc);
                        NextSection.AddPacket(tsPacket, plOffset + size);
                    }
                }

                return _Finished;
            }
        }

        /// <summary>
        /// Fügt ein Paket ohne Header einem Byte[] der Section hinzu.
        /// </summary>
        /// <param name="tsPacket">MPEG Transport-Strom-Paket ohne Header</param>
        /// <param name="plOffset">0 wenn erste Section in dem Packet. größer 0 wenn weitere packers vorhanden.</param>
        /// <returns>True wenn letzte benötigte Paket eingelesen und Prüfsumme korrekt berechnet wurde.</returns>
        private bool AddPacket184(byte[] tsPacket, int plOffset)
        {
            if (_AllowedTables == null)
                throw new InvalidOperationException("Section wurde nicht korrekt initialisiert um 184 Byte Packets aufzunehmen.");

            lock (this)
            {
                if (_Finished)
                    return true;

                // Dann sind Daten fehlerhaft, also Neustart:
                if (_Completed && !_Finished)
                    Reset();

                int plPointer = tsPacket[0];

                if (_PacketCtr == 0 && plOffset == 0)
                {
                    plOffset = 1 + plPointer;
                }

                if (plOffset > 180) // dann kann was nicht stimmen.
                {
                    Reset();
                    return false;
                }

                if (_PacketCtr == 0)
                {
                    byte tableId = tsPacket[plOffset];
                    bool syntax = (tsPacket[plOffset + 1] & 0x80) > 0;

                    bool isTable = false;

                    for (int i = 0; i < _AllowedTables.Length; i++)
                    {
                        if (_AllowedTables[i] == tableId)
                        {
                            isTable = true;
                            break;
                        }
                    }

                    if ((tableId < 0x80 && !syntax) || !isTable)
                    {
                        Reset();
                        return false;
                    }
                }

                int len = (184) - plOffset;
                int remain;

                if (_PacketCtr == 0)
                {
                    int u = Marshal.ReadByte(tsPacket, plOffset + 1);
                    int l = Marshal.ReadByte(tsPacket, plOffset + 2);

                    u &= 0x0f;
                    l |= u << 8;

                    remain = l + 3;
                }
                else
                {
                    remain = SectionSize - _Ptr;
                }

                if (remain < len)
                    len = remain;

                if (_Ptr + len > _SectionData.Length)
                    len -= _SectionData.Length - _Ptr; // einkürzen, damit in Buffer passt.

                if (len <= 0)
                {
                    Reset();
                    return false; // fehler
                }

                Array.Copy(tsPacket, plOffset, SectionData, _Ptr, len);
                _PacketCtr++;

                _Ptr += len;

                int size = SectionSize;
                _DoCrc = _SectionData[0] < 0x80; // Table ID, kein CRC auf ECM/EMM

                if (size > 1023) // max len = 1024 nach en300468
                {
                    Reset();
                    return false;
                }

                // wenn rest vom vorherige section zu lang ist, könnte teil der Section-Length fehlen
                // daher immer auf size prüfen.
                _Completed = size > 5 && (_Ptr >= size);

                if (_Completed)
                {
                    // dann CRC prüfen, wenn gefordert, ECMs sind z.B. ohne.:
                    _Finished = !_DoCrc || SectionCrc.Compare(_SectionData, size, 0x00000000);

                    if (_Finished && _SectionData[size] != 0xff) // prüfen ob weitere Sections vorhanden sind:
                    {
                        NextSection = new SectionBase(_Pid, _AllowedTables);
                        NextSection.AddPacket184(tsPacket, plOffset + size); // offsetstart + Section-Länge = start nächste Section
                    }
                }

                return _Finished;
            }
        }

        /// <summary>
        /// Erstellt aus einer Section ein Byte-Array, welches TS-Packets enthält.
        /// </summary>
        /// <param name="contCounter"></param>
        /// <returns></returns>
        public byte[] Packetize(ref uint contCounter)
        {
            if (!_Finished)
                return null;

            int len = SectionSize;

            using (MemoryStream ms = new MemoryStream())
            {
                byte tmp;
                int i = 0;

                while (i < len)
                {
                    tmp = 0;

                    ms.WriteByte(0x47);

                    if (i == 0)
                        tmp |= 0x40; // start

                    tmp |= (byte)((_Pid >> 8) & 0x1f);
                    ms.WriteByte(tmp);

                    tmp = (byte)(_Pid & 0xff);
                    ms.WriteByte(tmp);

                    tmp = 0x10;
                    tmp |= (byte)((contCounter++) & 0x0f);
                    ms.WriteByte(tmp);

                    int pLen = 184; // nutzdatenlänge

                    if (i == 0) // erstes Packet.
                    {
                        ms.WriteByte(0);
                        pLen -= 1;
                    }

                    ms.Write(_SectionData, i, pLen);
                    i += pLen;
                }

                return ms.ToArray();
            }
        }
    }
}
