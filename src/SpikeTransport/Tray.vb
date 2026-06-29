' Konteks aplikasi tray: ikon di system tray + menu klik-kanan. Server HTTP berjalan di thread
' background (Program.RunAcceptLoop) sementara message loop WinForms menjaga app hidup + ikon tray.

Option Strict On
Option Explicit On

Imports System
Imports System.Diagnostics
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms

Friend Class TrayContext
    Inherits ApplicationContext

    Private ReadOnly _tray As NotifyIcon
    Private _serverThread As Thread

    Public Sub New()
        ' Server HTTP sudah dimulai di Program.Main (StartServer sukses). Loop accept di thread BG.
        _serverThread = New Thread(AddressOf Program.RunAcceptLoop) With {.IsBackground = True}
        _serverThread.Start()

        ' Menu tray.
        Dim menu As New ContextMenuStrip()
        menu.Items.Add("Status", Nothing, AddressOf OnStatus)
        menu.Items.Add("Buka folder data", Nothing, AddressOf OnOpenData)
        menu.Items.Add("Lihat log", Nothing, AddressOf OnViewLog)
        menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("Keluar", Nothing, AddressOf OnExit)

        _tray = New NotifyIcon() With {
            .Icon = LoadAppIcon(),
            .Text = "Gama Print Agent",
            .Visible = True,
            .ContextMenuStrip = menu
        }
        Try
            _tray.ShowBalloonTip(3000, "Gama Print Agent", "Aktif — siap menerima cetak.", ToolTipIcon.Info)
        Catch
        End Try
    End Sub

    ' Ikon = ikon exe (di-set via <ApplicationIcon> = gamapos.ico). Fallback ke ikon sistem.
    Private Function LoadAppIcon() As Icon
        Try
            Return Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
            Return SystemIcons.Application
        End Try
    End Function

    Private Sub OnStatus(sender As Object, e As EventArgs)
        MessageBox.Show(Program.StatusSummary(), "Status — Gama Print Agent",
                        MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub OnOpenData(sender As Object, e As EventArgs)
        OpenFolder(AppPaths.DataDir())
    End Sub

    Private Sub OnViewLog(sender As Object, e As EventArgs)
        OpenFolder(AppPaths.LogDir())
    End Sub

    Private Sub OpenFolder(path As String)
        Try
            Process.Start("explorer.exe", path)
        Catch
        End Try
    End Sub

    Private Sub OnExit(sender As Object, e As EventArgs)
        Program.StopServer()
        If _tray IsNot Nothing Then
            _tray.Visible = False
            _tray.Dispose()
        End If
        ExitThread()
    End Sub

End Class
