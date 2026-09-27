Option Strict On
Option Explicit On

Module Program
    Sub Main(args As String())
        If args.Length <> 1 OrElse Not IO.Directory.Exists(args(0)) Then
            Throw New ArgumentException("Diperlukan jalur direktori fixtures v1.")
        End If
        Dim cases As (Name As String, Body As String, Expected As String)() = {
            ("v1", "{""schemaVersion"":1,""jobType"":""cashier_receipt""}", "OK"),
            ("v2 same job", "{""schemaVersion"":2,""jobType"":""cashier_receipt""}", "UNSUPPORTED_SCHEMA"),
            ("future", "{""schemaVersion"":3,""jobType"":""cashier_receipt""}", "UNSUPPORTED_SCHEMA"),
            ("missing", "{""jobType"":""cashier_receipt""}", "UNSUPPORTED_SCHEMA"),
            ("float", "{""schemaVersion"":1.0,""jobType"":""cashier_receipt""}", "UNSUPPORTED_SCHEMA"),
            ("string", "{""schemaVersion"":""1"",""jobType"":""cashier_receipt""}", "UNSUPPORTED_SCHEMA"),
            ("overflow", "{""schemaVersion"":999999999999999999999999}", "UNSUPPORTED_SCHEMA"),
            ("duplicate root", "{""schemaVersion"":2,""schemaVersion"":1,""jobType"":""cashier_receipt""}", "BAD_PAYLOAD"),
            ("duplicate nested", "{""schemaVersion"":1,""payload"":{""amount"":1,""amount"":2}}", "BAD_PAYLOAD"),
            ("malformed", "{""schemaVersion"":1", "BAD_PAYLOAD"),
            ("array", "[{""schemaVersion"":1}]", "BAD_PAYLOAD")
        }
        For Each scenario In cases
            Dim actual As String = CheckPrintSchema(scenario.Body, 1)
            If actual <> scenario.Expected Then
                Throw New InvalidOperationException(scenario.Name & ": " & actual & " != " & scenario.Expected)
            End If
        Next
        Dim fixtures As String() = IO.Directory.GetFiles(args(0), "*.sample.json")
        If fixtures.Length < 17 Then Throw New InvalidOperationException("Fixture v1 kurang dari 17.")
        For Each fixture As String In fixtures
            Dim actual As String = CheckPrintSchema(IO.File.ReadAllText(fixture), 1)
            If actual <> "OK" Then Throw New InvalidOperationException(fixture & ": " & actual)
        Next
        Console.WriteLine("Schema gate: " & cases.Length & " cases + " & fixtures.Length & " fixtures v1 passed.")
    End Sub
End Module
