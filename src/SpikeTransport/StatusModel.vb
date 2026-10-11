' Isi jendela Status agen (P-626, putusan pemilik 11 Okt 2026): bahasa toko,
' bukan nama folder/berkas. Rincian teknis hanya ikut "Salin info untuk
' bantuan". Modul ini murni (tanpa WinForms/printer) supaya teruji di CI
' Linux; pengambilan keadaan nyata ada di StatusCapture.vb.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text

Friend Enum UpdateState
    NotChecked
    NotInstalled
    UpToDate
    Staged
    CheckFailed
End Enum

Friend NotInheritable Class PrintOutcome
    Friend Property At As DateTime
    Friend Property Role As String
    Friend Property Ok As Boolean
    Friend Property ErrorCode As String
End Class

Friend NotInheritable Class RoleState
    Friend Property Role As String
    Friend Property Printer As String
    Friend Property Installed As Boolean
End Class

Friend NotInheritable Class StatusSnapshot
    Friend Property Now As DateTime
    Friend Property Version As String
    Friend Property Roles As New List(Of RoleState)()
    Friend Property DefaultPrinter As String = ""
    Friend Property InstalledPrinters As New List(Of String)()
    Friend Property ConfigExists As Boolean
    Friend Property LastPrint As PrintOutcome
    Friend Property Update As UpdateState
    Friend Property StagedVersion As String = ""
    Friend Property UpdateCheckedAt As DateTime?
    Friend Property ListenAddress As String = ""
    Friend Property DataDir As String = ""
    Friend Property LogDir As String = ""
    Friend Property DeviceId As String = ""
    Friend Property OsArch As String = ""
    Friend Property CatalogVersion As String = ""
    Friend Property CatalogSource As String = ""
End Class

Friend NotInheritable Class StatusRow
    Friend Property Caption As String
    Friend Property Value As String
    Friend Property Note As String = ""
    Friend Property Warn As Boolean
End Class

Friend NotInheritable Class StatusView
    Friend Property Subtitle As String
    Friend Property BandWarn As Boolean
    Friend Property BandText As String
    Friend Property Printers As New List(Of StatusRow)()
    Friend Property Activity As New List(Of StatusRow)()
End Class

