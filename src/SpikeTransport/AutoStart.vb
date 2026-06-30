' Auto-start saat login user via HKCU\...\Run. Velopack TIDAK mendaftarkan auto-start sendiri
' (hanya bikin shortcut + jalankan sekali setelah install), jadi agent mendaftarkan dirinya.
'
' Dipanggil tiap startup → nilai path selalu segar. Velopack menjalankan exe dari folder
' ...\current\ yang STABIL lintas-update, jadi Application.ExecutablePath tetap valid setelah update.

Option Strict On
Option Explicit On

Imports System
Imports System.Windows.Forms
Imports Microsoft.Win32

Module AutoStart

    Private Const RunSubKey As String = "Software\Microsoft\Windows\CurrentVersion\Run"
    Private Const ValueName As String = "GamaPrintAgent"

    ' Daftarkan agent agar jalan otomatis saat user login.
    Public Sub Enable()
        Try
            Dim exe As String = """" & Application.ExecutablePath & """"
            Using k = Registry.CurrentUser.OpenSubKey(RunSubKey, writable:=True)
                If k IsNot Nothing Then k.SetValue(ValueName, exe)
            End Using
        Catch
            ' gagal daftar auto-start tak boleh menggagalkan agent
        End Try
    End Sub

    ' Hapus pendaftaran (mis. saat uninstall).
    Public Sub Disable()
        Try
            Using k = Registry.CurrentUser.OpenSubKey(RunSubKey, writable:=True)
                If k IsNot Nothing Then k.DeleteValue(ValueName, throwOnMissingValue:=False)
            End Using
        Catch
        End Try
    End Sub

End Module
