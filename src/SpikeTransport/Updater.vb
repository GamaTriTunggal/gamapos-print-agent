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

            Return "Update " & info.TargetFullRelease.Version.ToString() & " siap — dipasang saat restart."
        Catch ex As Exception
            Console.WriteLine("Update check error: " & ex.Message)
            Return Nothing
        End Try
    End Function

End Module
