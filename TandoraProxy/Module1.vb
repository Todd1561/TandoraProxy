Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading

Module Module1

    Dim pianobarStations As New ArrayList, pSong As String = "", pAlbum As String = "", pArtist As String = "", pStation As String = ""
    Dim cmd As String = "", pCurTime As String = "/", pIsPlaying As Boolean = False, pianobarLast As String = ""
    Dim pianoPath As String = AppDomain.CurrentDomain.BaseDirectory & "Pianobar.exe", pianoServ As String = "localhost", pianoPort As String = "23"
    Dim username As String = "", password As String = "", tandoraPort As String = "1561"
    Dim tcpListener

    Sub Main(ByVal args() As String)

        For Each arg As String In args
            If arg.Substring(0, 1) = "/" And arg.Contains("=") Then
                If arg.Substring(0, arg.IndexOf("=") + 1) = "/pianopath=" And arg.Length > 11 Then pianoPath = arg.Substring(arg.IndexOf("=") + 1)
                If arg.Substring(0, arg.IndexOf("=") + 1) = "/pianoserv=" And arg.Length > 11 Then pianoServ = arg.Substring(arg.IndexOf("=") + 1)
                If arg.Substring(0, arg.IndexOf("=") + 1) = "/pianoport=" And arg.Length > 11 Then pianoPort = arg.Substring(arg.IndexOf("=") + 1)
                If arg.Substring(0, arg.IndexOf("=") + 1) = "/tandoraport=" And arg.Length > 13 Then tandoraPort = arg.Substring(arg.IndexOf("=") + 1)
                If arg.Substring(0, arg.IndexOf("=") + 1) = "/username=" And arg.Length > 10 Then username = arg.Substring(arg.IndexOf("=") + 1)
                If arg.Substring(0, arg.IndexOf("=") + 1) = "/password=" And arg.Length > 10 Then password = arg.Substring(arg.IndexOf("=") + 1)
            End If

            If arg = "/help" Then
                Console.WriteLine(vbCrLf & "TandoraProxy, Ver. 2.01 (4/9/2020), toddnelson.net.  https://toddnelson.net" & vbCrLf)
                Console.WriteLine("Syntax:")
                Console.WriteLine(vbTab & "/pianopath=<absolute path to Pianobar exe> (default: current directory)")
                Console.WriteLine(vbTab & "/pianoserv=<address of Pianobar Telnet server> (default: localhost)")
                Console.WriteLine(vbTab & "/pianoport=<TCP port of Pianobar Telnet server> (default: 23)")
                Console.WriteLine(vbTab & "/tandoraport=<TCP port for TandoraProxy> (default: 1561)")
                Console.WriteLine(vbTab & "/username=<Pianobar Telnet server username> (Required)")
                Console.WriteLine(vbTab & "/password=<Pianobar Telnet server password> (Required)")
                Exit Sub
            End If
        Next

        If username = "" Or password = "" Then
            Console.WriteLine("Username and password required.  Run TandoraProxy.exe /help")
            Exit Sub
        End If

        If Not Regex.IsMatch(pianoPath.ToLower, "[a-z]:.*") Then
            Console.WriteLine("Path to Pianobar.exe needs to be a local drive letter.")
            Exit Sub
        End If

        tcpListener = New TcpListener(IPAddress.Any, tandoraPort)

        'Create new thread for TCP listener so rest of program can continue to monitor telnet/pianobar session
        Dim srvThread As Thread = New Thread(AddressOf ListenForClients)
        srvThread.Start()

        Dim tc As MinimalisticTelnet.TelnetConnection = New MinimalisticTelnet.TelnetConnection(pianoServ, pianoPort)
        Dim s As String = tc.Login(username, password, 600)
        Console.Write(s)
        Dim prompt As String = s.TrimEnd()
        prompt = s.Substring(prompt.Length - 1, 1)
        If prompt <> "$" AndAlso prompt <> ">" Then Throw New Exception("Connection failed")
        prompt = ""

        If tc.IsConnected Then
            tc.WriteLine(pianoPath.Substring(0, 2))
            tc.WriteLine("""" & pianoPath & """")
            Threading.Thread.Sleep(3000)

            'set pianobar to "high" priroty to avoid stuttering audio
            Process.Start("wmic", "process where name=""pianobar.exe"" CALL setpriority 128")

            Dim resp As String = ""

            'keep looping, taking response from pandora and sending commands as I get them from the API
            'commands can be broken up with pipes and comas injected to create delays. 
            '(useful for changing stations And needing to wait a split second for list to build in pianobar UI)
            While True

                If cmd <> "" Then
                    For Each c As String In cmd.Split("|")
                        If c = "," Then Thread.Sleep(50) Else tc.Write(c)
                    Next

                    cmd = ""
                End If

                resp = tc.Read

                If resp <> "" Then

                    'parse out station list
                    If Regex.IsMatch(resp, "\s(\d+)\).{5}(.*?)\n") Then
                        For Each station As Match In Regex.Matches(resp, "\s(\d+)\).{5}(.*?)\n")

                            'Detected the first station, clear the array
                            If station.Groups(1).Value.Trim = "0" Then pianobarStations.Clear()
                            pianobarStations.Add(station.Groups(2).Value.Trim)

                        Next
                    End If

                    'parse out current song info
                    If Regex.IsMatch(resp, "\|\>  ""(.*?)"" by ""(.*?)"" on ""(.*?)""") Then
                        Dim m As Match = Regex.Match(resp, "\|\>  ""(.*?)"" by ""(.*?)"" on ""(.*?)""")
                        pSong = m.Groups(1).Value
                        pArtist = m.Groups(2).Value
                        pAlbum = m.Groups(3).Value
                    End If

                    'parse out current station
                    If Regex.IsMatch(resp, "\|\>  Station ""(.*?)""") Then
                        Dim m As Match = Regex.Match(resp, "\|\>  Station ""(.*?)""")
                        pStation = m.Groups(1).Value
                    End If

                    'parse out current play time
                    If Regex.IsMatch(resp, "#\s+([-\d:]+)/([-\d:]+)") Then
                        Dim lastPCurTime As String = pCurTime
                        Dim m As Match = Regex.Match(resp, "#\s+([-\d:]+)/([-\d:]+)")
                        pCurTime = m.Groups(1).Value & "/" & m.Groups(2).Value
                        If lastPCurTime <> pCurTime Then pIsPlaying = True Else pIsPlaying = False
                    End If

                    Console.Write(resp)
                    pianobarLast = resp
                End If

            End While

        End If

        While tc.IsConnected AndAlso prompt.Trim() <> "exit"
            Console.Write(tc.Read())
            prompt = Console.ReadLine()
            tc.WriteLine(prompt)
            Console.Write(tc.Read())
        End While

        Console.WriteLine("***DISCONNECTED")
        Console.ReadLine()
    End Sub

    Sub ListenForClients()

        tcpListener.Start()

        While True
            'blocks until a client has connected to the server
            Dim client As TcpClient = tcpListener.AcceptTcpClient
            'create a thread to handle communication with connected client
            Dim clientThread As Thread = New Thread(New ParameterizedThreadStart(AddressOf HandleClientComm))
            clientThread.Start(client)
        End While

    End Sub

    Private Sub HandleClientComm(ByVal client As Object)
        Try
            Dim tcpClient As TcpClient = CType(client, TcpClient)
            Dim clientStream As NetworkStream = tcpClient.GetStream
            Dim message() As Byte = New Byte((4096) - 1) {}
            Dim encoder As ASCIIEncoding = New ASCIIEncoding
            Dim bytesRead As Integer
            Dim fullMsg As String = ""

            While True
                bytesRead = 0
                Try
                    'blocks until a client sends a message
                    bytesRead = clientStream.Read(message, 0, 4096)
                Catch ex As System.Exception
                    'a socket error has occured
                    Exit While
                End Try

                fullMsg += encoder.GetString(message, 0, bytesRead)

                If (bytesRead = 0) Or fullMsg.Contains(vbCrLf) Then
                    'the client has disconnected from the server
                    fullMsg = fullMsg.Replace(vbCrLf, "")
                    Exit While
                End If

            End While

            Console.WriteLine("Command Received: """ & fullMsg & """ (" & Now & ")")

            If fullMsg.Contains("change station") Then

                'Capture current song info so I know when pianobar has finished selecting new song
                Dim curPianobarDur As String = pCurTime.Substring(pCurTime.IndexOf("/"))

                'See if song selected, if so need to press 's' first to change station
                If pianobarLast.Substring(0, 3) = "#  " Then cmd = "s|,|"
                cmd += pianobarStations.IndexOf(fullMsg.Substring(15)) & vbCrLf

                'Wait for pianobar to select song and start playing
                Do Until curPianobarDur <> pCurTime.Substring(pCurTime.IndexOf("/"))
                    Threading.Thread.Sleep(10)
                Loop
            ElseIf fullMsg.Contains("playpause") Then
                cmd = "p"
            ElseIf fullMsg.Contains("next") Then
                Dim curPianobarDur As String = pCurTime.Substring(pCurTime.IndexOf("/"))
                cmd = "n"
                Do Until curPianobarDur <> pCurTime.Substring(pCurTime.IndexOf("/"))
                    Threading.Thread.Sleep(10)
                Loop
            ElseIf fullMsg.Contains("thumbsdown") Then
                Dim curPianobarDur As String = pCurTime.Substring(pCurTime.IndexOf("/"))
                cmd = "-"
                Do Until curPianobarDur <> pCurTime.Substring(pCurTime.IndexOf("/"))
                    Threading.Thread.Sleep(100)
                Loop
            ElseIf fullMsg.Contains("thumpsup") Then
                cmd = "+"
            End If

            If Trim(pStation) = "" Then pStation = "** No Station Selected **"
            If Trim(pSong) = "" Then pSong = "** No Song Selected **"

            Dim sendStr As String = "IS PLAYING: " & pIsPlaying & vbCrLf &
                                    "STATION LIST: " & String.Join("|", pianobarStations.ToArray) & vbCrLf &
                                    "CURRENT STATION: " & pStation & vbCrLf &
                                    "CURRENT SONG: """ & pSong.Replace(ChrW(233), "e") & """ by """ & pArtist & """ on """ & pAlbum & """" & vbCrLf &
                                    "CURRENT TIME: " & pCurTime & vbCrLf

            Dim sendBytes As Byte() = Encoding.ASCII.GetBytes(sendStr)
            clientStream.Write(sendBytes, 0, sendBytes.Length)

            tcpClient.Close()
        Catch e As Exception
            Console.WriteLine(e.StackTrace)
            'System.Diagnostics.EventLog.WriteEntry("TandoraProxy", e.Message & vbCrLf & vbCrLf & e.StackTrace, EventLogEntryType.Error)
        End Try
    End Sub

End Module