Module StatusModel
    ' Urutan dan nama peran mengikuti halaman Pengaturan → Printer di web.
    Friend ReadOnly StatusRoles As String() = {"CASHIER", "QRLABEL", "DELIVERY", "REPORT"}

    Private ReadOnly MonthNames As String() = {"Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des"}

    Friend Function RoleCaption(role As String) As String
        Select Case If(role, "").ToUpperInvariant()
            Case "CASHIER" : Return "Struk kasir"
            Case "QRLABEL" : Return "Label QR"
            Case "DELIVERY" : Return "Surat jalan"
            Case "REPORT" : Return "Laporan"
            Case Else : Return "Cetakan"
        End Select
    End Function

    ' Gagal karena printer (bukan karena isi job): sarannya memeriksa printer.
    Friend Function IsPrinterFailure(code As String) As Boolean
        Select Case If(code, "")
            Case "PRINT_FAILED", "PRINTER_NOT_FOUND", "PRINTER_SET_FAILED" : Return True
            Case Else : Return False
        End Select
    End Function

    Friend Function BuildStatusView(s As StatusSnapshot) As StatusView
        Dim view As New StatusView With {.Subtitle = "Versi " & s.Version & " · terpasang di PC ini"}
        Dim missingRole As String = Nothing
        For Each role As String In StatusRoles
            Dim state As RoleState = FindRole(s, role)
            Dim row As New StatusRow With {.Caption = RoleCaption(role)}
            If state IsNot Nothing AndAlso state.Printer <> "" Then
                row.Value = state.Printer
                If Not state.Installed Then
                    row.Note = "Tidak ditemukan di PC ini"
                    row.Warn = True
                    If missingRole Is Nothing Then missingRole = role
                End If
            ElseIf role = "CASHIER" Then
                ' Struk kasir tanpa pilihan = printer utama Windows; tampilkan namanya.
                If s.DefaultPrinter = "" Then
                    row.Value = "Belum ada printer"
                    row.Note = "Pasang printer di Windows"
                    row.Warn = True
                Else
                    row.Value = s.DefaultPrinter
                    row.Note = "Printer utama Windows"
                End If
            Else
                row.Value = "Belum dipilih"
                row.Note = "Memakai printer utama Windows"
            End If
            view.Printers.Add(row)
        Next

        Dim last As New StatusRow With {.Caption = "Cetakan terakhir"}
        If s.LastPrint Is Nothing Then
            last.Value = "Belum ada sejak agen dinyalakan"
        Else
            Dim result As String = If(s.LastPrint.Ok, "berhasil", If(IsPrinterFailure(s.LastPrint.ErrorCode), "gagal", "ditolak"))
            last.Value = WhenText(s.LastPrint.At, s.Now) & " · " & RoleCaption(s.LastPrint.Role) & " · " & result
            last.Warn = Not s.LastPrint.Ok
        End If
        view.Activity.Add(last)
        view.Activity.Add(New StatusRow With {.Caption = "Pembaruan", .Value = UpdateText(s)})

        ' Satu pita: masalah yang paling perlu ditindak lebih dulu.
        If s.LastPrint IsNot Nothing AndAlso Not s.LastPrint.Ok Then
            view.BandWarn = True
            view.BandText = If(IsPrinterFailure(s.LastPrint.ErrorCode),
                "Cetakan terakhir gagal — periksa printer menyala dan kertas terpasang.",
                "Cetakan terakhir ditolak agen — salin info untuk bantuan.")
        ElseIf missingRole IsNot Nothing Then
            view.BandWarn = True
            view.BandText = "Printer " & RoleCaption(missingRole).ToLowerInvariant() &
                " tidak ditemukan — pilih ulang di GamaPOS: Pengaturan → Printer."
        ElseIf s.DefaultPrinter = "" AndAlso s.InstalledPrinters.Count = 0 Then
            view.BandWarn = True
            view.BandText = "Belum ada printer terpasang di Windows."
        Else
            view.BandText = "Aktif — siap menerima cetakan dari GamaPOS"
        End If
        Return view
    End Function

    Friend Function UpdateText(s As StatusSnapshot) As String
        Select Case s.Update
            Case UpdateState.Staged
                Return "Versi " & s.StagedVersion & " siap; dipasang otomatis saat agen tidak dipakai 10 menit."
            Case UpdateState.UpToDate
                Return "Sudah versi terbaru"
            Case UpdateState.NotInstalled
                Return "Tidak tersedia (bukan instalasi resmi)"
            Case UpdateState.CheckFailed
                Return "Belum dapat diperiksa; dicoba lagi otomatis"
            Case Else
                Return "Belum diperiksa"
        End Select
    End Function

    Friend Function WhenText(at As DateTime, now As DateTime) As String
        Dim clock As String = at.ToString("HH:mm", CultureInfo.InvariantCulture)
        If at.Date = now.Date Then Return "Hari ini " & clock
        If at.Date = now.Date.AddDays(-1) Then Return "Kemarin " & clock
        Return at.Day.ToString(CultureInfo.InvariantCulture) & " " & MonthNames(at.Month - 1) & " " & clock
    End Function

    ' Teks untuk tim bantuan. Tidak memuat isi nota (aturan mutlak 6).
    Friend Function BuildSupportText(s As StatusSnapshot) As String
        Dim stamp As String = "yyyy-MM-dd HH:mm:ss"
        Dim sb As New StringBuilder()
        sb.AppendLine("GamaPOS Print Agent " & s.Version)
        sb.AppendLine("Waktu: " & s.Now.ToString(stamp, CultureInfo.InvariantCulture))
        sb.AppendLine("Alamat: " & s.ListenAddress)
        sb.AppendLine("Folder data: " & s.DataDir)
        sb.AppendLine("Folder log: " & s.LogDir)
        sb.AppendLine("ID perangkat: " & s.DeviceId)
        sb.AppendLine("Arsitektur: " & s.OsArch)
        sb.AppendLine("Katalog resep: v" & s.CatalogVersion & " (" & s.CatalogSource & ")")
        sb.AppendLine("printers.json: " & If(s.ConfigExists, "ada", "tidak ada"))
        For Each role As String In StatusRoles
            Dim state As RoleState = FindRole(s, role)
            Dim value As String = "(tidak dipilih)"
            If state IsNot Nothing AndAlso state.Printer <> "" Then
                value = state.Printer & If(state.Installed, "", " [TIDAK TERPASANG]")
            End If
            sb.AppendLine("Peran " & role & ": " & value)
        Next
        sb.AppendLine("Printer utama Windows: " & If(s.DefaultPrinter = "", "(tidak ada)", s.DefaultPrinter))
        sb.AppendLine("Printer terpasang: " & If(s.InstalledPrinters.Count = 0, "(tidak ada)", String.Join("; ", s.InstalledPrinters)))
        If s.LastPrint Is Nothing Then
            sb.AppendLine("Cetakan terakhir: belum ada sejak agen dinyalakan")
        Else
            sb.AppendLine("Cetakan terakhir: " & s.LastPrint.At.ToString(stamp, CultureInfo.InvariantCulture) & " " &
                          If(s.LastPrint.Role = "", "-", s.LastPrint.Role) & " " &
                          If(s.LastPrint.Ok, "ok", "gagal " & If(s.LastPrint.ErrorCode = "", "-", s.LastPrint.ErrorCode)))
        End If
        Dim checkedAt As String = If(s.UpdateCheckedAt.HasValue,
            " (diperiksa " & s.UpdateCheckedAt.Value.ToString(stamp, CultureInfo.InvariantCulture) & ")", "")
        sb.Append("Pembaruan: " & UpdateText(s) & checkedAt)
        Return sb.ToString()
    End Function

    Private Function FindRole(s As StatusSnapshot, role As String) As RoleState
        For Each state As RoleState In s.Roles
            If String.Equals(state.Role, role, StringComparison.OrdinalIgnoreCase) Then Return state
        Next
        Return Nothing
    End Function
End Module
