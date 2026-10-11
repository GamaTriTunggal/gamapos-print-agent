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

    ' Keadaan pemeriksaan terakhir untuk jendela Status (P-626). Ditulis/dibaca di thread UI.
    Private _state As UpdateState = UpdateState.NotChecked
    Private _checkedAt As DateTime? = Nothing

    Friend Function CurrentState() As UpdateState
        If _staged IsNot Nothing Then Return UpdateState.Staged
        Return _state
    End Function

    Friend Function StagedVersion() As String
        If _staged Is Nothing Then Return ""
        Return _staged.TargetFullRelease.Version.ToString()
    End Function

    Friend Function LastCheckedAt() As DateTime?
        Return _checkedAt
    End Function

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
            If Not mgr.IsInstalled Then
                _state = UpdateState.NotInstalled
                Return Nothing
            End If

            Dim info As UpdateInfo = Await mgr.CheckForUpdatesAsync()
            _checkedAt = DateTime.Now
            If info Is Nothing Then   ' sudah versi terbaru
                _state = UpdateState.UpToDate
                Return Nothing
            End If

            Await mgr.DownloadUpdatesAsync(info)
            mgr.WaitExitThenApplyUpdates(info)       ' pasang saat keluar/restart berikutnya
            _staged = info
            _stagedMgr = mgr

            Return "Update " & info.TargetFullRelease.Version.ToString() & " siap — dipasang saat agent menganggur."
        Catch ex As Exception
            Console.WriteLine("Update check error: " & ex.Message)
            _state = UpdateState.CheckFailed
            Return Nothing
        End Try
    End Function

End Module
