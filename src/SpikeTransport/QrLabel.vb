' Gama Print Agent — Label QR (qr_item_label & qr_invoice)
'
' Port modGlobalProcedure.printQrCode & printQrInv + encodeQrCode (modGlobalFunction).
' BEDA dari nota teks: pakai PrintDocument + GDI (DrawString/DrawImage) + ZXing.Net (BarcodeWriter → Bitmap).
' Targeting: set PrinterSettings.PrinterName = Resolve("QRLABEL") LANGSUNG (tak perlu SetDefaultPrinter).
' qr_item_label: itemId + harga (Courier 17 bold) lalu QR(itemId), `copies` salinan.
' qr_invoice   : invoiceNo (Courier 8 bold) lalu QR(invoiceNo), 1 salinan.
' Run di thread STA (sama spt cetak PrintDocument lain). ZXing.Net 0.16.9 — API persis aplikasi lama.

Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Printing

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

    Public Sub PrintQrItemLabel(p As QrItemLabelPayload)
        RunSta(Sub() RenderQrItem(p))
    End Sub

    Private Sub RenderQrItem(p As QrItemLabelPayload)
        Dim itemId As String = If(p.itemId, "")
        Dim priceStr As String = Fmt(p.salesPrice)
        Dim copies As Integer = Math.Min(20, Math.Max(1, p.copies))   ' clamp [1..20]: cegah cetak massal + CShort overflow
        PrintViaDoc(Sub(e As PrintPageEventArgs) DrawTwoLineQr(e, itemId, priceStr, itemId, 17), copies)
    End Sub

    Public Sub PrintQrInvoice(p As QrInvoicePayload)
        RunSta(Sub() RenderQrInvoice(p))
    End Sub

    Private Sub RenderQrInvoice(p As QrInvoicePayload)
        Dim inv As String = If(p.invoiceNo, "")
        PrintViaDoc(Sub(e As PrintPageEventArgs) DrawOneLineQr(e, inv, inv, 8), 1)
    End Sub

    ' Buat PrintDocument, target printer QRLABEL (atau default bila tak dipetakan), cetak `copies` salinan.
    Private Sub PrintViaDoc(painter As Action(Of PrintPageEventArgs), copies As Integer)
        Using doc As New PrintDocument()
            Dim target As String = Printers.Resolve("QRLABEL")
            If target <> "" Then
                doc.PrinterSettings.PrinterName = target
                If Not doc.PrinterSettings.IsValid Then
                    Throw New Exception("Printer QRLABEL '" & target & "' tidak ditemukan.")
                End If
            End If
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
    End Sub

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

End Module
