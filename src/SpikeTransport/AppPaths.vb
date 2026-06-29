' Lokasi data STABIL per-user + logging.
'
' PENTING (Velopack): exe agent berjalan dari folder versioned (mis. ...\current\app-1.0.0\) yang
' DIGANTI setiap auto-update. Maka printers.json / jobs / logs / penanda restore TIDAK boleh di
' samping exe (akan hilang tiap update) — semuanya di %LOCALAPPDATA%\GamaPrintAgent\ yang bertahan.

Option Strict On
Option Explicit On

Imports System
Imports System.IO

Module AppPaths

    Private _dataDir As String = Nothing

    ' %LOCALAPPDATA%\GamaPrintAgent — folder data stabil (config/jobs/logs), bertahan lintas update.
    Public Function DataDir() As String
        If _dataDir IsNot Nothing Then Return _dataDir
        Dim d As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamaPrintAgent")
        Try
            Directory.CreateDirectory(d)
        Catch
        End Try
        _dataDir = d
        Return _dataDir
    End Function

    Public Function ConfigPath() As String
        Return Path.Combine(DataDir(), "printers.json")
    End Function

    Public Function RestoreMarkerPath() As String
        Return Path.Combine(DataDir(), "default-printer-restore.txt")
    End Function

    Public Function DebugFlagPath() As String
        Return Path.Combine(DataDir(), "debug.flag")
    End Function

    Public Function JobsDir() As String
        Dim d As String = Path.Combine(DataDir(), "jobs")
        Try
            Directory.CreateDirectory(d)
        Catch
        End Try
        Return d
    End Function

    Public Function LogDir() As String
        Dim d As String = Path.Combine(DataDir(), "logs")
        Try
            Directory.CreateDirectory(d)
        Catch
        End Try
        Return d
    End Function

    ' Arahkan Console.Out (semua Console.WriteLine yang sudah ada) ke file log harian di DataDir/logs.
    ' Dipanggil sekali di Main — agent tray (WinExe) tak punya jendela console, jadi log ke file.
    Public Sub SetupLogging()
        Try
            Dim path As String = Path.Combine(LogDir(), "agent-" & DateTime.Now.ToString("yyyyMMdd") & ".log")
            Dim sw As New StreamWriter(path, append:=True) With {.AutoFlush = True}
            Console.SetOut(sw)
            Console.WriteLine(Environment.NewLine & "=== Gama Print Agent start " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & " ===")
            PruneLogs(14)
        Catch
            ' gagal setup log tak boleh menggagalkan agent
        End Try
    End Sub

    Private Sub PruneLogs(keepDays As Integer)
        Try
            Dim cutoff As DateTime = DateTime.Now.AddDays(-keepDays)
            For Each f As FileInfo In New DirectoryInfo(LogDir()).GetFiles("agent-*.log")
                If f.LastWriteTime < cutoff Then
                    Try
                        f.Delete()
                    Catch
                    End Try
                End If
            Next
        Catch
        End Try
    End Sub

End Module
