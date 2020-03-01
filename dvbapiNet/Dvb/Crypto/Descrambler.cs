using dvbapiNet.Dvb.Crypto.Algo;
using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using System;

namespace dvbapiNet.Dvb.Crypto
{
    /// <summary>
    /// Descrambler für Verschlüsselte DVB-Packets. Stellt vom Grundprinzip einen Wrapper dar,
    /// um einfacher zwischen den verschiedenen Algorithmen wechseln zu können.
    /// </summary>
    public class Descrambler
    {
        private const string cLogSection = "descrambler";

        private IDescramblerAlgo _Algo;
        private bool _Disposed;

        /// <summary>
        /// Erstellt neuen Descrambler ohne initialen Algorithmus.
        /// </summary>
        public Descrambler()
        {
            _Disposed = false;
        }

        /// <summary>
        /// Initialisiert den Descrambler
        /// </summary>
        private void Initialize()
        {
            if (_Algo != null)
                return;

            try
            {
                _Algo = new DvbCsa(); // initial DVBCSA.
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.DescrCsaFailed, ex);

                _Algo = null; // im Fehlerfall. Fehlt ffdecsa.dll?
            }
        }

        /// <summary>
        /// Globaler Index des Descramblers, vergeben von der dvbapi
        /// Dient nur zur Identifizierung, hat intern keine weitere Bedeutung.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Fügt das angegebene Packet dem zu grunde liegendem Algorithmus zur Stapelverarbeitung hinzu.
        /// Dieser aufruf löst unter Umständen eine Verarbeitung aus, wenn die maximale Stapelgröße erreicht ist.
        /// </summary>
        /// <param name="tsPacket"></param>
        public void AddToBatch(IntPtr tsPacket)
        {
            if (_Disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (_Algo == null)
                return;

            _Algo.AddToBatch(tsPacket);
        }

        /// <summary>
        /// Verarbeitet die hinzugefügten Packets.
        /// </summary>
        public void DescrambleBatch()
        {
            if (_Disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (_Algo == null)
                return;

            _Algo.DescrambleBatch();
        }

        /// <summary>
        /// Entschlüsselt ein einzelnes Packet.
        /// </summary>
        /// <param name="tsPacket"></param>
        public void DescrambleSingle(IntPtr tsPacket)
        {
            if (_Disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (_Algo == null)
                return;

            _Algo.DescrambleSingle(tsPacket);
        }

        /// <summary>
        /// Gibt alle verwendeten Ressourcen wieder frei.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (_Algo != null)
            {
                lock (this)
                    _Algo.Dispose();
            }

            _Disposed = true;
        }

        /// <summary>
        /// Setzt Descrambler-Daten, z.B. Key oder Initialisierungsvektor
        /// </summary>
        /// <param name="parity">Parität für die die Daten gelten</param>
        /// <param name="dType">Angabe um welche Art von Daten es sich handelt</param>
        /// <param name="data">Descrambler-Daten</param>
        public void SetDescramblerData(DescramblerParity parity, DescramblerDataType dType, byte[] data)
        {
            if (_Disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (_Algo == null) // Dann oscam ohne ext-CW Support, default CSA initialisieren:
                Initialize();

            lock (this)
                _Algo.SetDescramblerData(parity, dType, data);

            if (dType == DescramblerDataType.Key)
                LogProvider.WriteCWLog(DebugLevel.ControlWord, cLogSection, parity, data);
        }

        /// <summary>
        /// Setzt den Descrambler-Modus
        /// </summary>
        /// <param name="algo">Zu verwendender Algorithmus</param>
        /// <param name="mode">Betriebsmodus des Algorithmus, wird für DVB-CSA ignoriert</param>
        public void SetDescramblerMode(DescramblerAlgo algo, DescramblerMode mode)
        {
            if (_Disposed)
                throw new ObjectDisposedException(GetType().Name);

            Type t = null;

            lock (this)
            {
                if (_Algo != null)
                    t = _Algo.GetType();

                try
                {
                    switch (algo)
                    {
                        case DescramblerAlgo.DvbCsa:
                            if (t != typeof(DvbCsa))
                            {
                                if (_Algo != null)
                                    _Algo.Dispose();

                                _Algo = new DvbCsa();
                            }
                            break;

                        case DescramblerAlgo.Des:
                            if (t != typeof(Des))
                            {
                                if (_Algo != null)
                                    _Algo.Dispose();

                                _Algo = new Des();
                            }
                            break;

                        case DescramblerAlgo.Aes128:
                            if (t != typeof(Aes128))
                            {
                                if (_Algo != null)
                                    _Algo.Dispose();

                                _Algo = new Aes128();
                            }
                            break;
                    }

                    _Algo.SetDescramblerMode(mode);
                }
                catch (Exception ex)
                {
                    LogProvider.Exception(cLogSection, Message.DescrAlgoSwitchFailed, ex);
                }
            }
        }
    }
}
