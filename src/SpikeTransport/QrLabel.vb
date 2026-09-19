' Gama Print Agent — Label QR (qr_item_label & qr_invoice)
'
' Port modGlobalProcedure.printQrCode & printQrInv + encodeQrCode (modGlobalFunction).
' BEDA dari nota teks: pakai PrintDocument + GDI (DrawString/DrawImage) + ZXing.Net (BarcodeWriter → Bitmap).
' Targeting: set PrinterSettings.PrinterName = Resolve("QRLABEL") LANGSUNG (tak perlu SetDefaultPrinter).
' qr_item_label: itemId + harga (Courier 17 bold) lalu QR(itemId), `copies` salinan.
' qr_invoice   : invoiceNo (Courier 8 bold) lalu QR(invoiceNo), 1 salinan.
' Run di thread STA (sama spt cetak PrintDocument lain). ZXing.Net 0.16.9 — API persis aplikasi lama.
' P-592 (residu PR-12): peran QRLABEL BELUM dipetakan → tetap dicetak ke printer default Windows
' (perilaku 1.0.2; menolak = K-1c, ditahan) TETAPI hasilnya membawa `warning: ROLE_UNMAPPED` + nama
' printer yang dipakai, supaya web bisa memberi tahu toko alih-alih diam.

Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Printing

' Hasil cetak label: printer yang dipakai + apakah jatuh ke default Windows karena peran belum dipetakan.
Public Class QrPrintOutcome
    Public Property Printer As String = ""
    Public Property FellBackToDefault As Boolean = False
End Class

Module QrLabel

    ' Port encodeQrCode (ZXing.Net) — Bitmap QR.
    Private Function EncodeQr(value As String, size As Integer) As Bitmap
        Dim writer As New ZXing.BarcodeWriter()
        writer.Format = ZXing.BarcodeFormat.QR_CODE
        writer.Options = New ZXing.Common.EncodingOptions()
        writer.Options.Height = size
        writer.Options.Width = size
        writer.Options.Margin = 0
        Return writer.Write(value)
    End Function

    Public Function PrintQrItemLabel(p As QrItemLabelPayload) As QrPrintOutcome
        Dim outcome As QrPrintOutcome = Nothing
        RunSta(Sub() outcome = RenderQrItem(p))
        Return outcome
    End Function

    Private Function RenderQrItem(p As QrItemLabelPayload) As QrPrintOutcome
        Dim itemId As String = If(p.itemId, "")
        Dim priceStr As String = Fmt(p.salesPrice)
        Dim copies As Integer = Math.Min(20, Math.Max(1, p.copies))   ' clamp [1..20]: cegah cetak massal + CShort overflow
        Return PrintViaDoc(Sub(e As PrintPageEventArgs) DrawTwoLineQr(e, itemId, priceStr, itemId, 17), copies)
    End Function

    Public Function PrintQrInvoice(p As QrInvoicePayload) As QrPrintOutcome
        Dim outcome As QrPrintOutcome = Nothing
        RunSta(Sub() outcome = RenderQrInvoice(p))
        Return outcome
    End Function

    Private Function RenderQrInvoice(p As QrInvoicePayload) As QrPrintOutcome
        Dim inv As String = If(p.invoiceNo, "")
        Return PrintViaDoc(Sub(e As PrintPageEventArgs) DrawOneLineQr(e, inv, inv, 8), 1)
    End Function

    ' Buat PrintDocument, target printer QRLABEL (atau default bila tak dipetakan — dilaporkan di
    ' hasil, P-592), cetak `copies` salinan.
    Private Function PrintViaDoc(painter As Action(Of PrintPageEventArgs), copies As Integer) As QrPrintOutcome
        Dim outcome As New QrPrintOutcome()
        Using doc As New PrintDocument()
            Dim target As String = Printers.Resolve("QRLABEL")
            If target <> "" Then
                doc.PrinterSettings.PrinterName = target
                If Not doc.PrinterSettings.IsValid Then
                    Throw New Exception("Printer QRLABEL '" & target & "' tidak ditemukan.")
                End If
            Else
                outcome.FellBackToDefault = True
            End If
            outcome.Printer = doc.PrinterSettings.PrinterName
            SelectLabelStock(doc)   ' pilih ukuran ~40×30 eksplisit (jangan bergantung default printer)
            doc.PrinterSettings.Copies = CShort(copies)
            Dim handler As PrintPageEventHandler =
                Sub(sender As Object, e As PrintPageEventArgs)
                    painter(e)
                    e.HasMorePages = False
                End Sub
            AddHandler doc.PrintPage, handler
            Try
                doc.Print()
            Finally
                RemoveHandler doc.PrintPage, handler
            End Try
        End Using
        Return outcome
    End Function

    ' itemId + harga lalu QR (label barang).
    Private Sub DrawTwoLineQr(e As PrintPageEventArgs, lineA As String, lineB As String, qrValue As String, fontSize As Integer)
        Using f As New Font("Courier New", fontSize, FontStyle.Bold)
            Const x As Single = 10.0F
            Dim y As Single = 3.0F
            e.Graphics.DrawString(lineA, f, Brushes.Black, x, y)
            y += fontSize + 5
            e.Graphics.DrawString(lineB, f, Brushes.Black, x, y)
            y += fontSize + 5
            Using qr As Bitmap = EncodeQr(qrValue, 70)
                e.Graphics.DrawImage(qr, x, y)
            End Using
        End Using
    End Sub

    ' Satu baris teks lalu QR (label invoice).
    Private Sub DrawOneLineQr(e As PrintPageEventArgs, lineA As String, qrValue As String, fontSize As Integer)
        Using f As New Font("Courier New", fontSize, FontStyle.Bold)
            Const x As Single = 10.0F
            Dim y As Single = 3.0F
            e.Graphics.DrawString(lineA, f, Brushes.Black, x, y)
            y += fontSize + 5
            Using qr As Bitmap = EncodeQr(qrValue, 70)
                e.Graphics.DrawImage(qr, x, y)
            End Using
        End Using
    End Sub

    ' Pilih ukuran kertas ~40mm × 30mm agar label tak bergantung DEFAULT printer. (Batasan driver Seagull:
    ' golden config membawa DAFTAR stock, tapi install baru tak menyetel default terpilih.) PaperSize.Width/
    ' Height dalam 1/100 inci → 40mm≈157, 30mm≈118 (toleransi ±8; cocokkan kedua orientasi).
    ' Tak ketemu → biarkan default printer (perilaku lama, aman).
    Private Sub SelectLabelStock(doc As PrintDocument)
        Try
            For Each ps As PaperSize In doc.PrinterSettings.PaperSizes
                If (NearHundredths(ps.Width, 157) AndAlso NearHundredths(ps.Height, 118)) OrElse
                   (NearHundredths(ps.Width, 118) AndAlso NearHundredths(ps.Height, 157)) Then
                    doc.DefaultPageSettings.PaperSize = ps
                    Return
                End If
            Next
        Catch
            ' abaikan → pakai default printer
        End Try
    End Sub

    Private Function NearHundredths(val As Integer, target As Integer) As Boolean
        Return Math.Abs(val - target) <= 8
    End Function

End Module
