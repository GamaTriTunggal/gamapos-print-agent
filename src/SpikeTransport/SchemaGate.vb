' Gerbang versi murni: dapat diuji lintas-platform sebelum Windows/printer.
' Formatter v1 tidak boleh melihat amplop schema 2 dengan jobType yang sama.
Option Strict On
Option Explicit On

Imports Newtonsoft.Json.Linq

Module SchemaGate
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
