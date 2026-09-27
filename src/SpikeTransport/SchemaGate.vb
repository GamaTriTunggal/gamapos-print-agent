' Gerbang versi murni: dapat diuji lintas-platform sebelum Windows/printer.
' Formatter v1 tidak boleh melihat amplop schema 2 dengan jobType yang sama.
Option Strict On
Option Explicit On

Imports Newtonsoft.Json.Linq

Module SchemaGate
    ' Rute murni: agent yang kelak mengiklankan schema 2 tetap menerima v1.
    ' Pada build kini supportedVersion=1, sehingga v2 tetap ditolak.
    Friend Function RoutePrintSchema(body As String, supportedVersion As Integer) As String
        Dim v1 As String = CheckPrintSchema(body, 1)
        If v1 = "BAD_PAYLOAD" Then Return v1
        If v1 = "OK" Then Return "V1"
        If supportedVersion >= 2 Then
            Dim v2 As String = CheckPrintSchema(body, 2)
            If v2 = "BAD_PAYLOAD" Then Return v2
            If v2 = "OK" Then Return "V2"
        End If
        Return "UNSUPPORTED_SCHEMA"
    End Function

    Friend Function CheckPrintSchema(body As String, supportedVersion As Integer) As String
        Dim envelope As JObject
        Try
            envelope = JObject.Parse(body, New JsonLoadSettings With {
                .DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error})
        Catch
            Return "BAD_PAYLOAD"
        End Try
        Dim schema As JToken = envelope("schemaVersion")
        Dim version As Integer = 0
        If schema Is Nothing OrElse schema.Type <> JTokenType.Integer OrElse
           Not Integer.TryParse(schema.ToString(), version) OrElse version <> supportedVersion Then
            Return "UNSUPPORTED_SCHEMA"
        End If
        Return "OK"
    End Function
End Module
