' Auto-update via Velopack (feed = GitHub Releases publik). Cek berkala dari TrayContext.
'
' CATATAN VERIFIKASI (build di VM): nama method/properti Velopack bisa berbeda antar versi.
' Jalankan `dotnet add package Velopack` untuk pin versi terbaru, lalu sesuaikan API di bawah
' dengan IntelliSense bila ada error (UpdateManager / GithubSource / CheckForUpdatesAsync /
' DownloadUpdatesAsync / WaitExitThenApplyUpdates / UpdateInfo.TargetFullRelease.Version).

Option Strict On
Option Explicit On

Imports System
Imports System.Threading.Tasks
Imports Velopack
Imports Velopack.Sources

Module Updater

    Private Const RepoUrl As String = "https://github.com/GamaTriTunggal/gamapos-print-agent"

    ' Update yang sudah diunduh & di-stage (menunggu diterapkan). PR-12 (K-16 #7): diterapkan sendiri
    ' saat agent MENGANGGUR ≥ 10 menit (tak ada cetak/pemasangan), bukan hanya saat PC restart.
    Private _staged As UpdateInfo = Nothing
    Private _stagedMgr As UpdateManager = Nothing

    Public Function HasStagedUpdate() As Boolean
        Return _staged IsNot Nothing
    End Function

    ' Terapkan update yang sudah di-stage lalu restart agent. Return True bila proses restart dimulai.
    Public Function ApplyStagedAndRestart() As Boolean
        Try
            If _staged Is Nothing OrElse _stagedMgr Is Nothing Then Return False
            Console.WriteLine("Update: menerapkan " & _staged.TargetFullRelease.Version.ToString() & " saat menganggur — restart agent")
            _stagedMgr.ApplyUpdatesAndRestart(_staged)
            Return True
        Catch ex As Exception
            Console.WriteLine("Update apply error: " & ex.Message)
            Return False
        End Try
    End Function

    ' Cek GitHub Releases. Ada versi baru → download + STAGE (terapkan saat agent KELUAR/restart →
    ' tidak mengganggu cetak yang sedang berjalan). Return pesan untuk balloon tray, atau Nothing.
    Public Async Function CheckAndStageAsync() As Task(Of String)
        Try
            Dim mgr As New UpdateManager(New GithubSource(RepoUrl, Nothing, False))

            ' Jalan dari bin/dev (bukan instalasi Velopack) → tak ada yang bisa di-update. Lewati.
            If Not mgr.IsInstalled Then Return Nothing

            Dim info As UpdateInfo = Await mgr.CheckForUpdatesAsync()
            If info Is Nothing Then Return Nothing   ' sudah versi terbaru

            Await mgr.DownloadUpdatesAsync(info)
            mgr.WaitExitThenApplyUpdates(info)       ' pasang saat keluar/restart berikutnya
            _staged = info
            _stagedMgr = mgr

            Return "Update " & info.TargetFullRelease.Version.ToString() & " siap — dipasang saat agent menganggur."
        Catch ex As Exception
            Console.WriteLine("Update check error: " & ex.Message)
            Return Nothing
        End Try
    End Function

End Module
