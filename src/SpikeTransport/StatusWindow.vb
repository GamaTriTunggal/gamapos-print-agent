' Jendela Status agen (P-626, mockup disetujui pemilik 11 Okt 2026).
' Menggantikan MessageBox berisi folder/berkas. Isi dihitung StatusModel;
' jendela ini hanya menata dan menyegarkan tiap 3 detik selama terbuka.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Friend NotInheritable Class StatusWindow
    Inherits Form

    Private Shared _current As StatusWindow = Nothing

    Private Shared ReadOnly Ink As Color = Color.FromArgb(16, 24, 21)
    Private Shared ReadOnly Muted As Color = Color.FromArgb(90, 104, 98)
    Private Shared ReadOnly LineColor As Color = Color.FromArgb(218, 226, 221)
    Private Shared ReadOnly OkInk As Color = Color.FromArgb(11, 127, 88)
    Private Shared ReadOnly OkBack As Color = Color.FromArgb(227, 243, 236)
    Private Shared ReadOnly WarnInk As Color = Color.FromArgb(143, 90, 12)
    Private Shared ReadOnly WarnBack As Color = Color.FromArgb(255, 244, 220)
    Private Shared ReadOnly FooterBack As Color = Color.FromArgb(243, 243, 243)

    Private Const ContentWidth As Integer = 420
    Private Const CaptionWidth As Integer = 130

    Private ReadOnly _subtitle As New Label()
    Private ReadOnly _band As New Panel()
    Private ReadOnly _bandText As New Label()
    Private ReadOnly _printerValues As New List(Of Label)()
    Private ReadOnly _printerNotes As New List(Of Label)()
    Private ReadOnly _activityValues As New List(Of Label)()
    Private ReadOnly _copyButton As New Button()
    Private ReadOnly _refreshTimer As New Timer() With {.Interval = 3000}
    Private ReadOnly _copyResetTimer As New Timer() With {.Interval = 2000}
    Private _snapshot As StatusSnapshot

    ' Satu jendela saja: klik Status lagi membawa jendela yang sama ke depan.
    Friend Shared Sub ShowSingle()
        If _current IsNot Nothing AndAlso Not _current.IsDisposed Then
            _current.Reload()
            If _current.WindowState = FormWindowState.Minimized Then _current.WindowState = FormWindowState.Normal
            _current.Activate()
            Return
        End If
        _current = New StatusWindow()
        _current.Show()
        _current.Activate()
    End Sub

    Private Sub New()
        Text = "Status agen cetak"
        Font = New Font("Segoe UI", 9.75F)
        BackColor = Color.White
        ForeColor = Ink
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        MinimizeBox = False
        StartPosition = FormStartPosition.CenterScreen
        AutoScaleMode = AutoScaleMode.Font
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink
        Try
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        Dim root As New TableLayoutPanel With {
            .ColumnCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding = New Padding(0), .Margin = New Padding(0)}
        Dim body As New TableLayoutPanel With {
            .ColumnCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding = New Padding(20, 18, 20, 14), .Margin = New Padding(0)}
        body.Controls.Add(BuildHeader())
        body.Controls.Add(BuildBand())
        body.Controls.Add(SectionTitle("PRINTER"))
        For Each role As String In StatusRoles
            Dim value As New Label()
            Dim note As New Label()
            _printerValues.Add(value)
            _printerNotes.Add(note)
            body.Controls.Add(BuildRow(RoleCaption(role), value, note))
        Next
        body.Controls.Add(SectionTitle("AKTIVITAS"))
        For Each caption As String In {"Cetakan terakhir", "Pembaruan"}
            Dim value As New Label()
            _activityValues.Add(value)
            body.Controls.Add(BuildRow(caption, value, Nothing))
        Next
        root.Controls.Add(body)
        root.Controls.Add(BuildFooter())
        Controls.Add(root)

        AddHandler _refreshTimer.Tick, Sub() Reload()
        AddHandler _copyResetTimer.Tick, Sub()
                                             _copyResetTimer.Stop()
                                             _copyButton.Text = "Salin info untuk bantuan"
                                         End Sub
        Reload()
        _refreshTimer.Start()
    End Sub

    Private Function BuildHeader() As Control
        Dim header As New FlowLayoutPanel With {
            .AutoSize = True, .WrapContents = False, .Margin = New Padding(0, 0, 0, 14)}
        Dim logo As New PictureBox With {
            .Size = New Size(40, 40), .SizeMode = PictureBoxSizeMode.Zoom, .Margin = New Padding(0, 0, 12, 0)}
        Try
            logo.Image = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap()
        Catch
        End Try
        Dim titles As New FlowLayoutPanel With {
            .AutoSize = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Margin = New Padding(0)}
        titles.Controls.Add(New Label With {
            .Text = "GamaPOS Print Agent", .AutoSize = True, .Font = New Font("Segoe UI Semibold", 12.0F),
            .Margin = New Padding(0, 0, 0, 1)})
        _subtitle.AutoSize = True
        _subtitle.ForeColor = Muted
        _subtitle.Margin = New Padding(1, 0, 0, 0)
        titles.Controls.Add(_subtitle)
        header.Controls.Add(logo)
        header.Controls.Add(titles)
        Return header
    End Function

    Private Function BuildBand() As Control
        _band.Width = ContentWidth
        _band.AutoSize = True
        _band.AutoSizeMode = AutoSizeMode.GrowAndShrink
        _band.Padding = New Padding(12, 9, 12, 9)
        _band.Margin = New Padding(0, 0, 0, 16)
        _bandText.AutoSize = True
        _bandText.MaximumSize = New Size(ContentWidth - 24, 0)
        _bandText.MinimumSize = New Size(ContentWidth - 24, 0)
        _bandText.Font = New Font("Segoe UI Semibold", 9.75F)
        _bandText.Margin = New Padding(0)
        _band.Controls.Add(_bandText)
        Return _band
    End Function

    Private Function SectionTitle(caption As String) As Control
        Return New Label With {
            .Text = caption, .AutoSize = True, .ForeColor = Muted,
            .Font = New Font("Segoe UI Semibold", 8.25F), .Margin = New Padding(0, 2, 0, 6)}
    End Function

    Private Function BuildRow(caption As String, value As Label, note As Label) As Control
        Dim row As New TableLayoutPanel With {
            .ColumnCount = 2, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Margin = New Padding(0, 0, 0, 8)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, CaptionWidth))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, ContentWidth - CaptionWidth))
        row.Controls.Add(New Label With {
            .Text = caption, .AutoSize = True, .ForeColor = Muted, .Margin = New Padding(0)}, 0, 0)
        Dim cell As New FlowLayoutPanel With {
            .AutoSize = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Margin = New Padding(0)}
        value.AutoSize = True
        value.MaximumSize = New Size(ContentWidth - CaptionWidth, 0)
        value.Font = New Font("Segoe UI Semibold", 9.75F)
        value.Margin = New Padding(0)
        cell.Controls.Add(value)
        If note IsNot Nothing Then
            note.AutoSize = True
            note.MaximumSize = New Size(ContentWidth - CaptionWidth, 0)
            note.Font = New Font("Segoe UI", 8.75F)
            note.Margin = New Padding(0, 1, 0, 0)
            cell.Controls.Add(note)
        End If
        row.Controls.Add(cell, 1, 0)
        Return row
    End Function

    Private Function BuildFooter() As Control
        Dim footer As New Panel With {
            .Width = ContentWidth + 40, .Height = 52, .BackColor = FooterBack, .Margin = New Padding(0)}
        AddHandler footer.Paint, Sub(sender As Object, e As PaintEventArgs)
                                     Using pen As New Pen(LineColor)
                                         e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0)
                                     End Using
                                 End Sub
        _copyButton.Text = "Salin info untuk bantuan"
        _copyButton.AutoSize = True
        _copyButton.MinimumSize = New Size(0, 30)
        _copyButton.Padding = New Padding(8, 0, 8, 0)
        _copyButton.Location = New Point(20, 11)
        AddHandler _copyButton.Click, AddressOf OnCopy
        Dim closeButton As New Button With {
            .Text = "Tutup", .Width = 96, .Height = 30, .FlatStyle = FlatStyle.Flat,
            .BackColor = OkInk, .ForeColor = Color.White, .Font = New Font("Segoe UI Semibold", 9.75F)}
        closeButton.FlatAppearance.BorderSize = 0
        closeButton.Location = New Point(footer.Width - 20 - closeButton.Width, 11)
        AddHandler closeButton.Click, Sub() Me.Close()
        footer.Controls.Add(_copyButton)
        footer.Controls.Add(closeButton)
        AcceptButton = closeButton
        CancelButton = closeButton
        Return footer
    End Function

    Private Sub Reload()
        Try
            _snapshot = StatusCapture.Capture()
        Catch ex As Exception
            Console.WriteLine("Status capture error: " & ex.GetType().Name)
            Return
        End Try
        Dim view As StatusView = BuildStatusView(_snapshot)
        _subtitle.Text = view.Subtitle
        _band.BackColor = If(view.BandWarn, WarnBack, OkBack)
        _bandText.ForeColor = If(view.BandWarn, WarnInk, OkInk)
        _bandText.Text = "●  " & view.BandText
        For index As Integer = 0 To view.Printers.Count - 1
            Dim row As StatusRow = view.Printers(index)
            _printerValues(index).Text = row.Value
            _printerNotes(index).Text = row.Note
            _printerNotes(index).Visible = row.Note <> ""
            _printerNotes(index).ForeColor = If(row.Warn, WarnInk, Muted)
        Next
        For index As Integer = 0 To view.Activity.Count - 1
            _activityValues(index).Text = view.Activity(index).Value
            _activityValues(index).ForeColor = If(view.Activity(index).Warn, WarnInk, Ink)
        Next
    End Sub

    Private Sub OnCopy(sender As Object, e As EventArgs)
        Try
            Reload()
            Clipboard.SetText(BuildSupportText(_snapshot))
            _copyButton.Text = "Tersalin ✓"
        Catch
            _copyButton.Text = "Gagal menyalin, coba lagi"
        End Try
        _copyResetTimer.Stop()
        _copyResetTimer.Start()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _refreshTimer.Stop()
        _copyResetTimer.Stop()
        If _current Is Me Then _current = Nothing
        MyBase.OnFormClosed(e)
    End Sub
End Class
