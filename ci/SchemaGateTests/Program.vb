Option Strict On
Option Explicit On

Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.Globalization
Imports System.Linq

Module Program
    Sub Main(args As String())
        If args.Length < 1 OrElse args.Length > 4 OrElse Not IO.Directory.Exists(args(0)) OrElse
           (args.Length >= 2 AndAlso Not IO.Directory.Exists(args(1))) OrElse
           (args.Length >= 3 AndAlso Not IO.Directory.Exists(args(2))) OrElse
           (args.Length = 4 AndAlso Not IO.Directory.Exists(args(3))) Then
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
        Dim routes As (Name As String, Body As String, Supported As Integer, Expected As String)() = {
            ("v1 active", "{""schemaVersion"":1,""jobType"":""cashier_receipt""}", 1, "V1"),
            ("v2 gated", "{""schemaVersion"":2,""jobType"":""cashier_receipt""}", 1, "UNSUPPORTED_SCHEMA"),
            ("v1 retained", "{""schemaVersion"":1,""jobType"":""cashier_receipt""}", 2, "V1"),
            ("v2 future", "{""schemaVersion"":2,""jobType"":""cashier_receipt""}", 2, "V2"),
            ("future unsupported", "{""schemaVersion"":3,""jobType"":""cashier_receipt""}", 2, "UNSUPPORTED_SCHEMA"),
            ("duplicate blocked", "{""schemaVersion"":2,""schemaVersion"":1}", 2, "BAD_PAYLOAD"),
            ("malformed blocked", "{""schemaVersion"":2", 2, "BAD_PAYLOAD")
        }
        For Each scenario In routes
            Dim actual As String = RoutePrintSchema(scenario.Body, scenario.Supported)
            If actual <> scenario.Expected Then
                Throw New InvalidOperationException(scenario.Name & ": " & actual & " != " & scenario.Expected)
            End If
        Next
        Dim capabilities As JObject = JObject.FromObject(HealthPrintCapabilities())
        If Not JToken.DeepEquals(capabilities("supportedPrintSchemas"), New JArray(1)) OrElse
           Not JToken.DeepEquals(capabilities("supportedPrintJobTypesV2"), New JArray()) OrElse
           RoutePrintSchema("{""schemaVersion"":2,""jobType"":""return_note""}", 1) <> "UNSUPPORTED_SCHEMA" Then
            Throw New InvalidOperationException("Health mengiklankan job v2 sebelum gerbang cetak dibuka.")
        End If
        For Each scenario In {
            (Name:="cashier_receipt", Family:="SALE"), (Name:="kasbon_receipt", Family:="SALE"),
            (Name:="split_receipt", Family:="SALE"),
            (Name:="receivable_selected", Family:="RECEIVABLE"),
            (Name:="receivable_selected_card", Family:="RECEIVABLE"),
            (Name:="receivable_proof", Family:="RECEIVABLE"),
            (Name:="return_note", Family:="RETURN"),
            (Name:="Cashier_Receipt", Family:="UNSUPPORTED_JOBTYPE")}
            If SelectV2Family(scenario.Name) <> scenario.Family Then
                Throw New InvalidOperationException("Keluarga job v2 salah: " & scenario.Name)
            End If
        Next
        Dim fixtures As String() = IO.Directory.GetFiles(args(0), "*.sample.json")
        If fixtures.Length < 17 Then Throw New InvalidOperationException("Fixture v1 kurang dari 17.")
        For Each fixture As String In fixtures
            Dim actual As String = CheckPrintSchema(IO.File.ReadAllText(fixture), 1)
            If actual <> "OK" Then Throw New InvalidOperationException(fixture & ": " & actual)
        Next
        Dim saleFixture As String = IO.File.ReadAllText(IO.Path.Combine(args(0), "v2", "sale_cash.sample.json"))
        If RoutePrintSchema(saleFixture, 1) <> "UNSUPPORTED_SCHEMA" OrElse
           RoutePrintSchema(saleFixture, 2) <> "V2" Then
            Throw New InvalidOperationException("Fixture nota v2 tidak dipilih sesuai kemampuan agent.")
        End If
        CheckSaleV2(IO.Path.Combine(args(0), "v2", "sale_cash.sample.json"))
        CheckReceivableV2(IO.Path.Combine(args(0), "v2"))
        CheckReturnV2(IO.Path.Combine(args(0), "v2", "return_note.sample.json"))
        CheckPhysicalSamples(IO.Path.Combine(args(0), "v2"))
        If args.Length >= 2 Then CheckGoReceivableV2(args(1))
        If args.Length >= 3 Then CheckGoSaleV2(args(2))
        If args.Length = 4 Then CheckGoReturnV2(args(3))
        CheckNameLayout()
        CheckMoneyFormat()
        CheckItemLayout()
        CheckMetadataLayout()
        Console.WriteLine("Schema gate: " & cases.Length & " cases + " & routes.Length & " routes + " & fixtures.Length & " fixtures v1 passed; sale/receivable/return v2 parser accepted/rejected; return core and amount/name/money/item/metadata/correction layout passed.")
    End Sub

    Private Sub CheckPhysicalSamples(folder As String)
        Dim expected As String() = {
            "sale_cash.sample.json", "sale_split_edc.sample.json", "sale_kasbon_dp0.sample.json",
            "receivable_selected.sample.json", "receivable_selected_card.sample.json",
            "receivable_proof.sample.json", "return_note.sample.json"}
        Dim files As String() = IO.Directory.GetFiles(folder, "*.sample.json")
        If files.Length <> expected.Length OrElse
           Not expected.All(Function(name) files.Any(Function(path) IO.Path.GetFileName(path) = name)) Then
            Throw New InvalidOperationException("Keluarga fixture uji kertas v2 tidak lengkap.")
        End If
        Dim characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        For Each path As String In files
            Dim body As String = IO.File.ReadAllText(path)
            Dim kind As String = CStr(JObject.Parse(body)("jobType"))
            Select Case SelectV2Family(kind)
                Case "SALE"
                    Dim root As JObject = ParseSaleV2(body)
                    Dim payload As JObject = CType(root("payload"), JObject)
                    For Each row As SaleAmountRow In BuildSaleAmountRows(kind,
                        CStr(payload("paymentMethod")), CStr(payload("noncashMethod")), CType(payload("amounts"), JObject))
                        LayoutSaleAmountLine(row.Caption, row.Sen, 40, 40.0F, characters)
                    Next
                Case "RECEIVABLE"
                    BuildReceivableV2Plan(ParseReceivableV2(body), 40.0F, 20.0F, characters, characters)
                Case "RETURN"
                    BuildReturnV2CorePlan(ParseReturnV2(body), 40.0F, characters)
                Case Else
                    Throw New InvalidOperationException("Keluarga fixture uji kertas tidak dikenal: " & kind)
            End Select
        Next
    End Sub

    Private Sub CheckReturnV2(path As String)
        Dim source As String = IO.File.ReadAllText(path)
        If RoutePrintSchema(source, 1) <> "UNSUPPORTED_SCHEMA" OrElse
           RoutePrintSchema(source, 2) <> "V2" OrElse
           SelectV2Family("return_note") <> "RETURN" Then
            Throw New InvalidOperationException("Retur v2 melewati pagar schema aktif atau salah keluarga.")
        End If
        Dim original As JObject = ParseReturnV2(source)
        Dim payload As JObject = CType(original("payload"), JObject)
        If CStr(payload("totalSen")) <> "252" OrElse
           CStr(payload("items")(0)("totalSen")) <> "152" Then
            Throw New InvalidOperationException("Jumlah retur pecahan salah.")
        End If
        Dim characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        Dim plan As ReturnV2CorePlan = BuildReturnV2CorePlan(original, 40.0F, characters)
        If plan.Title <> "NOTA KEMBALI BARANG" OrElse plan.CustomerLines.Count <> 3 OrElse
           plan.ReceiptLine.IndexOf("26090001", StringComparison.Ordinal) < 0 OrElse
           plan.ReprintLine IsNot Nothing OrElse plan.ItemLines.Count <> 2 OrElse
           plan.ItemLines(0)(0) <> "1,5 BARANG A" OrElse
           Not plan.ItemLines(0).Last().EndsWith("1,52", StringComparison.Ordinal) OrElse
           Not plan.TotalLine.EndsWith("2,52", StringComparison.Ordinal) OrElse
           plan.OriginalName.Count <> 1 OrElse plan.OriginalName(0).Text <> "KASIR ASAL" OrElse
           plan.OriginalName(0).X <> 25.0F OrElse plan.ReprintName.Count <> 0 Then
            Throw New InvalidOperationException("Layout inti retur asli tidak cocok.")
        End If
        Dim reprint As JObject = CType(original.DeepClone(), JObject)
        reprint("payload")("reprint") = JObject.Parse("{""date"":""2026-09-29"",""time"":""09:30:00"",""processor"":{""userId"":""43"",""name"":""KASIR ULANG""}}")
        ParseReturnV2(reprint.ToString(Formatting.None))
        Dim reprintPlan As ReturnV2CorePlan = BuildReturnV2CorePlan(reprint, 40.0F, characters)
        If reprintPlan.ReprintLine Is Nothing OrElse
           Not reprintPlan.ReprintLine.StartsWith("CETAK ULANG:", StringComparison.Ordinal) OrElse
           reprintPlan.TotalLine <> plan.TotalLine OrElse
           reprintPlan.OriginalName(0).Text <> "KASIR ASAL" OrElse
           reprintPlan.ReprintName.Count <> 1 OrElse
           reprintPlan.ReprintName(0).Text <> "Dicetak ulang oleh: KASIR ULANG" OrElse
           reprintPlan.ReprintName(0).X <> 40.0F - reprintPlan.ReprintName(0).Text.Length Then
            Throw New InvalidOperationException("Layout salinan retur mengubah nilai asal.")
        End If
        Dim longName As JObject = CType(reprint.DeepClone(), JObject)
        longName("payload")("originalProcessor")("name") = "NAMA PEMROSES ASAL YANG SANGAT PANJANG SEKALI"
        longName("payload")("reprint")("processor")("name") = "NAMA PEMROSES CETAK ULANG YANG SANGAT PANJANG"
        Dim longPlan As ReturnV2CorePlan = BuildReturnV2CorePlan(longName, 40.0F, characters)
        If longPlan.OriginalName.Count < 2 OrElse longPlan.ReprintName.Count < 2 OrElse
           longPlan.OriginalName.Any(Function(line) line.X < 0.0F OrElse line.X + line.Text.Length > 40.0F) OrElse
           longPlan.ReprintName.Any(Function(line) line.X < 0.0F OrElse line.X + line.Text.Length > 40.0F) Then
            Throw New InvalidOperationException("Nama panjang retur melampaui area cetak.")
        End If
        For Each action As Action In {
            Sub() BuildReturnV2CorePlan(original, 39.0F, characters),
            Sub() BuildReturnV2CorePlan(original, Single.NaN, characters),
            Sub() BuildReturnV2CorePlan(original, 40.0F, Nothing)}
            Dim rejected As Boolean = False
            Try
                action()
            Catch ex As ArgumentException
                rejected = True
            End Try
            If Not rejected Then Throw New InvalidOperationException("Layout retur menerima area/metrik cacat.")
        Next
        Dim mutations As (Name As String, Edit As Action(Of JObject))() = {
            ("dua salinan", Sub(j) j("copies") = 2),
            ("schema lama", Sub(j) j("schemaVersion") = 1),
            ("header toko palsu", Sub(j) j("store")("name") = "TOKO KINI"),
            ("transaksi palsu", Sub(j) j("payload")("transactionId") = "xxxxxxxxxxxxxxxx"),
            ("nama kosong", Sub(j) j("payload")("items")(0)("name") = vbTab & vbCrLf),
            ("kasir kosong", Sub(j) j("payload")("originalProcessor")("name") = ChrW(&H200B)),
            ("harga angka JSON", Sub(j) j("payload")("items")(0)("priceSen") = 101),
            ("baris tidak cocok", Sub(j) j("payload")("items")(0)("totalSen") = "200"),
            ("total tidak cocok", Sub(j) j("payload")("totalSen") = "251"),
            ("tanggal mustahil", Sub(j) j("payload")("date") = "2026-02-30"),
            ("jam mustahil", Sub(j) j("payload")("time") = "24:00:00"),
            ("ID aktor bukan angka", Sub(j) j("payload")("originalProcessor")("userId") = "42x")
        }
        For Each scenario In mutations
            Dim changed As JObject = CType(original.DeepClone(), JObject)
            scenario.Edit(changed)
            Dim rejected As Boolean = False
            Try
                ParseReturnV2(changed.ToString(Formatting.None))
            Catch ex As Exception
                rejected = True
            End Try
            If Not rejected Then Throw New InvalidOperationException("Retur v2 menerima " & scenario.Name)
        Next
        Dim duplicate As String = source.Replace("""jobId"": ""return-1""", """jobId"": ""return-1"", ""jobId"": ""return-2""")
        If duplicate = source Then Throw New InvalidOperationException("Fixture tidak memuat jobId kanonik.")
        Dim duplicateRejected As Boolean = False
        Try
            ParseReturnV2(duplicate)
        Catch ex As Exception
            duplicateRejected = True
        End Try
        If Not duplicateRejected Then Throw New InvalidOperationException("Retur v2 menerima kunci JSON duplikat.")
    End Sub

    Private Sub CheckGoSaleV2(folder As String)
        Dim files As String() = IO.Directory.GetFiles(folder, "*.json")
        If files.Length <> 1 OrElse IO.Path.GetFileName(files(0)) <> "sale_corrected_reprint.json" Then
            Throw New InvalidOperationException("Korpus Go salinan nota R1 tidak lengkap.")
        End If
        Dim root As JObject = ParseSaleV2(IO.File.ReadAllText(files(0)))
        Dim payload As JObject = CType(root("payload"), JObject)
        Dim reprint As JObject = CType(payload("reprint"), JObject)
        Dim corrections As JArray = CType(reprint("corrections"), JArray)
        Dim lines As List(Of String) = LayoutSaleCorrections(reprint, 40.0F,
            Function(value As String) CSng(value.Length))
        If CStr(root("jobType")) <> "cashier_receipt" OrElse
           CStr(payload("amounts")("netSen")) <> "1950000" OrElse
           corrections.Count <> 2 OrElse CStr(corrections(0)("customerId")) <> "CS9002" OrElse
           CStr(corrections(1)("paymentMethod")) <> "WIRE" OrElse
           Not CBool(corrections(1)("convertedToCredit")) OrElse
           Not lines.Any(Function(line) line.Contains("NOTA ASAL")) OrElse
           Not lines.Any(Function(line) line.Contains("KASBON")) OrElse
           lines.Any(Function(line) line.Length > 40) Then
            Throw New InvalidOperationException("Job Go salinan nota gagal diparse atau dicatat lengkap.")
        End If
        Console.WriteLine("Go→agent sale v2: corrected reprint parsed and preflighted.")
    End Sub

    Private Sub CheckGoReturnV2(folder As String)
        Dim expected As String() = {"return_committed.json", "return_fractional.json", "return_reprint.json"}
        Dim files As String() = IO.Directory.GetFiles(folder, "*.json")
        If files.Length <> expected.Length OrElse
           Not expected.All(Function(name) files.Any(Function(path) IO.Path.GetFileName(path) = name)) Then
            Throw New InvalidOperationException("Korpus Go retur v2 tidak lengkap.")
        End If
        Dim characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        Dim jobs As New Dictionary(Of String, JObject)(StringComparer.Ordinal)
        For Each name As String In expected
            Dim root As JObject = ParseReturnV2(IO.File.ReadAllText(IO.Path.Combine(folder, name)))
            Dim plan As ReturnV2CorePlan = BuildReturnV2CorePlan(root, 40.0F, characters)
            If CStr(root("jobType")) <> "return_note" OrElse CStr(root("printerRole")) <> "CASHIER" OrElse
               plan.Title <> "NOTA KEMBALI BARANG" OrElse plan.ItemLines.Count = 0 OrElse
               plan.TotalLine.Length > 40 OrElse SelectV2Family("return_note") <> "RETURN" Then
                Throw New InvalidOperationException("Job Go retur belum aman untuk layout: " & name)
            End If
            jobs.Add(name, root)
        Next
        Dim fractional As JObject = CType(jobs("return_fractional.json")("payload"), JObject)
        If CStr(fractional("totalSen")) <> "252" OrElse
           CStr(fractional("items")(0)("totalSen")) <> "152" OrElse
           CStr(fractional("items")(1)("totalSen")) <> "100" Then
            Throw New InvalidOperationException("Residu sen produsen Go berubah di agent.")
        End If
        Dim committed As JObject = CType(jobs("return_committed.json")("payload"), JObject)
        Dim reprint As JObject = CType(jobs("return_reprint.json")("payload"), JObject)
        If CStr(committed("customer")("name")) <> "PIUTANG" OrElse
           CStr(committed("originalProcessor")("name")) <> "KASIR AWAL" OrElse
           committed.Property("reprint") IsNot Nothing OrElse
           CStr(reprint("reprint")("processor")("name")) <> "Kasir Ulang" OrElse
           CStr(reprint("reprint")("time")) <> "09:03:04" OrElse
           CStr(committed("transactionId")) <> CStr(reprint("transactionId")) OrElse
           CStr(committed("receiptNo")) <> CStr(reprint("receiptNo")) OrElse
           CStr(committed("totalSen")) <> CStr(reprint("totalSen")) OrElse
           Not JToken.DeepEquals(committed("items"), reprint("items")) OrElse
           Not JToken.DeepEquals(committed("originalProcessor"), reprint("originalProcessor")) Then
            Throw New InvalidOperationException("Salinan Go→agent mengubah bukti retur asal.")
        End If
        Console.WriteLine("Go→agent return v2: 3 synthetic jobs parsed and preflighted; printer still disabled.")
    End Sub

    Private Sub CheckGoReceivableV2(folder As String)
        Dim scenarios As (FileName As String, JobType As String)() = {
            ("selected_cash.json", "receivable_selected"),
            ("selected_wire.json", "receivable_selected"),
            ("selected_edc.json", "receivable_selected_card"),
            ("selected_reprint.json", "receivable_selected"),
            ("selected_zero.json", "receivable_selected"),
            ("fifo_cash.json", "receivable_proof"),
            ("fifo_wire_credit.json", "receivable_proof"),
            ("fifo_edc.json", "receivable_proof"),
            ("fifo_fee_exceeds_payment.json", "receivable_proof")}
        If IO.Directory.GetFiles(folder, "*.json").Length <> scenarios.Length Then
            Throw New InvalidOperationException("Jumlah fixture Go bukti piutang tidak lengkap.")
        End If
        Dim characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        For Each scenario In scenarios
            Dim root As JObject = ParseReceivableV2(IO.File.ReadAllText(IO.Path.Combine(folder, scenario.FileName)))
            If CStr(root("jobType")) <> scenario.JobType OrElse
               CStr(root("store")("name")) <> "SYNTHETIC STORE" OrElse
               CStr(root("payload")("customer")("id")) <> "CS9001" Then
                Throw New InvalidOperationException("Fixture Go bukti piutang tidak cocok: " & scenario.FileName)
            End If
            Dim plan As ReceivableV2PrintPlan = BuildReceivableV2Plan(root, 40.0F, 20.0F, characters, characters)
            If plan.AllocationLines.Count = 0 OrElse plan.AmountLines.Count = 0 Then
                Throw New InvalidOperationException("Layout fixture Go kosong: " & scenario.FileName)
            End If
            If scenario.FileName = "selected_reprint.json" AndAlso
               (plan.ReprintName.Count = 0 OrElse plan.OriginalName.Count = 0) Then
                Throw New InvalidOperationException("Atribusi cetak ulang fixture Go hilang.")
            End If
            If scenario.FileName = "selected_zero.json" AndAlso
               CStr(root("payload")("amounts")("netPaymentSen")) <> "0" Then
                Throw New InvalidOperationException("Selected nol fixture Go berubah.")
            End If
            If scenario.FileName = "fifo_fee_exceeds_payment.json" AndAlso
               Not CStr(root("payload")("amounts")("merchantReceivesSen")).StartsWith("-", StringComparison.Ordinal) Then
                Throw New InvalidOperationException("Merchant net negatif fixture Go hilang.")
            End If
        Next
        Console.WriteLine("Go→agent receivable v2: 9 committed synthetic jobs parsed and preflighted.")
    End Sub

    Private Sub CheckReceivableV2(folder As String)
        Dim selectedSource As String = IO.File.ReadAllText(IO.Path.Combine(folder, "receivable_selected.sample.json"))
        Dim fifoSource As String = IO.File.ReadAllText(IO.Path.Combine(folder, "receivable_proof.sample.json"))
        If RoutePrintSchema(selectedSource, 1) <> "UNSUPPORTED_SCHEMA" OrElse
           RoutePrintSchema(selectedSource, 2) <> "V2" Then
            Throw New InvalidOperationException("Bukti piutang v2 melewati gerbang schema1.")
        End If
        Dim selected As JObject = JObject.Parse(selectedSource)
        Dim fifo As JObject = JObject.Parse(fifoSource)
        AcceptProof("selected dengan kredit", selected)
        AcceptProof("FIFO transfer", fifo)
        CheckProofRows("selected kredit dan diskon", selected,
            "BON 101=-100|BON 102=1100", "TOTAL BON=1000|DISKON (-)=100|-TOTAL BAYAR=900|UANG DITERIMA=1000|KEMBALIAN=100")
        CheckProofRows("FIFO fee ditanggung toko", fifo,
            "BON 101=400", "TOTAL BON=2000|BAYAR=400|-SISA BON=1600")
        CheckReceivableLayout(selected, fifo)

        Dim card As JObject = CType(selected.DeepClone(), JObject)
        card("jobType") = "receivable_selected_card"
        card("payload")("paymentMethod") = "EDC"
        Dim cardAmounts As JObject = CType(card("payload")("amounts"), JObject)
        cardAmounts("cashSen") = "0"
        cardAmounts("noncashSen") = "900"
        cardAmounts("customerFeeSen") = "20"
        cardAmounts("merchantFeeSen") = "5"
        cardAmounts("customerPaysSen") = "920"
        cardAmounts("merchantReceivesSen") = "915"
        cardAmounts("tenderSen") = "0"
        cardAmounts("changeSen") = "0"
        AcceptProof("selected EDC", card)
        CheckProofRows("selected EDC", card,
            "BON 101=-100|BON 102=1100", "TOTAL BON=1000|DISKON (-)=100|-TOTAL BAYAR=900|BIAYA EDC=20|-TOTAL DITAGIH=920")

        Dim selectedWire As JObject = CType(selected.DeepClone(), JObject)
        selectedWire("payload")("paymentMethod") = "WIRE"
        Dim wireAmounts As JObject = CType(selectedWire("payload")("amounts"), JObject)
        wireAmounts("roundingSen") = "25"
        wireAmounts("transferFeeSen") = "25"
        wireAmounts("stampFeeSen") = "50"
        wireAmounts("netPaymentSen") = "800"
        wireAmounts("cashSen") = "0"
        wireAmounts("noncashSen") = "800"
        wireAmounts("customerPaysSen") = "800"
        wireAmounts("merchantReceivesSen") = "800"
        wireAmounts("tenderSen") = "0"
        wireAmounts("changeSen") = "0"
        selectedWire("payload")("allocations")(1)("roundingSen") = "25"
        selectedWire("payload")("allocations")(1)("transferFeeSen") = "25"
        selectedWire("payload")("allocations")(1)("stampFeeSen") = "50"
        CheckProofRows("selected transfer semua pengurang", selectedWire,
            "BON 101=-100|BON 102=1100", "TOTAL BON=1000|DISKON (-)=100|PEMBULATAN (-)=25|BIAYA TF (-)=25|MATERAI (-)=50|-TOTAL BAYAR=800")

        Dim zero As JObject = CType(selected.DeepClone(), JObject)
        zero("payload")("paymentMethod") = "WIRE"
        zero("payload")("allocations")(1)("discountSen") = "1000"
        Dim zeroAmounts As JObject = CType(zero("payload")("amounts"), JObject)
        zeroAmounts("discountSen") = "1000"
        For Each name As String In {"netPaymentSen", "cashSen", "customerPaysSen", "merchantReceivesSen", "tenderSen", "changeSen"}
            zeroAmounts(name) = "0"
        Next
        AcceptProof("selected nol", zero)
        CheckProofRows("selected nol", zero,
            "BON 101=-100|BON 102=1100", "TOTAL BON=1000|DISKON (-)=1000|-TOTAL BAYAR=0")

        Dim reprint As JObject = CType(selected.DeepClone(), JObject)
        CType(reprint("payload"), JObject)("reprint") = JObject.Parse("{""date"":""2026-09-28"",""time"":""08:00"",""processor"":{""userId"":""9"",""name"":""KASIR ULANG""}}")
        AcceptProof("cetak ulang", reprint)
        If CStr(reprint("payload")("originalProcessor")("name")) <> "KASIR ASLI" OrElse
           CStr(reprint("payload")("reprint")("processor")("name")) <> "KASIR ULANG" Then
            Throw New InvalidOperationException("Pemroses bukti asli tertimpa.")
        End If

        Dim expensive As JObject = CType(fifo.DeepClone(), JObject)
        expensive("payload")("amounts")("transferFeeSen") = "500"
        expensive("payload")("amounts")("merchantReceivesSen") = "-100"
        AcceptProof("FIFO fee lebih besar dari cicilan", expensive)
        CheckProofRows("FIFO fee lebih besar dari cicilan", expensive,
            "BON 101=400", "TOTAL BON=2000|BAYAR=400|-SISA BON=1600")

        Dim credit As JObject = CType(fifo.DeepClone(), JObject)
        Dim rows As JArray = CType(credit("payload")("allocations"), JArray)
        rows(0)("receiptNo") = "102"
        rows(0)("principalAppliedSen") = "500"
        rows(0)("afterSen") = "500"
        rows.Insert(0, JObject.Parse("{""receiptNo"":""101"",""beforeSen"":""-100"",""principalAppliedSen"":""-100"",""afterSen"":""0"",""discountSen"":""0"",""roundingSen"":""0"",""transferFeeSen"":""0"",""stampFeeSen"":""0""}"))
        AcceptProof("FIFO bon kredit", credit)

        RejectProof("schema v1", selectedSource.Replace("""schemaVersion"": 2", """schemaVersion"": 1"))
        RejectProof("schema duplikat", selectedSource.Replace("""schemaVersion"": 2", """schemaVersion"": 2,""schemaVersion"":1"))
        RejectProof("dua salinan", selectedSource.Replace("""copies"": 1", """copies"": 2"))
        RejectProof("saldo bon tidak konservatif", selectedSource.Replace("""principalAppliedSen"":""1100""", """principalAppliedSen"":""1099"""))
        RejectProof("kas tak cocok", selectedSource.Replace("""netPaymentSen"":""900""", """netPaymentSen"":""901"""))
        RejectProof("bon duplikat", selectedSource.Replace("""receiptNo"":""102""", """receiptNo"":""101"""))
        RejectProof("angka negatif nol", selectedSource.Replace("""beforeSen"":""-100""", """beforeSen"":""-0"""))
        RejectProof("field asing", selectedSource.Replace("""copies"": 1", """copies"": 1,""secret"":1"))
        RejectProof("EDC di job salah", selectedSource.Replace("""paymentMethod"": ""CASH""", """paymentMethod"": ""EDC"""))
        RejectProof("fee FIFO pada bon", fifoSource.Replace("""transferFeeSen"":""0""", """transferFeeSen"":""20"""))
        RejectProof("penerimaan FIFO salah", fifoSource.Replace("""merchantReceivesSen"":""380""", """merchantReceivesSen"":""400"""))
        Dim invisible As JObject = CType(selected.DeepClone(), JObject)
        invisible("payload")("originalProcessor")("name") = " " & vbTab
        RejectProof("pemroses tidak terlihat", invisible.ToString(Formatting.None))
        Dim missingStore As JObject = CType(selected.DeepClone(), JObject)
        CType(missingStore("store"), JObject).Remove("name")
        RejectProof("header historis hilang", missingStore.ToString(Formatting.None))
        If FormatReceivableSen(-100L) <> "-1" OrElse
           FormatReceivableSen(252L) <> "2,52" OrElse
           FormatReceivableSen(99999999999999L) <> "999.999.999.999,99" Then
            Throw New InvalidOperationException("Format sen bertanda bukti piutang salah.")
        End If
        Try
            FormatReceivableSen(Long.MinValue)
            Throw New InvalidOperationException("Sen di luar kontrak diterima.")
        Catch ex As ArgumentOutOfRangeException
            ' Ditolak sebelum Math.Abs agar tidak overflow.
        End Try
    End Sub

    Private Sub CheckProofRows(name As String, job As JObject, expectedAllocations As String,
                               expectedAmounts As String)
        Dim valid As JObject = ParseReceivableV2(job.ToString(Formatting.None))
        Dim payload As JObject = CType(valid("payload"), JObject)
        Dim actualAllocations As String = String.Join("|", BuildReceivableAllocationRows(payload).
            Select(Function(row) row.Caption & "=" & row.Sen.ToString(CultureInfo.InvariantCulture)))
        Dim actualAmounts As String = String.Join("|", BuildReceivableAmountRows(CStr(valid("jobType")),
            CStr(payload("paymentMethod")), CType(payload("amounts"), JObject)).
            Select(Function(row) If(row.SeparatorBefore, "-", "") & row.Caption & "=" & row.Sen.ToString(CultureInfo.InvariantCulture)))
        If actualAllocations <> expectedAllocations OrElse actualAmounts <> expectedAmounts Then
            Throw New InvalidOperationException(name & " baris salah: " & actualAllocations & " / " & actualAmounts)
        End If
    End Sub

    Private Sub CheckReceivableLayout(selected As JObject, fifo As JObject)
        Dim characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        Dim original As ReceivableV2PrintPlan = BuildReceivableV2Plan(
            ParseReceivableV2(selected.ToString(Formatting.None)), 40.0F, 20.0F, characters, characters)
        If original.StoreName.Count <> 1 OrElse original.StoreDetails.Count <> 2 OrElse
           original.Title.Count <> 1 OrElse original.Metadata.Count < 6 OrElse
           Not original.Metadata.Any(Function(line) line.Contains("PELUNASAN BON")) OrElse
           Not original.Metadata.Any(Function(line) line.Contains("IN/260927/000001")) OrElse
           Not original.Metadata.Any(Function(line) line.Contains("PELANGGAN")) OrElse
           original.AllocationLines.Count <> 2 OrElse
           Not original.AllocationLines(0).EndsWith("-1", StringComparison.Ordinal) OrElse
           original.OriginalName.Count <> 1 OrElse original.OriginalName(0).Text <> "KASIR ASLI" OrElse
           original.ReprintName.Count <> 0 Then
            Throw New InvalidOperationException("Preflight bukti selected kehilangan identitas atau kredit bon.")
        End If
        Dim installment As ReceivableV2PrintPlan = BuildReceivableV2Plan(
            ParseReceivableV2(fifo.ToString(Formatting.None)), 40.0F, 20.0F, characters, characters)
        If installment.AllocationLines.Count <> 1 OrElse
           Not installment.Metadata.Any(Function(line) line.Contains("CICILAN BON")) OrElse
           Not installment.AmountLines.Any(Function(line) line.Contains("SISA BON")) OrElse
           installment.AmountLines.Any(Function(line) line.Contains("BIAYA TF")) Then
            Throw New InvalidOperationException("Biaya transfer merchant FIFO bocor sebagai tagihan pelanggan.")
        End If
        Dim reprint As JObject = CType(selected.DeepClone(), JObject)
        CType(reprint("payload"), JObject)("reprint") = JObject.Parse("{""date"":""2026-09-28"",""time"":""08:00"",""processor"":{""userId"":""9"",""name"":""KASIR ULANG""}}")
        Dim again As ReceivableV2PrintPlan = BuildReceivableV2Plan(
            ParseReceivableV2(reprint.ToString(Formatting.None)), 40.0F, 20.0F, characters, characters)
        If again.OriginalName(0).Text <> "KASIR ASLI" OrElse again.ReprintName.Count = 0 OrElse
           Not again.ReprintName.Any(Function(line) line.Text.Contains("KASIR ULANG")) OrElse
           Not again.Metadata.Any(Function(line) line.Contains("CETAK ULANG")) Then
            Throw New InvalidOperationException("Cetak ulang menimpa pemroses asal.")
        End If
        Dim longNames As JObject = CType(reprint.DeepClone(), JObject)
        longNames("payload")("originalProcessor")("name") = New String("X"c, 60)
        longNames("payload")("reprint")("processor")("name") = New String("Y"c, 60)
        Dim wrappedNames As ReceivableV2PrintPlan = BuildReceivableV2Plan(
            ParseReceivableV2(longNames.ToString(Formatting.None)), 40.0F, 20.0F, characters, characters)
        If wrappedNames.OriginalName.Count < 2 OrElse wrappedNames.ReprintName.Count < 2 OrElse
           wrappedNames.OriginalName.Any(Function(line) line.X < 0.0F OrElse line.X + line.Text.Length > 40.0F) OrElse
           wrappedNames.ReprintName.Any(Function(line) line.X < 0.0F OrElse line.X + line.Text.Length > 40.0F) Then
            Throw New InvalidOperationException("Nama kasir panjang melewati area cetak.")
        End If
        Dim longCustomer As JObject = CType(selected.DeepClone(), JObject)
        longCustomer("payload")("customer")("name") = "PELANGGAN TOKO BANGUNAN DENGAN NAMA SANGAT PANJANG DAN BERULANG"
        Dim wrapped As ReceivableV2PrintPlan = BuildReceivableV2Plan(
            ParseReceivableV2(longCustomer.ToString(Formatting.None)), 40.0F, 20.0F, characters, characters)
        If wrapped.Metadata.Count <= original.Metadata.Count Then
            Throw New InvalidOperationException("Nama pelanggan panjang tidak dibungkus.")
        End If
        Try
            BuildReceivableV2Plan(ParseReceivableV2(selected.ToString(Formatting.None)),
                                  25.0F, 20.0F, characters, characters)
            Throw New InvalidOperationException("Area cetak sempit diterima sebelum printer.")
        Catch ex As ArgumentException
            ' Preflight menolak sebelum ada Printer.Print.
        End Try
        Try
            LayoutReceivableAmountLine("BON 101", -100L, 40.0F, Function(value As String) Single.NaN)
            Throw New InvalidOperationException("Metrik printer tidak sah diterima.")
        Catch ex As ArgumentException
            ' Lebar NaN bukan izin cetak.
        End Try
    End Sub

    Private Sub AcceptProof(name As String, job As JObject)
        Try
            ParseReceivableV2(job.ToString(Formatting.None))
        Catch ex As Exception
            Throw New InvalidOperationException(name & " ditolak: " & ex.Message, ex)
        End Try
    End Sub

    Private Sub RejectProof(name As String, body As String)
        Try
            ParseReceivableV2(body)
        Catch
            Return
        End Try
        Throw New InvalidOperationException(name & " diterima padahal tidak sah.")
    End Sub

    Private Sub CheckSaleV2(path As String)
        Dim source As String = IO.File.ReadAllText(path)
        ParseSaleV2(source)
        Dim baseline As JObject = JObject.Parse(source)
        Accept("cash", baseline)
        CheckAmountRows("cash rounding", baseline,
            "TOTAL BELANJA=1975200|PEMBULATAN (-)=25200|-TOTAL NOTA=1950000|UANG DITERIMA=2000000|KEMBALIAN=50000")

        Dim discounted As JObject = CType(baseline.DeepClone(), JObject)
        Dim discountedAmounts As JObject = CType(discounted("payload")("amounts"), JObject)
        discountedAmounts("discountSen") = "10000"
        For Each name As String In {"netSen", "principalAppliedSen", "cashSen", "customerPaysSen", "merchantReceivesSen"}
            discountedAmounts(name) = "1940000"
        Next
        discountedAmounts("changeSen") = "60000"
        Accept("cash discount plus rounding", discounted)
        CheckAmountRows("cash discount plus rounding", discounted,
            "TOTAL BELANJA=1975200|DISKON (-)=10000|PEMBULATAN (-)=25200|-TOTAL NOTA=1940000|UANG DITERIMA=2000000|KEMBALIAN=60000")

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
        CheckAmountRows("edc fees", edc,
            "TOTAL BELANJA=1975200|PEMBULATAN (-)=25200|-TOTAL NOTA=1950000|BIAYA EDC=500|-TOTAL DIBAYAR=1950500")

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
        CheckAmountRows("split edc", split,
            "TOTAL BELANJA=1975200|PEMBULATAN (-)=25200|-TOTAL NOTA=1950000|BIAYA EDC=500|-TOTAL DIBAYAR=1950500|TUNAI=1000000|EDC=950000")

        Dim splitWire As JObject = CType(split.DeepClone(), JObject)
        payload = CType(splitWire("payload"), JObject)
        amounts = CType(payload("amounts"), JObject)
        payload("noncashMethod") = "WIRE"
        amounts("customerFeeSen") = "0"
        amounts("merchantFeeSen") = "0"
        amounts("customerPaysSen") = "1950000"
        amounts("merchantReceivesSen") = "1950000"
        Accept("split wire", splitWire)
        CheckAmountRows("split wire", splitWire,
            "TOTAL BELANJA=1975200|PEMBULATAN (-)=25200|-TOTAL NOTA=1950000|TUNAI=1000000|TRANSFER=950000")

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
        CheckAmountRows("DP0", credit,
            "TOTAL BELANJA=1975200|PEMBULATAN (-)=25200|-TOTAL NOTA=1950000|BAYAR=0|SISA UTANG=1950000")

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
        CheckAmountRows("DP positif", deposit,
            "TOTAL BELANJA=1975200|PEMBULATAN (-)=25200|-TOTAL NOTA=1950000|BAYAR=600000|SISA UTANG=1350000")

        Dim reprint As JObject = CType(baseline.DeepClone(), JObject)
        CType(reprint("payload"), JObject)("reprint") = JObject.Parse("{""date"":""2026-09-28"",""time"":""08:00"",""processor"":{""userId"":""3"",""name"":""Kasir Ulang""},""corrections"":[]}")
        Accept("reprint", reprint)
        Dim checkedReprint As JObject = ParseSaleV2(reprint.ToString(Formatting.None))
        If CStr(checkedReprint("payload")("originalProcessor")("name")) <> "Siti" OrElse
           CStr(checkedReprint("payload")("reprint")("processor")("name")) <> "Kasir Ulang" Then
            Throw New InvalidOperationException("Pemroses asli berubah saat cetak ulang.")
        End If
        If LayoutSaleCorrections(CType(checkedReprint("payload")("reprint"), JObject), 40.0F,
            Function(value As String) CSng(value.Length)).Count <> 0 Then
            Throw New InvalidOperationException("Salinan tanpa koreksi diberi catatan palsu.")
        End If

        Dim corrected As JObject = CType(reprint.DeepClone(), JObject)
        corrected("payload")("reprint")("corrections") = JArray.Parse("[{""kind"":""customer"",""eventId"":""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"",""date"":""2026-09-28"",""time"":""09:00:00"",""actorName"":""ADMIN SATU"",""customerId"":""CS9002"",""customerName"":""PELANGGAN BARU SANGAT PANJANG UNTUK DIBUNGKUS""},{""kind"":""payment"",""eventId"":""bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"",""date"":""2026-09-28"",""time"":""10:00:00"",""actorName"":""ADMIN DUA"",""paymentMethod"":""WIRE"",""convertedToCredit"":true}]")
        Accept("salinan dengan dua koreksi", corrected)
        Dim correctedLines As List(Of String) = LayoutSaleCorrections(CType(corrected("payload")("reprint"), JObject),
            40.0F, Function(value As String) CSng(value.Length))
        If correctedLines.Count < 8 OrElse
           Not correctedLines.Any(Function(line) line.Contains("NOTA ASAL")) OrElse
           Not correctedLines.Any(Function(line) line.Contains("CS9002")) OrElse
           Not correctedLines.Any(Function(line) line.Contains("KASBON")) OrElse
           Not correctedLines.Any(Function(line) line.Contains("TRANSFER")) OrElse
           Not correctedLines.Any(Function(line) line.Contains("LIHAT RIWAYAT")) OrElse
           correctedLines.Any(Function(line) line.Length > 40) Then
            Throw New InvalidOperationException("Catatan koreksi hilang atau melewati area cetak.")
        End If
        Try
            LayoutSaleCorrections(CType(corrected("payload")("reprint"), JObject), 8.0F,
                Function(value As String) CSng(value.Length))
            Throw New InvalidOperationException("Catatan koreksi lolos pada area cetak sempit.")
        Catch ex As ArgumentException
            ' Preflight menolak sebelum Printer.Print pertama.
        End Try
        Dim badOrder As JObject = CType(corrected.DeepClone(), JObject)
        Dim swapped As JArray = CType(badOrder("payload")("reprint")("corrections"), JArray)
        Dim first As JToken = swapped(0).DeepClone()
        swapped(0) = swapped(1).DeepClone()
        swapped(1) = first
        Reject("urutan koreksi terbalik", badOrder.ToString(Formatting.None))
        Dim missingCorrections As JObject = CType(reprint.DeepClone(), JObject)
        CType(missingCorrections("payload")("reprint"), JObject).Remove("corrections")
        Reject("salinan tanpa daftar koreksi", missingCorrections.ToString(Formatting.None))
        Dim invisibleCorrection As JObject = CType(corrected.DeepClone(), JObject)
        invisibleCorrection("payload")("reprint")("corrections")(0)("actorName") = vbTab & vbCrLf
        Reject("aktor koreksi tidak terlihat", invisibleCorrection.ToString(Formatting.None))
        Dim oversizedCorrection As JObject = CType(corrected.DeepClone(), JObject)
        oversizedCorrection("payload")("reprint")("corrections")(0)("actorName") = New String("A"c, 256)
        Reject("aktor koreksi terlalu panjang", oversizedCorrection.ToString(Formatting.None))
        Dim wrongDate As JObject = CType(corrected.DeepClone(), JObject)
        wrongDate("payload")("reprint")("corrections")(0)("date") = "2026/09/28"
        Reject("tanggal koreksi tidak sah", wrongDate.ToString(Formatting.None))
        Dim sentinelCustomer As JObject = CType(corrected.DeepClone(), JObject)
        sentinelCustomer("payload")("reprint")("corrections")(0)("customerId") = "CS0001"
        Reject("pelanggan sentinel sebagai koreksi", sentinelCustomer.ToString(Formatting.None))

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
        Dim invisibleActor As JObject = CType(baseline.DeepClone(), JObject)
        invisibleActor("payload")("originalProcessor")("name") = " " & vbTab & vbCrLf
        Reject("nama kasir tanpa karakter terlihat", invisibleActor.ToString(Formatting.None))
        Dim invisibleItem As JObject = CType(baseline.DeepClone(), JObject)
        invisibleItem("payload")("items")(0)("name") = vbTab & vbCrLf
        Reject("nama barang tanpa karakter terlihat", invisibleItem.ToString(Formatting.None))
        Dim invisibleReprint As JObject = CType(reprint.DeepClone(), JObject)
        invisibleReprint("payload")("reprint")("processor")("name") = " " & vbTab
        Reject("nama pencetak ulang tanpa karakter terlihat", invisibleReprint.ToString(Formatting.None))
        Dim formatOnly As JObject = CType(baseline.DeepClone(), JObject)
        formatOnly("payload")("originalProcessor")("name") = ChrW(&H200D)
        Reject("nama kasir karakter format saja", formatOnly.ToString(Formatting.None))
        Dim duplicateOriginal As JObject = CType(baseline.DeepClone(), JObject)
        duplicateOriginal("copies") = 2
        Reject("dua salinan nota asli", duplicateOriginal.ToString(Formatting.None))
        Dim duplicateReprint As JObject = CType(reprint.DeepClone(), JObject)
        duplicateReprint("copies") = 2
        Reject("dua salinan cetak ulang", duplicateReprint.ToString(Formatting.None))
    End Sub

    Private Sub CheckAmountRows(name As String, job As JObject, expected As String)
        Dim payload As JObject = CType(job("payload"), JObject)
        Dim rows = BuildSaleAmountRows(CStr(job("jobType")), CStr(payload("paymentMethod")),
                                       CStr(payload("noncashMethod")), CType(payload("amounts"), JObject))
        Dim actual As String = String.Join("|", rows.Select(Function(row) If(row.SeparatorBefore, "-", "") &
            row.Caption & "=" & row.Sen.ToString(CultureInfo.InvariantCulture)))
        If actual <> expected Then Throw New InvalidOperationException(name & ": baris jumlah berbeda: " & actual)
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

    Private Sub CheckNameLayout()
        Dim measure As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        If NormalizeProcessorName("  Siti" & vbTab & vbCrLf & "  Kasir  ") <> "Siti Kasir" Then
            Throw New InvalidOperationException("Spasi/control nama tidak dinormalisasi.")
        End If
        Dim centered = LayoutOriginalName("Siti", 30.0F, 40.0F, measure)
        If centered.Count <> 1 OrElse centered(0).X <> 28.0F Then Throw New InvalidOperationException("Nama pendek tidak berpusat pada HORMAT KAMI.")
        Dim right = LayoutOriginalName("ABCDEFGHIJKLMNOPQRSTUVWXY", 30.0F, 40.0F, measure)
        If right.Count <> 1 OrElse right(0).X <> 15.0F Then Throw New InvalidOperationException("Nama menengah tidak rata kanan.")
        Dim outside = LayoutOriginalName("Siti", 45.0F, 40.0F, measure)
        If outside.Count <> 1 OrElse outside(0).X <> 36.0F Then Throw New InvalidOperationException("Jangkar di luar area tidak fallback kanan.")
        Dim wrapped = LayoutOriginalName("SATU DUA TIGA EMPAT LIMA ENAM TUJUH DELAPAN SEMBILAN SEPULUH", 30.0F, 40.0F, measure)
        If wrapped.Count < 2 Then Throw New InvalidOperationException("Nama panjang tidak dibungkus.")
        For Each line In wrapped
            If line.X < 0.0F OrElse line.X + measure(line.Text) > 40.0F Then Throw New InvalidOperationException("Nama melewati area cetak.")
        Next
        Dim unicodeName As String = String.Concat(Enumerable.Repeat("😊", 30))
        Dim textElements As Func(Of String, Single) = Function(value As String) CSng(StringInfo.ParseCombiningCharacters(value).Length)
        Dim unicodeLines = LayoutOriginalName(unicodeName, 7.0F, 10.0F, textElements)
        If unicodeLines.Count <> 3 OrElse String.Concat(unicodeLines.Select(Function(line) line.Text)) <> unicodeName Then
            Throw New InvalidOperationException("Nama Unicode dipotong di tengah karakter.")
        End If
        Dim reprint = LayoutReprintName("Kasir Ulang Dengan Nama Yang Sangat Panjang", 20.0F, measure)
        If reprint.Count < 2 OrElse Not reprint(0).Text.StartsWith("Dicetak", StringComparison.Ordinal) Then
            Throw New InvalidOperationException("Penanda cetak ulang tidak dibungkus.")
        End If
        For Each line In reprint
            If line.X < 0.0F OrElse line.X + measure(line.Text) > 20.0F Then Throw New InvalidOperationException("Nama pencetak ulang melewati area.")
        Next
    End Sub

    Private Sub CheckMoneyFormat()
        For Each scenario In New (Value As Long, Expected As String)() {
            (0L, "0"), (1L, "0,01"), (25200L, "252"), (1975200L, "19.752"),
            (99999999999999L, "999.999.999.999,99")}
            If FormatSaleSen(scenario.Value) <> scenario.Expected Then
                Throw New InvalidOperationException("Format uang " & scenario.Value & " tidak cocok.")
            End If
        Next
        For Each scenario In New (Value As Long, Expected As String)() {
            (50L, "0,5"), (100L, "1"), (125L, "1,25")}
            If FormatSaleQuantity(scenario.Value) <> scenario.Expected Then
                Throw New InvalidOperationException("Format kuantitas " & scenario.Value & " tidak cocok.")
            End If
        Next
    End Sub

    Private Sub CheckItemLayout()
        Dim characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        Dim ordinary = BuildSaleItemLines(100L, " SAK" & vbTab, "Semen  Putih" & vbCrLf & "Kualitas Tinggi",
                                          1975200L, 1975200L, 40.0F, 40, characters)
        If ordinary(0) <> "1 SAK Semen Putih Kualitas Tinggi" OrElse
           ordinary(ordinary.Count - 1).Length <> 40 Then
            Throw New InvalidOperationException("Item biasa tidak dinormalisasi/diratakan.")
        End If
        Dim wrapped = BuildSaleItemLines(100L, "SAK", "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                                         1000L, 1000L, 12.0F, 12, characters)
        Dim restored As String = ""
        For Each line As String In wrapped.Take(wrapped.Count - 1)
            If line.Length > 12 OrElse Not line.StartsWith("1 SAK ", StringComparison.Ordinal) AndAlso
               Not line.StartsWith("      ", StringComparison.Ordinal) Then
                Throw New InvalidOperationException("Item panjang melewati area/indentasi.")
            End If
            restored &= line.Substring(6)
        Next
        If restored <> "ABCDEFGHIJKLMNOPQRSTUVWXYZ" Then Throw New InvalidOperationException("Nama item terpotong saat bungkus.")

        Dim graphemes As Func(Of String, Single) = Function(value As String) CSng(StringInfo.ParseCombiningCharacters(value).Length)
        Dim unicodeName As String = String.Concat(Enumerable.Repeat("😊", 20))
        Dim unicodeLines = BuildSaleItemLines(100L, "", unicodeName, 1000L, 1000L, 10.0F, 10, graphemes)
        Dim unicodeRestored As String = ""
        For Each line As String In unicodeLines.Take(unicodeLines.Count - 1)
            If graphemes(line) > 10.0F Then Throw New InvalidOperationException("Item Unicode melewati area.")
            unicodeRestored &= line.Substring(2)
        Next
        If unicodeRestored <> unicodeName Then Throw New InvalidOperationException("Grapheme item terpotong.")
        Dim highAmount = BuildSaleItemLines(100L, "SAK", "A", 99999999999999L, 99999999999999L,
                                            40.0F, 40, characters)
        If highAmount.Count <> 3 OrElse highAmount(1).Length > 40 OrElse highAmount(2).Length > 40 Then
            Throw New InvalidOperationException("Harga dan total besar tidak dipisah aman.")
        End If
        For Each action As Action In {
            Sub() BuildSaleItemLines(100L, "UNIT PANJANG", "Barang", 1000L, 1000L, 4.0F, 4, characters),
            Sub() BuildSaleItemLines(100L, "", vbTab & vbCrLf, 1000L, 1000L, 40.0F, 40, characters),
            Sub() BuildSaleItemLines(100L, "", "Barang", 1000L, 1000L, Single.NaN, 40, characters),
            Sub() BuildSaleItemLines(100L, "", "Barang", 1000L, 1000L, 40.0F, 40, Nothing)}
            Dim rejected As Boolean = False
            Try
                action()
            Catch ex As ArgumentException
                rejected = True
            End Try
            If Not rejected Then Throw New InvalidOperationException("Item yang tidak muat/metrik rusak diterima.")
        Next
    End Sub

    Private Sub CheckMetadataLayout()
        Dim characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)
        Dim exact = LayoutCenteredSaleText("ABCDEFGHIJKLMNOPQRST", "NO NAME", 20.0F, characters)
        If exact.Count <> 1 OrElse exact(0).Text <> "ABCDEFGHIJKLMNOPQRST" OrElse exact(0).X <> 0.0F Then
            Throw New InvalidOperationException("Nama toko tepat 20 kolom berubah/menambah baris kosong.")
        End If
        Dim wrapped = LayoutCenteredSaleText("ABCDEFGHIJKLMNOPQRSTU", "NO NAME", 20.0F, characters)
        If wrapped.Count <> 2 OrElse String.Concat(wrapped.Select(Function(line) line.Text)) <> "ABCDEFGHIJKLMNOPQRSTU" OrElse
           wrapped.Any(Function(line) line.X < 0.0F OrElse line.X + characters(line.Text) > 20.0F) Then
            Throw New InvalidOperationException("Nama toko panjang terpotong/melewati area.")
        End If
        Dim fullAddress = LayoutCenteredSaleText(New String("A"c, 40), "", 40.0F, characters)
        If fullAddress.Count <> 1 OrElse fullAddress(0).Text.Length <> 40 Then
            Throw New InvalidOperationException("Alamat tepat 40 kolom menambah baris kosong.")
        End If
        If LayoutCenteredSaleText("", "", 40.0F, characters).Count <> 0 Then
            Throw New InvalidOperationException("Alamat kosong menambah NO NAME.")
        End If
        Dim customer = LayoutSaleCustomer("ALAMAT   : ", "  Jalan" & vbTab & "Panjang Sekali  ", 20.0F, characters)
        If customer.Count < 2 OrElse customer(0) <> "ALAMAT   : Jalan" OrElse
           customer.Any(Function(line) line.Length > 20) Then
            Throw New InvalidOperationException("Alamat pelanggan tidak dibungkus aman.")
        End If
        Dim graphemes As Func(Of String, Single) = Function(value As String) CSng(StringInfo.ParseCombiningCharacters(value).Length)
        Dim unicodeName As String = String.Concat(Enumerable.Repeat("😊", 12))
        Dim unicodeLines = LayoutCenteredSaleText(unicodeName, "", 5.0F, graphemes)
        If String.Concat(unicodeLines.Select(Function(line) line.Text)) <> unicodeName OrElse
           unicodeLines.Any(Function(line) line.X < 0.0F OrElse line.X + graphemes(line.Text) > 5.0F) Then
            Throw New InvalidOperationException("Header Unicode dipotong di tengah grapheme.")
        End If
        Dim receipt As String = LayoutSaleReceiptLine("26092701", "2026-09-27", "14:30:00", False, 40.0F, characters)
        Dim reprint As String = LayoutSaleReceiptLine("", "2026-09-28", "08:00:00", True, 40.0F, characters)
        If receipt.Length <> 40 OrElse Not receipt.StartsWith("NO:26092701", StringComparison.Ordinal) OrElse
           Not receipt.EndsWith("14:30:00", StringComparison.Ordinal) OrElse
           Not reprint.StartsWith("CETAK ULANG:", StringComparison.Ordinal) OrElse reprint.Length <> 40 Then
            Throw New InvalidOperationException("Nomor/tanggal/jam nota tidak sesuai kolom.")
        End If
        Dim amount As String = LayoutSaleAmountLine("TOTAL BELANJA", 99999999999999L, 40, 40.0F, characters)
        If amount.Length <> 40 OrElse Not amount.EndsWith("999.999.999.999,99", StringComparison.Ordinal) Then
            Throw New InvalidOperationException("Jumlah besar tidak rata kanan.")
        End If
        For Each action As Action In {
            Sub() LayoutSaleReceiptLine("1234567890123456", "2026-09-27", "14:30:00", False, 40.0F, characters),
            Sub() LayoutSaleReceiptLine("1", "2026-09-27" & vbCrLf, "14:30:00", False, 40.0F, characters),
            Sub() LayoutSaleAmountLine("TOTAL BELANJA", 99999999999999L, 30, 30.0F, characters),
            Sub() LayoutSaleCustomer("ALAMAT   : ", "A", 3.0F, characters),
            Sub() LayoutCenteredSaleText("A", "", Single.NaN, characters)}
            Dim rejected As Boolean = False
            Try
                action()
            Catch ex As ArgumentException
                rejected = True
            End Try
            If Not rejected Then Throw New InvalidOperationException("Preflight metadata membiarkan masukan tak muat.")
        Next
    End Sub
End Module
