Public Class FrmMain
    Declare Auto Function SendMessage Lib "user32.dll" (ByVal hWnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As IntPtr
    Private clsFF3x_API As New FF3x_API.FF3x_API
    Dim bytResponse() As Byte = Nothing
    Dim outfile As System.IO.StreamWriter
    Private Sub FrmMain_Load(sender As Object, e As EventArgs) Handles Me.Load
        Label1.Font = New Font("Arial", Label1.Height * 0.6)
        InitFF()
    End Sub
    Private Function ConvertPinName2PinBlock(ByVal strPin As String) As Byte
        Dim bytPin() As Byte = System.Text.Encoding.ASCII.GetBytes(strPin)
        Return CByte(bytPin(0) - &H37)
    End Function
    Private Function ConvertPinName2PinNumber(ByVal strPin As String) As Byte
        Dim bytPin() As Byte = System.Text.Encoding.ASCII.GetBytes(strPin)
        If bytPin.Length = 3 Then   ' two-digits pin number
            Return CByte((bytPin(1) - 39) + (bytPin(2) - 48))
        End If
        Return CByte(bytPin(1) - 48)
    End Function

    Private Function HexToByte(ByVal strIn As String) As Byte
        Return CByte(CInt("&H" & strIn.Substring(2, 2)))
    End Function

    Private Sub BtnStart_Click(sender As Object, e As EventArgs) Handles BtnStart.Click
        If Timer1.Enabled = True Then
            Timer1.Stop()
            BtnStart.Text = "Start"
        Else
            Timer1.Start()
            BtnStart.Text = "Stop"
        End If
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Dim friction As Double
        friction = (readAnalog("B1") - My.Settings.zeroOffset) / (My.Settings.ownWeight - My.Settings.zeroOffset)
        Label1.Text = friction.ToString("F3")
    End Sub

    Private Sub BtnRecord_Click(sender As Object, e As EventArgs) Handles BtnRecord.Click
        Dim result As DialogResult
        SaveFileDialog1.ValidateNames = True
        SaveFileDialog1.DefaultExt = ".txt"
        SaveFileDialog1.Filter = "Text files|*.txt|All files|*.*"
        SaveFileDialog1.OverwritePrompt = True
        result = SaveFileDialog1.ShowDialog()
        If result = Windows.Forms.DialogResult.Cancel Then
            Exit Sub
        End If
        recordFile(SaveFileDialog1.FileName)
    End Sub

    Private Sub recordFile(filename As String)
        Dim friction As Double
        Dim readingCount As Integer
        Dim startTime As Date
        Dim timeDiff As TimeSpan
        Dim millis As Long = 0
        Timer1.Stop()
        BtnStart.Text = "Start"
        BtnStart.Enabled = False
        BtnRecord.Enabled = False
        BtnCalibrate.Enabled = False
        BtnInit.Enabled = False
        MsgBox("Press OK, then drag the device for 5 seconds.", MsgBoxStyle.Information, "Friction Meter")
        outfile = My.Computer.FileSystem.OpenTextFileWriter(filename, False)
        outfile.WriteLine("Time [ms]" & vbTab & "Ft/Fn")
        SendMessage(ProgressBar1.Handle, (1024 + 16), 3, 0)
        For readingCount = 0 To 199
            timeDiff = New TimeSpan(0)
            friction = (readAnalog("B1") - My.Settings.zeroOffset) / (My.Settings.ownWeight - My.Settings.zeroOffset)
            Label1.Text = friction.ToString("F3")
            ProgressBar1.Value = 0.5 * (readingCount + 1)
            Me.Refresh()
            startTime = DateTime.Now
            While timeDiff.TotalMilliseconds < 25
                timeDiff = DateTime.Now.Subtract(startTime)
                'Label1.Text = timeDiff.TotalMilliseconds.ToString
                'Label1.Refresh()
                'My.Application.DoEvents()
            End While
            millis += timeDiff.TotalMilliseconds
            outfile.WriteLine(millis.ToString & vbTab & friction.ToString("F3"))
        Next
        outfile.Close()
        SendMessage(ProgressBar1.Handle, (1024 + 16), 1, 0)
        ProgressBar1.Value = 100
        BtnStart.Enabled = True
        BtnRecord.Enabled = True
        BtnCalibrate.Enabled = True
        BtnInit.Enabled = True
    End Sub
    Private Function readAnalog(pin As String) As Double
        If clsFF3x_API.GetAnalogInput(ConvertPinName2PinBlock("B"), ConvertPinName2PinNumber(pin), bytResponse) = False Then
            Return Double.NaN
        Else
            Dim dblValue As Double = (((bytResponse(2) * 256) + bytResponse(3)) / 1023)
            If bytResponse(1) = CByte(&H33) Then
                Return dblValue * 3.3
            Else
                Return dblValue * 5
            End If
        End If
    End Function
    Private Sub InitFF()
        Dim intNumOfDevices As Integer
        clsFF3x_API.CloseComm()
        intNumOfDevices = clsFF3x_API.GetNumOfDevices
        ' check if any FF3x device is present
        If intNumOfDevices = 0 Then
            MsgBox("No FF3x device found.", MsgBoxStyle.Critical, "Friction Meter")
            'Me.Close()
            Return
        End If
        ' get 1st device path
        If clsFF3x_API.GetPath(1) = False Then
            MsgBox("Unable to locate FF3x device.", MsgBoxStyle.Critical, "Friction Meter")
            'Me.Close()
            Return
        End If
        ' connect to the device
        If clsFF3x_API.OpenComm = False Then
            MsgBox("Unable to open FF3x device.", MsgBoxStyle.Critical, "Friction Meter")
            'Me.Close()
            Return
        End If
    End Sub
    Private Sub Calibrate()
        Dim result As MsgBoxResult
        Dim reading As Double
        Dim readingCount As Integer
        Dim readingSum As Double = 0
        Dim zeroOffset As Double = 0
        Dim ownWeight As Double = 0.6
        result = MsgBox("Current zero offset is " & My.Settings.zeroOffset.ToString("F3") & ", current device weight is " & My.Settings.ownWeight.ToString("F3") & vbCrLf & "Leave the device with zero force on the string and press OK.", MsgBoxStyle.Information Or MsgBoxStyle.OkCancel, "Friction Meter")
        If result = MsgBoxResult.Cancel Then
            Exit Sub
        End If
        For readingCount = 0 To 49
            reading = readAnalog("B1")
            readingSum += reading
            Label1.Text = reading.ToString("F3")
            ProgressBar1.Value = (readingCount + 1) * 2
            Me.Refresh()
            System.Threading.Thread.Sleep(50)
        Next
        zeroOffset = readingSum / 50
        result = MsgBox("Zero offset is " & zeroOffset.ToString & ". Now suspend the device by the string and press OK.", MsgBoxStyle.Information Or MsgBoxStyle.OkCancel, "Friction Meter")
        If result = MsgBoxResult.Cancel Then
            Exit Sub
        End If
        readingSum = 0
        For readingCount = 0 To 49
            reading = readAnalog("B1")
            readingSum += reading
            Label1.Text = reading.ToString("F3")
            ProgressBar1.Value = (readingCount + 1) * 2
            Me.Refresh()
            System.Threading.Thread.Sleep(50)
        Next
        ownWeight = readingSum / 50
        result = MsgBox("Own weight of the device is " & ownWeight.ToString & ". Press OK to save the calibration results or cancel to keep the previous versions.", MsgBoxStyle.Information Or MsgBoxStyle.OkCancel, "Friction Meter")
        If result = MsgBoxResult.Cancel Then
            Exit Sub
        End If
        My.Settings.zeroOffset = zeroOffset
        My.Settings.ownWeight = ownWeight
    End Sub
    Private Sub BtnInit_Click(sender As Object, e As EventArgs) Handles BtnInit.Click
        Timer1.Stop()
        BtnStart.Text = "Start"
        InitFF()
    End Sub

    Private Sub BtnCalibrate_Click(sender As Object, e As EventArgs) Handles BtnCalibrate.Click
        Timer1.Stop()
        BtnStart.Text = "Start"
        Calibrate()
    End Sub

    Private Sub FrmMain_ResizeEnd(sender As Object, e As EventArgs) Handles Me.ResizeEnd
        Dim fhgt As Integer
        fhgt = Math.Min(Label1.Height * 0.6, Label1.Width * 0.2)
        Label1.Font = New Font("Arial", fhgt)
    End Sub
End Class
