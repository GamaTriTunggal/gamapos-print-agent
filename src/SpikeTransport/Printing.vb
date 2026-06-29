' Gama Print Agent — Print Spike 2a
'
' Membuktikan agent bisa mencetak SILENT dari proses console, TANPA PowerPacks.
' Target: "Microsoft Print to PDF" dengan PrintToFile=True + PrintFileName → PDF ditulis
' langsung ke file, tanpa dialog "Save As".
'
' Print dijalankan di thread STA: PrintDocument / driver PDF dapat melempar error apartment
' (OLE/COM butuh STA) bila dipanggil dari thread MTA — dan thread loop HttpListener adalah MTA.

Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Printing
Imports System.IO
Imports System.Threading

Module Printing

    Private Const PdfPrinterName As String = "Microsoft Print to PDF"

    ' Cetak halaman contoh ke PDF (silent). Mengembalikan path file PDF yang dihasilkan.
    Public Function PrintTestPage() As String
        Dim outDir As String = AppPaths.JobsDir()
        Dim pdfPath As String = Path.Combine(outDir,
            "test-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & "-" & Guid.NewGuid().ToString("N").Substring(0, 4) & ".pdf")

        ' Jalankan print di thread STA; marshal exception kembali ke pemanggil.
        Dim printEx As Exception = Nothing
        Dim worker As New Thread(Sub() printEx = DoPrintToPdf(pdfPath))
        worker.IsBackground = True
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()
        If Not worker.Join(30000) Then
            Throw New TimeoutException("PRINT_TIMEOUT: cetak tes melebihi batas waktu (printer offline / dialog?).")
        End If
        If printEx IsNot Nothing Then Throw printEx

        ' Deteksi kegagalan diam: driver "sukses" tapi file tidak terbentuk / 0 byte.
        Dim fi As New FileInfo(pdfPath)
        If (Not fi.Exists) OrElse fi.Length = 0 Then
            Throw New Exception("Cetak selesai tanpa error tapi PDF kosong / tidak terbentuk: " & pdfPath)
        End If

        Return pdfPath
    End Function

    ' Dipanggil di thread STA. Mengembalikan Nothing jika sukses, atau Exception jika gagal.
    Private Function DoPrintToPdf(pdfPath As String) As Exception
        Try
            Using doc As New PrintDocument()
                doc.PrinterSettings.PrinterName = PdfPrinterName
                If Not doc.PrinterSettings.IsValid Then
                    Throw New Exception("Printer '" & PdfPrinterName & "' tidak ditemukan. Cek Windows Settings > Printers.")
                End If
                doc.PrinterSettings.PrintToFile = True
                doc.PrinterSettings.PrintFileName = pdfPath

                AddHandler doc.PrintPage, AddressOf OnPrintTestPage
                Try
                    doc.Print()
                Finally
                    RemoveHandler doc.PrintPage, AddressOf OnPrintTestPage
                End Try
            End Using
            Return Nothing
        Catch ex As Exception
            ' Bersihkan PDF parsial bila sempat terbentuk.
            Try
                If File.Exists(pdfPath) Then File.Delete(pdfPath)
            Catch
                ' abaikan kegagalan hapus
            End Try
            Return ex
        End Try
    End Function

    Private Sub OnPrintTestPage(sender As Object, e As PrintPageEventArgs)
        Dim lines() As String = {
            "       GAMA PRINT AGENT",
            "      Tes cetak 2a (PDF)",
            "----------------------------------------",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "",
            "Kalau PDF ini muncul, cetak SILENT jalan",
            "dan PrintDocument OK (belum pakai PowerPacks).",
            "----------------------------------------"
        }

        Using font As New Font("Courier New", 9)
            Dim y As Single = 10.0F
            Dim lineH As Single = font.GetHeight(e.Graphics)
            For Each ln As String In lines
                e.Graphics.DrawString(ln, font, Brushes.Black, 10.0F, y)
                y += lineH
            Next
        End Using

        e.HasMorePages = False
    End Sub

End Module
