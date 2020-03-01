using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using dvbapiNet.Oscam;
using dvbapiNet.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace dvbapiNet.MdApi
{
    public static class Plugin
    {
        private const string cLogSection = "mdapi plg";
        private static DvbApiAdapter _DvbAdapter;
        private static int _DllId = -1;
        private static IntPtr _MdapiWindow;

        private static Filter.Context[] _Filters;

        private static bool _188ByteSupport;
        private static bool _KeepAliveSupport;
        private static bool _IsStopping;

        private static byte[] _DllName;

        static Plugin()
        {
            try
            {
                LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiInitStart);

                // Adapter anlegen, macht auch direkt Verbindung zum Server auf:
                _DvbAdapter = new DvbApiAdapter(Globals.PipeName, true);

                _188ByteSupport = false;
                _KeepAliveSupport = false;

                _Filters = new Filter.Context[64];

                byte[] tmp = Encoding.UTF8.GetBytes(Globals.Info);
                _DllName = new byte[tmp.Length + 1]; // für terminierung
                Array.Copy(tmp, 0, _DllName, 0, tmp.Length);

                Process p = Process.GetCurrentProcess();

                LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiHost, p.ProcessName);

                if (p != null)
                {
                    string pName = p.ProcessName.ToLowerInvariant();
                    // check auf progdvb, anders nicht möglich:
                    if (pName.Contains("progdvb"))
                    {
                        _188ByteSupport = true;
                    }
                    else if (pName.Contains("mdapi"))
                    {
                        ProcessModule m = p.MainModule;

                        if (m != null)
                        {
                            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(m.FileName);
                            Version v = new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
                            Version min = new Version(0, 9, 0, 1615);

                            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiHostVersion, v);

                            if (v >= min)
                            {
                                _188ByteSupport = true;
                                _KeepAliveSupport = true;
                            }
                            else
                            {
                                LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiUse184);
                            }
                        }
                    }
                    else
                    {
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.MdapiUnkownHost);
                    }
                }

                _DvbAdapter.AddPidRequested += AddPid;
                _DvbAdapter.DelPidRequested += DelPid;
                _DvbAdapter.GotControlWord += SetDcw; // es wird nicht selbst entschlüsselt.

                _IsStopping = false;

                LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiInitDone);
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.MdapiInitFailed, ex);
            }
        }

        private static void SetDcw(byte[] cw, Dvb.Crypto.DescramblerParity parity)
        {
            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiEvSetDcw);

            TDvbCommand cmd = new TDvbCommand();
            cmd.CommandLength = 0x0007; // nachfolgende anzahl an words.
            cmd.Command1 = (ushort)MdApiMessages.DvbSetDescrCmd;
            cmd.Command2 = 0x0005;
            cmd.Parity = (ushort)parity;
            cmd.Cw = new ushort[4];

            // von byte[] in ushort[] umwandeln:
            for (int i = 0; i < cmd.Cw.Length; i++)
            {
                int j = i << 1;
                int k = j + 1;

                cmd.Cw[i] = (ushort)(cw[k] | (cw[j] << 8));
            }

            IntPtr cmdPtr = Marshal.AllocHGlobal(Marshal.SizeOf(cmd));
            Marshal.StructureToPtr(cmd, cmdPtr, false);

            int res = (int)SendMessage(_MdapiWindow, (uint)WMessage.User, (UIntPtr)MdApiMessages.DvbCommand, cmdPtr);
            if (res == 0)
            {
                uint err = Plugin.GetLastError();
                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.MdapiEvSetDcwFailed, err);
            }

            Marshal.FreeHGlobal(cmdPtr);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="MDInstance">HINSTANCE</param>
        /// <param name="MDWnd">HWND</param>
        /// <param name="Log_Set">bool</param>
        /// <param name="DLL_ID">int</param>
        /// <param name="My_Hot_Key">char *</param>
        /// <param name="Api_Version">char *</param>
        /// <param name="keepMeRunning">int *</param>
        [DllExport(ExportName = "On_Start", CallingConvention = CallingConvention.Cdecl)]
        public static void OnStart(
            IntPtr MDInstance, IntPtr MDWnd, int Log_Set, int dllId,
            IntPtr My_Hot_Key, IntPtr Api_Version, IntPtr keepMeRunning
            )
        {
            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiEvOnStart);

            _DllId = dllId;
            _MdapiWindow = MDWnd;

            if (keepMeRunning != IntPtr.Zero && _KeepAliveSupport)
            {
                Marshal.WriteInt32(keepMeRunning, 1);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="MDInstance">HINSTANCE</param>
        /// <param name="MDWnd">HWND</param>
        /// <param name="Log_Set">bool</param>
        [DllExport(ExportName = "On_Exit", CallingConvention = CallingConvention.Cdecl)]
        public static void OnExit(IntPtr MDInstance, IntPtr MDWnd, int Log_Set)
        {
            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiUnload);
            try
            {
                _IsStopping = true;
                _DvbAdapter.Tune(-1, -1, -1, -1);
                _DvbAdapter.Dispose();
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.MdapiUnloadFailed, ex);
            }
            Globals.Dispose();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name">char *</param>
        [DllExport(ExportName = "On_Send_Dll_ID_Name", CallingConvention = CallingConvention.Cdecl)]
        public static void OnSendDllIdName(IntPtr name)
        {
            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiEvDllName);

            if (name != IntPtr.Zero)
            {
                Marshal.Copy(_DllName, 0, name, _DllName.Length);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="currentProgram">struct TProgramm82</param>
        [DllExport(ExportName = "On_Channel_Change", CallingConvention = CallingConvention.Cdecl)]
        public static void OnChannelChange(TProgramm82 currentProgram)
        {
            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiEvChannelChange);
            try
            {
                if (currentProgram.ServiceId <= 0)
                {
                    _DvbAdapter.Tune(-1, -1, -1, -1);
                }
                else
                {
                    LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiEvChannelInfo, Environment.NewLine, Statics.GetStringFromC(currentProgram.Name), Statics.GetStringFromC(currentProgram.Anbieter), Statics.GetStringFromC(currentProgram.Land));
                    _DvbAdapter.Tune(currentProgram.ServiceId, currentProgram.PmtPid, currentProgram.TransportStreamId, -1);
                }
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.MdapiEvChannelChangeFailed, ex);
            }
        }

        [DllExport(ExportName = "On_Filter_Close", CallingConvention = CallingConvention.Cdecl)]
        public static void OnFilterClose(ushort filterOffset)
        {
        }

        [DllExport(ExportName = "On_Rec_Play", CallingConvention = CallingConvention.Cdecl)]
        public static void OnRecPlay(Recplay mode)
        {
        }

        [DllExport(ExportName = "On_Hot_Key", CallingConvention = CallingConvention.Cdecl)]
        public static void OnHotKey()
        {
        }

        [DllExport(ExportName = "On_Menu_Select", CallingConvention = CallingConvention.Cdecl)]
        public static void OnMenuSelect(ulong dwMenuId)
        {
        }

        private static void AddPid(DvbApiAdapter sender, ushort pid)
        {
            if (_IsStopping)
                return;

            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiAddPid, pid);

            try
            {
                lock (_Filters)
                {
                    int free = -1;
                    int found = -1;
                    for (int i = 0; i < _Filters.Length; i++)
                    {
                        if (_Filters[i] == null && free < 0)
                        {
                            free = i;
                        }
                        else if (_Filters[i] != null && _Filters[i].FilterPid == pid && found < 0)
                        {
                            found = i;
                        }
                    }

                    if (found >= 0)
                    {
                        _Filters[found].Dispose();
                        _Filters[found] = new Filter.Context(_DvbAdapter, _DllName, _MdapiWindow, _188ByteSupport, _DllId, pid, (ushort)found);
                    }
                    else if (free >= 0)
                    {
                        _Filters[free] = new Filter.Context(_DvbAdapter, _DllName, _MdapiWindow, _188ByteSupport, _DllId, pid, (ushort)free);
                    }
                    else
                    {
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.MdapiAddPidMaxFilter, pid);
                    }
                }
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.MdapiAddPidFailed, ex);
            }
        }

        private static void DelPid(DvbApiAdapter sender, ushort pid)
        {
            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiDelPid, pid);

            lock (_Filters)
            {
                for (int i = 0; i < _Filters.Length; i++)
                {
                    if (_Filters[i] != null && _Filters[i].FilterPid == pid)
                    {
                        LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiStopFilter, i, pid);
                        _Filters[i].Dispose();
                        _Filters[i] = null;
                        return;
                    }
                }

                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.MdapiStopFilterFailed, pid);
            }
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint wMsg,
                        UIntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        internal static extern uint GetLastError();
    }
}
