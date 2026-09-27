Option Strict On
Option Explicit On

Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

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
        CheckSaleV2(IO.Path.Combine(args(0), "v2", "sale_cash.sample.json"))
        Console.WriteLine("Schema gate: " & cases.Length & " cases + " & fixtures.Length & " fixtures v1 passed; sale v2 parser: 8 accepted + 11 rejected passed.")
    End Sub

    Private Sub CheckSaleV2(path As String)
        Dim source As String = IO.File.ReadAllText(path)
        ParseSaleV2(source)
        Dim baseline As JObject = JObject.Parse(source)
        Accept("cash", baseline)

        Dim edc As JObject = CType(baseline.DeepClone(), JObject)
        Dim payload As JObject = CType(edc("payload"), JObject)
        Dim amounts As JObject = CType(payload("amounts"), JObject)
        payload("paymentMethod") = "EDC"
        payload("noncashMethod") = "EDC"
        amounts("cashSen") = "0"
        amounts("noncashSen") = "1950000"
        amounts("customerFeeSen") = "500"
        amounts("merchantFeeSen") = "250"
        amounts("customerPaysSen") = "1950500"
        amounts("merchantReceivesSen") = "1950250"
        amounts("tenderSen") = "0"
        amounts("changeSen") = "0"
        Accept("edc fees", edc)

        Dim split As JObject = CType(edc.DeepClone(), JObject)
        payload = CType(split("payload"), JObject)
        amounts = CType(payload("amounts"), JObject)
        split("jobType") = "split_receipt"
        payload("paymentMethod") = "SPLIT"
        amounts("cashSen") = "1000000"
        amounts("noncashSen") = "950000"
        amounts("tenderSen") = "1050000"
        amounts("changeSen") = "50000"
        Accept("split edc", split)

        Dim splitWire As JObject = CType(split.DeepClone(), JObject)
        payload = CType(splitWire("payload"), JObject)
        amounts = CType(payload("amounts"), JObject)
        payload("noncashMethod") = "WIRE"
        amounts("customerFeeSen") = "0"
        amounts("merchantFeeSen") = "0"
        amounts("customerPaysSen") = "1950000"
        amounts("merchantReceivesSen") = "1950000"
        Accept("split wire", splitWire)

        Dim credit As JObject = CType(baseline.DeepClone(), JObject)
        payload = CType(credit("payload"), JObject)
        amounts = CType(payload("amounts"), JObject)
        credit("jobType") = "kasbon_receipt"
        payload("paymentMethod") = "CREDIT"
        payload("transactionId") = ""
        amounts("principalAppliedSen") = "0"
        amounts("remainingSen") = "1950000"
        amounts("cashSen") = "0"
        amounts("customerPaysSen") = "0"
        amounts("merchantReceivesSen") = "0"
        amounts("tenderSen") = "0"
        amounts("changeSen") = "0"
        Accept("DP0", credit)

        Dim deposit As JObject = CType(credit.DeepClone(), JObject)
        payload = CType(deposit("payload"), JObject)
        amounts = CType(payload("amounts"), JObject)
        payload("transactionId") = "IN/260927/000001"
        amounts("principalAppliedSen") = "600000"
        amounts("remainingSen") = "1350000"
        amounts("cashSen") = "600000"
        amounts("customerPaysSen") = "600000"
        amounts("merchantReceivesSen") = "600000"
        amounts("tenderSen") = "600000"
        Accept("DP positif", deposit)

        Dim reprint As JObject = CType(baseline.DeepClone(), JObject)
        CType(reprint("payload"), JObject)("reprint") = JObject.Parse("{""date"":""2026-09-28"",""time"":""08:00"",""processor"":{""userId"":""3"",""name"":""Kasir Ulang""}}")
        Accept("reprint", reprint)
        Dim checkedReprint As JObject = ParseSaleV2(reprint.ToString(Formatting.None))
        If CStr(checkedReprint("payload")("originalProcessor")("name")) <> "Siti" OrElse
           CStr(checkedReprint("payload")("reprint")("processor")("name")) <> "Kasir Ulang" Then
            Throw New InvalidOperationException("Pemroses asli berubah saat cetak ulang.")
        End If

        Dim fraction As JObject = CType(baseline.DeepClone(), JObject)
        payload = CType(fraction("payload"), JObject)
        payload("items") = JArray.Parse("[{""name"":""A"",""quantity100"":""50"",""unit"":""PCS"",""priceSen"":""1"",""totalSen"":""1""},{""name"":""B"",""quantity100"":""50"",""unit"":""PCS"",""priceSen"":""1"",""totalSen"":""0""},{""name"":""C"",""quantity100"":""50"",""unit"":""PCS"",""priceSen"":""1"",""totalSen"":""1""}]")
        amounts = CType(payload("amounts"), JObject)
        For Each name As String In {"grossSen", "netSen", "principalAppliedSen", "cashSen", "customerPaysSen", "merchantReceivesSen", "tenderSen"}
            amounts(name) = "2"
        Next
        amounts("roundingSen") = "0"
        amounts("changeSen") = "0"
        Accept("residu satu sen", fraction)

        amounts = CType(CType(edc("payload"), JObject)("amounts"), JObject)
        amounts("merchantReceivesSen") = "1949750"
        Reject("rumus fee lama", edc.ToString(Formatting.None))
        Reject("schema duplikat", source.Replace("""schemaVersion"":2", """schemaVersion"":2,""schemaVersion"":1"))
        Reject("sen tidak kanonik", source.Replace("""roundingSen"":""25200""", """roundingSen"":""025200"""))
        Reject("field asing", source.Replace("""copies"":1", """copies"":1,""total"":19500"))
        Reject("metode salah", source.Replace("""paymentMethod"":""CASH""", """paymentMethod"":""EDC"""))
        Reject("total tidak cocok", source.Replace("""grossSen"":""1975200""", """grossSen"":""1975199"""))
        Reject("retur belum ada", source.Replace("""jobType"":""cashier_receipt""", """jobType"":""return_note"""))
        Reject("keluarga belum didukung", source.Replace("""jobType"":""cashier_receipt""", """jobType"":""receivable_proof"""))
        Reject("duplikat jumlah", source.Replace("""roundingSen"":""25200""", """roundingSen"":""25200"",""roundingSen"":""0"""))
        Reject("baris pecahan palsu", fraction.ToString(Formatting.None).Replace("""totalSen"":""0""", """totalSen"":""2"""))
        Reject("kasbon diskon manual", credit.ToString(Formatting.None).Replace("""discountSen"":""0""", """discountSen"":""1"""))
    End Sub

    Private Sub Accept(name As String, job As JObject)
        Try
            ParseSaleV2(job.ToString(Formatting.None))
        Catch ex As Exception
            Throw New InvalidOperationException(name & " ditolak: " & ex.Message, ex)
        End Try
    End Sub

    Private Sub Reject(name As String, raw As String)
        Try
            ParseSaleV2(raw)
        Catch
            Return
        End Try
        Throw New InvalidOperationException(name & " diterima padahal tidak sah.")
    End Sub
End Module
