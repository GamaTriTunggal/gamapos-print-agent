' Keadaan nyata untuk jendela Status (P-626): peta printer, cetakan terakhir,
' dan pembaruan. Cetakan terakhir hanya waktu + peran + hasil, sejak agen
' dinyalakan; isi nota tidak pernah disimpan (aturan mutlak 6).
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Newtonsoft.Json.Linq

Module StatusCapture
    Private ReadOnly OutcomeLock As New Object()
    Private _lastPrint As PrintOutcome = Nothing

    ' Dipanggil setelah POST /print dan /print/test berperan selesai.
    Friend Sub RecordPrint(role As String, ok As Boolean, errorCode As String)
        SyncLock OutcomeLock
            _lastPrint = New PrintOutcome With {
                .At = DateTime.Now, .Role = If(role, "").Trim().ToUpperInvariant(),
                .Ok = ok, .ErrorCode = If(errorCode, "")}
        End SyncLock
    End Sub

    ' Hasil POST /print dibaca dari balasan JSON yang sama dengan yang dikirim ke web;
    ' peran dari amplop. Galat baca tidak boleh mengganggu cetak.
    Friend Sub RecordPrintReply(body As String, reply As String)
        Try
            Dim role As String = ""
            Try
                Dim envelope As JObject = JObject.Parse(body)
                Dim token As JToken = envelope("printerRole")
                If token IsNot Nothing AndAlso token.Type = JTokenType.String Then role = CStr(token)
            Catch
            End Try
            Dim result As JObject = JObject.Parse(reply)
            Dim ok As Boolean = result("ok") IsNot Nothing AndAlso result("ok").Type = JTokenType.Boolean AndAlso CBool(result("ok"))
            Dim code As String = If(ok, "", If(result.Value(Of String)("error"), ""))
            RecordPrint(role, ok, code)
        Catch
        End Try
    End Sub

    Friend Function Capture() As StatusSnapshot
        Dim s As New StatusSnapshot With {
            .Now = DateTime.Now,
            .Version = AgentVersionString(),
            .DefaultPrinter = Printers.CurrentDefault(),
            .InstalledPrinters = Printers.Installed(),
            .ConfigExists = File.Exists(AppPaths.ConfigPath()),
            .Update = Updater.CurrentState(),
            .StagedVersion = Updater.StagedVersion(),
            .UpdateCheckedAt = Updater.LastCheckedAt(),
            .ListenAddress = ListenPrefix(),
            .DataDir = AppPaths.DataDir(),
            .LogDir = AppPaths.LogDir(),
            .DeviceId = DeviceIdString(),
            .OsArch = OsArchString(),
            .CatalogVersion = RecipeCatalog.Version().ToString(Globalization.CultureInfo.InvariantCulture),
            .CatalogSource = RecipeCatalog.Source()}
        Dim installed As New HashSet(Of String)(s.InstalledPrinters, StringComparer.OrdinalIgnoreCase)
        For Each role As String In StatusRoles
            Dim name As String = Printers.Resolve(role)
            s.Roles.Add(New RoleState With {.Role = role, .Printer = name, .Installed = name <> "" AndAlso installed.Contains(name)})
        Next
        SyncLock OutcomeLock
            s.LastPrint = _lastPrint
        End SyncLock
        Return s
    End Function
End Module
