' P-626: isi jendela Status agen dalam bahasa toko; rincian teknis hanya di
' teks bantuan. Murni (tanpa WinForms) sehingga jalan di CI Linux/Windows.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Linq

Module StatusModelTests
    Friend Sub Run()
        Dim now As New DateTime(2026, 10, 11, 20, 15, 0)
        Dim fresh As StatusSnapshot = Snapshot(now)

        ' Mockup: belum ada printers.json; struk kasir memakai printer utama Windows.
        Dim view As StatusView = BuildStatusView(fresh)
        Expect("subjudul", view.Subtitle, "Versi 1.4.0 · terpasang di PC ini")
        Expect("pita aktif", view.BandText, "Aktif — siap menerima cetakan dari GamaPOS")
        If view.BandWarn Then Throw New InvalidOperationException("Pita aktif berwarna peringatan.")
        Expect("baris printer", String.Join(" | ", view.Printers.Select(Function(r) r.Caption & "=" & r.Value & "/" & r.Note)),
               "Struk kasir=EPSON TM-U220 Receipt/Printer utama Windows | Label QR=Belum dipilih/Memakai printer utama Windows | " &
               "Surat jalan=Belum dipilih/Memakai printer utama Windows | Laporan=Belum dipilih/Memakai printer utama Windows")
        Expect("cetakan terakhir kosong", view.Activity(0).Value, "Belum ada sejak agen dinyalakan")
        Expect("pembaruan", view.Activity(1).Value, "Sudah versi terbaru")

        ' Tidak ada istilah teknis di jendela (yang dikeluhkan pemilik 10 Okt).
        Dim visible As String = view.Subtitle & view.BandText &
            String.Join("", view.Printers.Concat(view.Activity).Select(Function(r) r.Caption & r.Value & r.Note))
        For Each word In {"printers.json", "localhost", "9111", "AppData", "CASHIER", "QRLABEL", "DELIVERY", "REPORT", "(default)", "Config"}
            If visible.Contains(word) Then Throw New InvalidOperationException("Jendela Status menampilkan istilah teknis: " & word)
        Next

        ' Rincian teknis tetap lengkap di teks bantuan, tanpa isi nota.
        fresh.LastPrint = New PrintOutcome With {.At = now.AddMinutes(-1), .Role = "CASHIER", .Ok = False, .ErrorCode = "PRINT_FAILED"}
        Dim support As String = BuildSupportText(fresh)
        For Each fact In {"GamaPOS Print Agent 1.4.0", "Alamat: http://localhost:9111/", "Folder data: C:\Data",
                          "printers.json: tidak ada", "Peran CASHIER: (tidak dipilih)", "Printer utama Windows: EPSON TM-U220 Receipt",
                          "Printer terpasang: EPSON TM-U220 Receipt; Microsoft Print to PDF", "Cetakan terakhir: 2026-10-11 20:14:00 CASHIER gagal PRINT_FAILED",
                          "ID perangkat: 00000000-0000-0000-0000-000000000001", "Katalog resep: v0 (seed)"}
            If Not support.Contains(fact) Then Throw New InvalidOperationException("Teks bantuan tanpa: " & fact & vbLf & support)
        Next

        ' Gagal karena printer → saran memeriksa printer; ditolak karena isi → salin info.
        view = BuildStatusView(fresh)
        Expect("pita gagal", view.BandText, "Cetakan terakhir gagal — periksa printer menyala dan kertas terpasang.")
        Expect("baris gagal", view.Activity(0).Value, "Hari ini 20:14 · Struk kasir · gagal")
        If Not view.BandWarn OrElse Not view.Activity(0).Warn Then Throw New InvalidOperationException("Gagal cetak tidak diberi warna peringatan.")
        fresh.LastPrint = New PrintOutcome With {.At = now.AddDays(-1), .Role = "QRLABEL", .Ok = False, .ErrorCode = "BAD_PAYLOAD"}
        view = BuildStatusView(fresh)
        Expect("pita ditolak", view.BandText, "Cetakan terakhir ditolak agen — salin info untuk bantuan.")
        Expect("baris ditolak", view.Activity(0).Value, "Kemarin 20:15 · Label QR · ditolak")
        fresh.LastPrint = New PrintOutcome With {.At = New DateTime(2026, 10, 2, 8, 5, 0), .Role = "DELIVERY", .Ok = True}
        view = BuildStatusView(fresh)
        Expect("baris berhasil lama", view.Activity(0).Value, "2 Okt 08:05 · Surat jalan · berhasil")
        Expect("pita setelah berhasil", view.BandText, "Aktif — siap menerima cetakan dari GamaPOS")

        ' Printer pilihan hilang dari Windows → peringatan dengan langkah perbaikan.
        Dim missing As StatusSnapshot = Snapshot(now)
        missing.Roles(0) = New RoleState With {.Role = "CASHIER", .Printer = "EPSON LAMA", .Installed = False}
        missing.Roles(1) = New RoleState With {.Role = "QRLABEL", .Printer = "XP-365B", .Installed = True}
        view = BuildStatusView(missing)
        Expect("pita printer hilang", view.BandText, "Printer struk kasir tidak ditemukan — pilih ulang di GamaPOS: Pengaturan → Printer.")
        Expect("baris printer hilang", view.Printers(0).Value & "/" & view.Printers(0).Note, "EPSON LAMA/Tidak ditemukan di PC ini")
        Expect("baris printer dipilih", view.Printers(1).Value & "/" & view.Printers(1).Note, "XP-365B/")
        If Not BuildSupportText(missing).Contains("Peran CASHIER: EPSON LAMA [TIDAK TERPASANG]") Then
            Throw New InvalidOperationException("Teks bantuan tanpa tanda printer tidak terpasang.")
        End If

        ' Tanpa printer sama sekali.
        Dim empty As StatusSnapshot = Snapshot(now)
        empty.DefaultPrinter = ""
        empty.InstalledPrinters.Clear()
        view = BuildStatusView(empty)
        Expect("pita tanpa printer", view.BandText, "Belum ada printer terpasang di Windows.")
        Expect("struk tanpa printer", view.Printers(0).Value & "/" & view.Printers(0).Note, "Belum ada printer/Pasang printer di Windows")

        ' Keadaan pembaruan.
        For Each scenario In {
            (State:=UpdateState.Staged, Text:="Versi 1.4.1 siap; dipasang otomatis saat agen tidak dipakai 10 menit."),
            (State:=UpdateState.NotInstalled, Text:="Tidak tersedia (bukan instalasi resmi)"),
            (State:=UpdateState.CheckFailed, Text:="Belum dapat diperiksa; dicoba lagi otomatis"),
            (State:=UpdateState.NotChecked, Text:="Belum diperiksa")}
            Dim s As StatusSnapshot = Snapshot(now)
            s.Update = scenario.State
            s.StagedVersion = "1.4.1"
            Expect("pembaruan " & scenario.State.ToString(), BuildStatusView(s).Activity(1).Value, scenario.Text)
        Next
        Console.WriteLine("Status window model (P-626) passed: shop wording, warnings, support text.")
    End Sub

    Private Function Snapshot(now As DateTime) As StatusSnapshot
        Dim s As New StatusSnapshot With {
            .Now = now, .Version = "1.4.0", .DefaultPrinter = "EPSON TM-U220 Receipt",
            .InstalledPrinters = New List(Of String) From {"EPSON TM-U220 Receipt", "Microsoft Print to PDF"},
            .ConfigExists = False, .Update = UpdateState.UpToDate, .UpdateCheckedAt = now.AddMinutes(-5),
            .ListenAddress = "http://localhost:9111/", .DataDir = "C:\Data", .LogDir = "C:\Data\logs",
            .DeviceId = "00000000-0000-0000-0000-000000000001", .OsArch = "x64", .CatalogVersion = "0", .CatalogSource = "seed"}
        For Each role In StatusRoles
            s.Roles.Add(New RoleState With {.Role = role, .Printer = "", .Installed = False})
        Next
        Return s
    End Function

    Private Sub Expect(name As String, actual As String, expected As String)
        If actual <> expected Then Throw New InvalidOperationException(name & ": " & actual & " != " & expected)
    End Sub
End Module
