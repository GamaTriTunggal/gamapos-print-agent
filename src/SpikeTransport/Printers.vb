' Gama Print Agent — Targeting printer per-role
'
' Aplikasi VB.NET lama TIDAK punya targeting (SetDefaultPrinter = stub kosong; semua ke default Windows).
' Ini infrastruktur BARU: peta role→nama printer dari printers.json (di samping exe; per-PC).
' - Nota PowerPacks (teks, hanya bisa cetak ke DEFAULT) → WithRolePrinter ganti default Windows sesaat (P/Invoke).
' - Label QR (PrintDocument) → AKAN set PrinterSettings.PrinterName = Resolve(role) langsung (Family B; belum di-wire).
' Fallback: role tak dipetakan / printers.json tak ada → pakai default Windows (perilaku sekarang; tes VM tetap jalan).

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Drawing.Printing
Imports Newtonsoft.Json

Module Printers

    Private _map As Dictionary(Of String, String) = Nothing

    Private Function Map() As Dictionary(Of String, String)
        If _map IsNot Nothing Then Return _map
        Dim m As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Try
            Dim path As String = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "printers.json")
            If File.Exists(path) Then
                Dim d = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(File.ReadAllText(path))
                If d IsNot Nothing Then m = New Dictionary(Of String, String)(d, StringComparer.OrdinalIgnoreCase)
            End If
        Catch
            ' abaikan parse/IO error → peta kosong → fallback default Windows
        End Try
        _map = m
        Return _map
    End Function

    ' Nama printer Windows utk role, atau "" bila tak dipetakan (→ caller pakai default Windows).
    Public Function Resolve(role As String) As String
        If String.IsNullOrEmpty(role) Then Return ""
        Dim name As String = Nothing
        If Map().TryGetValue(role, name) AndAlso Not String.IsNullOrEmpty(name) Then Return name
        Return ""
    End Function

    Public Function Installed() As List(Of String)
        Dim r As New List(Of String)()
        Try
            For Each p As String In PrinterSettings.InstalledPrinters
                r.Add(p)
            Next
        Catch
        End Try
        Return r
    End Function

    Public Function CurrentDefault() As String
        Try
            Return New PrinterSettings().PrinterName
        Catch
            Return ""
        End Try
    End Function

    ' JSON utk GET /printers: daftar printer terpasang + peta role saat ini + default Windows.
    Public Function StatusJson() As String
        Dim roles As New Dictionary(Of String, Object)()
        For Each role As String In New String() {"CASHIER", "DELIVERY", "QRLABEL", "REPORT"}
            Dim n As String = Resolve(role)
            roles(role) = If(n = "", Nothing, CObj(n))
        Next
        Dim obj As New Dictionary(Of String, Object) From {
            {"ok", True},
            {"installed", Installed()},
            {"roles", roles},
            {"default", CurrentDefault()}
        }
        Return JsonConvert.SerializeObject(obj)
    End Function

    <DllImport("winspool.drv", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Function SetDefaultPrinter(<MarshalAs(UnmanagedType.LPTStr)> name As String) As Boolean
    End Function

    ' Jalankan body() dgn default printer Windows di-set ke printer role, lalu dikembalikan.
    ' Role tak dipetakan → jalankan apa adanya (default sekarang). Dipakai utk nota PowerPacks.
    ' body() dijalankan SINKRON (mis. RunSta yg join) → default tetap ter-set selama cetak, baru dikembalikan.
    Public Sub WithRolePrinter(role As String, body As Action)
        Dim target As String = Resolve(role)
        If String.IsNullOrEmpty(target) Then
            body()
            Return
        End If
        Dim prev As String = CurrentDefault()
        Dim switched As Boolean = SetDefaultPrinter(target)
        Try
            body()
        Finally
            If switched AndAlso Not String.IsNullOrEmpty(prev) Then
                If Not SetDefaultPrinter(prev) Then Console.WriteLine("WARN: gagal kembalikan default printer ke " & prev)
            End If
        End Try
    End Sub

End Module
