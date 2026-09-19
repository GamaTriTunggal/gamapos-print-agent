' Gama Print Agent — Pemeriksa kehadiran perangkat USB & keadaan antrean printer (WMI).
'
' Residu PR-12 papan printer gamapos-go-2 (K-16 #4; register P-592). Dipakai HANYA oleh alur
' "Pasang Otomatis" (PrinterSetup) untuk state `waiting_printer`:
'   1. UsbDevicePresent(usbIds) — apakah perangkat ber-hardware-ID resep (mis. USB\VID_04B8&PID_0202)
'      sedang tercolok & menyala. Sumber: Win32_PnPEntity; Windows 8+ juga mendaftar perangkat yang
'      PERNAH tercolok (Present=False) → properti Present dibaca bila ada, cadangan Status = "OK".
'   2. QueueState(printerName) — antrean Windows: online | offline | error | missing | unknown.
'      Sumber: Win32_Printer.WorkOffline + DetectedErrorState (4 kertas habis, 7 penutup terbuka,
'      8 macet, 9 offline).
' TIDAK dipakai di jalur cetak (K-1c ditahan): mencetak tetap seperti v1.0.2.
' Kegagalan WMI apa pun = "tidak tahu" (Nothing / "unknown") — pemanggil memperlakukannya sebagai
' "lanjut", supaya PC yang WMI-nya rusak tidak terkunci di layar menunggu.

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Management

Module DeviceProbe

    ' True/False bila terjawab; Nothing bila daftar id kosong atau seluruh kueri WMI gagal.
    Public Function UsbDevicePresent(usbIds As IEnumerable(Of String)) As Boolean?
        If usbIds Is Nothing Then Return Nothing
        Dim anyId As Boolean = False
        Dim answered As Boolean = False
        For Each id As String In usbIds
            If String.IsNullOrWhiteSpace(id) Then Continue For
            anyId = True
            Try
                ' WQL: backslash ditulis ganda; % = wildcard (instance id di belakang PID); tanda kutip dibuang.
                Dim pattern As String = id.Trim().Replace("\", "\\").Replace("'", "") & "%"
                Using s As New ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '" & pattern & "'")
                    For Each mo As ManagementObject In s.Get()
                        answered = True
                        If IsPresent(mo) Then Return True
                    Next
                End Using
                answered = True   ' kueri berjalan; hasil kosong = perangkat tidak hadir
            Catch ex As Exception
                Console.WriteLine("   WMI Win32_PnPEntity gagal (" & id & "): " & ex.Message)
            End Try
        Next
        If Not anyId OrElse Not answered Then Return Nothing
        Return False
    End Function

    Private Function IsPresent(mo As ManagementObject) As Boolean
        Try
            For Each pd As PropertyData In mo.Properties
                If String.Equals(pd.Name, "Present", StringComparison.OrdinalIgnoreCase) Then
                    If TypeOf pd.Value Is Boolean Then Return CBool(pd.Value)
                    Exit For
                End If
            Next
            Dim st As Object = mo("Status")
            Return st IsNot Nothing AndAlso String.Equals(st.ToString(), "OK", StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    ' online | offline | error | missing | unknown
    Public Function QueueState(printerName As String) As String
        If String.IsNullOrWhiteSpace(printerName) Then Return "unknown"
        Try
            Dim nm As String = printerName.Replace("\", "\\").Replace("'", "\'")
            Using s As New ManagementObjectSearcher("SELECT WorkOffline, DetectedErrorState FROM Win32_Printer WHERE Name = '" & nm & "'")
                For Each mo As ManagementObject In s.Get()
                    Dim offline As Boolean = False
                    Try
                        offline = CBool(mo("WorkOffline"))
                    Catch
                    End Try
                    Dim errState As Integer = 0
                    Try
                        errState = Convert.ToInt32(mo("DetectedErrorState"))
                    Catch
                    End Try
                    If offline OrElse errState = 9 Then Return "offline"
                    If errState = 4 OrElse errState = 7 OrElse errState = 8 Then Return "error"
                    Return "online"
                Next
            End Using
            Return "missing"
        Catch ex As Exception
            Console.WriteLine("   WMI Win32_Printer gagal: " & ex.Message)
            Return "unknown"
        End Try
    End Function

End Module
