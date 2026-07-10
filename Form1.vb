Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.IO

Public Class Form1
    Inherits Form

    Private WithEvents TickTimer As New Timer()
    Private net As NetworkPeer
    Private game As New PlatformGame()

    Private isHost As Boolean = False
    Private isConnected As Boolean = False
    Private isSoloMode As Boolean = False
    Private localPlayerIndex As Integer = 0 ' host = 0, client = 1
    Private frameCounter As Integer = 0

    Private keyLeft As Boolean
    Private keyRight As Boolean
    Private keyJump As Boolean
    Private keyShoot As Boolean
    Private keyDown As Boolean

    Private Structure SpriteSheet
        Public Sheet As Bitmap
        Public FrameW As Integer
        Public FrameH As Integer
        Public FrameCount As Integer
    End Structure

    Private sheetPlayer0 As SpriteSheet
    Private sheetPlayer1 As SpriteSheet
    Private sheetWalker As SpriteSheet
    Private sheetBoss As SpriteSheet

    Private spPlayer0 As Bitmap
    Private spPlayer0Walk2 As Bitmap
    Private spPlayer0Jump As Bitmap
    Private spPlayer1 As Bitmap
    Private spPlayer1Walk2 As Bitmap
    Private spPlayer1Jump As Bitmap
    Private spWalker As Bitmap
    Private spWalkerWalk2 As Bitmap
    Private spShelled As Bitmap
    Private spShell As Bitmap
    Private spBoss As Bitmap
    Private spBossWalk2 As Bitmap
    Private spPlant As Bitmap
    Private spFlag As Bitmap
    Private spCastle As Bitmap
    Private spPrincess As Bitmap
    Private spGround As Bitmap
    Private spQuestionBlock As Bitmap
    Private spPipe As Bitmap
    Private spFireball As Bitmap
    Private spEnemyFireball As Bitmap
    Private spPowerGrow As Bitmap
    Private spPowerFire As Bitmap
    Private spPowerLife As Bitmap
    Private spBackground As Bitmap

    Private lblStatus As New Label()
    Private btnSolo As New Button()
    Private btnHost As New Button()
    Private btnJoin As New Button()
    Private txtIp As New TextBox()
    Private pnlMenu As New Panel()

    Public Sub New()
        Me.Text = "Phieu Luu Ong Nuoc - Co-op Online"
        Me.ClientSize = New Size(PlatformGame.VIEW_WIDTH_PX, PlatformGame.VIEW_HEIGHT_PX)
        Me.DoubleBuffered = True
        Me.KeyPreview = True
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen

        LoadSpritesIfExist()
        BuildMenuUI()

        TickTimer.Interval = PlatformGame.TICK_MS
    End Sub

    ' ===================== UI CHON HOST/JOIN =====================
    Private Sub BuildMenuUI()
        pnlMenu.Size = New Size(280, 210)
        pnlMenu.Location = New Point((Me.ClientSize.Width - pnlMenu.Width) \ 2, (Me.ClientSize.Height - pnlMenu.Height) \ 2)
        pnlMenu.BackColor = Color.FromArgb(230, 20, 20, 30)

        btnSolo.Text = "Choi 1 nguoi (Solo)"
        btnSolo.Size = New Size(240, 36)
        btnSolo.Location = New Point(20, 20)
        AddHandler btnSolo.Click, AddressOf OnSoloClick

        btnHost.Text = "Choi 2 nguoi - Tao phong (Host)"
        btnHost.Size = New Size(240, 36)
        btnHost.Location = New Point(20, 66)
        AddHandler btnHost.Click, AddressOf OnHostClick

        txtIp.Text = "127.0.0.1"
        txtIp.Size = New Size(240, 24)
        txtIp.Location = New Point(20, 116)

        btnJoin.Text = "Choi 2 nguoi - Vao phong (Join)"
        btnJoin.Size = New Size(240, 36)
        btnJoin.Location = New Point(20, 150)
        AddHandler btnJoin.Click, AddressOf OnJoinClick

        lblStatus.Text = "Chon Solo, Host hoac Join"
        lblStatus.ForeColor = Color.White
        lblStatus.AutoSize = True
        lblStatus.Location = New Point(20, 4)

        pnlMenu.Controls.Add(lblStatus)
        pnlMenu.Controls.Add(btnSolo)
        pnlMenu.Controls.Add(btnHost)
        pnlMenu.Controls.Add(txtIp)
        pnlMenu.Controls.Add(btnJoin)
        Me.Controls.Add(pnlMenu)
    End Sub

    Private Sub OnSoloClick(sender As Object, e As EventArgs)
        isSoloMode = True
        isHost = True
        localPlayerIndex = 0
        game.SetSoloMode()
        pnlMenu.Visible = False
        TickTimer.Start()
    End Sub

    Private Sub OnHostClick(sender As Object, e As EventArgs)
        isHost = True
        localPlayerIndex = 0
        net = New NetworkPeer(Me)
        AddHandler net.LineReceived, AddressOf OnLineReceived
        AddHandler net.Connected, AddressOf OnPeerConnected
        AddHandler net.Disconnected, AddressOf OnPeerDisconnected
        net.StartHost(9898)
        lblStatus.Text = "Dang cho nguoi choi thu 2 ket noi... (port 9898)"
    End Sub

    Private Sub OnJoinClick(sender As Object, e As EventArgs)
        isHost = False
        localPlayerIndex = 1
        net = New NetworkPeer(Me)
        AddHandler net.LineReceived, AddressOf OnLineReceived
        AddHandler net.Connected, AddressOf OnPeerConnected
        AddHandler net.Disconnected, AddressOf OnPeerDisconnected
        net.ConnectToHost(txtIp.Text.Trim(), 9898)
        lblStatus.Text = "Dang ket noi den " & txtIp.Text.Trim() & " ..."
    End Sub

    Private Sub OnPeerConnected()
        isConnected = True
        pnlMenu.Visible = False
        TickTimer.Start()
    End Sub

    Private Sub OnPeerDisconnected()
        isConnected = False
        TickTimer.Stop()
        pnlMenu.Visible = True
        lblStatus.Text = "Mat ket noi. Chon lai Host/Join."
    End Sub

    ' ===================== NHAN DU LIEU MANG =====================
    Private Sub OnLineReceived(line As String)
        If isHost Then
            If line.StartsWith("INPUT|") Then
                Dim inp As PlatformGame.PlayerInput = PlatformGame.ParseInput(line)
                game.SetInput(1, inp)
            End If
        Else
            If line.StartsWith("STATE|") Then
                game.ApplyStateLine(line)
            End If
        End If
    End Sub

    ' ===================== VONG LAP CHINH =====================
    Private Sub TickTimer_Tick(sender As Object, e As EventArgs) Handles TickTimer.Tick
        frameCounter += 1
        Dim localInp As New PlatformGame.PlayerInput()
        localInp.Left = keyLeft
        localInp.Right = keyRight
        localInp.Jump = keyJump
        localInp.Shoot = keyShoot
        localInp.Down = keyDown

        If isSoloMode Then
            game.SetInput(0, localInp)
            game.Tick()
            Me.Invalidate()
            Return
        End If

        If isHost Then
            game.SetInput(0, localInp)
            game.Tick()
            If isConnected Then
                net.SendLine(game.SerializeState())
            End If
        Else
            If isConnected Then
                net.SendLine(PlatformGame.SerializeInput(localInp))
            End If
        End If

        Me.Invalidate()
    End Sub

    ' ===================== BAT PHIM =====================
    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        SetKeyState(e.KeyCode, True)
        MyBase.OnKeyDown(e)
    End Sub

    Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
        SetKeyState(e.KeyCode, False)
        MyBase.OnKeyUp(e)
    End Sub

    Private Sub SetKeyState(key As Keys, isDown As Boolean)
        Select Case key
            Case Keys.Left, Keys.A : keyLeft = isDown
            Case Keys.Right, Keys.D : keyRight = isDown
            Case Keys.Up, Keys.W, Keys.Space, Keys.Z : keyJump = isDown
            Case Keys.ControlKey, Keys.X : keyShoot = isDown
            Case Keys.Down, Keys.S : keyDown = isDown
        End Select
    End Sub

    ' ===================== VE HINH (GDI+ / sprite fallback) =====================
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.InterpolationMode = InterpolationMode.NearestNeighbor
        g.Clear(Color.FromArgb(92, 148, 252))

        DrawBackground(g)
        DrawFlagpoleAndCastle(g)
        DrawPlatforms(g)
        DrawPowerUps(g)
        DrawEnemies(g)
        DrawFireballs(g)
        DrawPlayers(g)
        DrawHud(g)

        If Not isConnected AndAlso Not isSoloMode Then
            Using f As New Font("Consolas", 10, FontStyle.Bold)
                g.DrawString("Chua ket noi - dung menu de Host/Join", f, Brushes.White, 10, PlatformGame.VIEW_HEIGHT_PX - 24)
            End Using
        End If

        MyBase.OnPaint(e)
    End Sub

    ' Ve cot co (ha xuong khi cham co), khung thanh, va cong chua dung o cong (chi hien sau khi thang).
    ' Thiet ke cong chua la nguyen ban (vay tim, vuong mien bac) de tranh trung hinh anh ban quyen.
    Private Sub DrawFlagpoleAndCastle(g As Graphics)
        Dim poleScreenX As Integer = WorldToScreenX(PlatformGame.FLAGPOLE_X)
        Dim castleScreenX As Integer = WorldToScreenX(PlatformGame.CASTLE_X)

        ' Khung thanh (chi ve khi da vao tam nhin)
        If castleScreenX > -220 AndAlso castleScreenX < PlatformGame.VIEW_WIDTH_PX + 40 Then
            Dim castleTop As Integer = PlatformGame.GROUND_Y - 190
            If spCastle IsNot Nothing Then
                Dim drawH As Integer = 190
                Dim drawW As Integer = CInt(spCastle.Width * (drawH / CDbl(spCastle.Height)))
                g.DrawImage(spCastle, castleScreenX, castleTop, drawW, drawH)
            Else
                Using wallB As New SolidBrush(Color.FromArgb(160, 160, 170))
                    g.FillRectangle(wallB, castleScreenX, castleTop + 30, 180, 160)
                End Using
                g.DrawRectangle(Pens.Black, castleScreenX, castleTop + 30, 180, 160)
                ' Rang chan thanh (crenellation)
                Using merlonB As New SolidBrush(Color.FromArgb(160, 160, 170))
                    For mx As Integer = castleScreenX To castleScreenX + 160 Step 30
                        g.FillRectangle(merlonB, mx, castleTop, 20, 30)
                        g.DrawRectangle(Pens.Black, mx, castleTop, 20, 30)
                    Next
                End Using
                ' Cong vom toi
                Using doorB As New SolidBrush(Color.FromArgb(40, 30, 30))
                    g.FillRectangle(doorB, castleScreenX + 70, castleTop + 100, 50, 90)
                End Using
            End If
        End If

        ' Cot co
        If poleScreenX > -20 AndAlso poleScreenX < PlatformGame.VIEW_WIDTH_PX + 20 Then
            Dim topY As Integer = CInt(PlatformGame.FLAGPOLE_TOP_Y)
            Dim botY As Integer = CInt(PlatformGame.FLAGPOLE_BOTTOM_Y)
            Using poleBrush As New SolidBrush(Color.FromArgb(210, 210, 200))
                g.FillRectangle(poleBrush, poleScreenX, topY, 4, botY - topY)
            End Using
            g.FillEllipse(Brushes.Gold, poleScreenX - 4, topY - 10, 12, 12)

            Dim flagY As Integer = If(game.FlagpoleTouched, botY - 40, topY + 4)
            If spFlag IsNot Nothing Then
                g.DrawImage(spFlag, poleScreenX + 4, flagY, 34, 24)
            Else
                Dim flagPts As Point() = {
                    New Point(poleScreenX + 4, flagY),
                    New Point(poleScreenX + 4, flagY + 24),
                    New Point(poleScreenX + 34, flagY + 12)
                }
                Using flagBrush As New SolidBrush(Color.LimeGreen)
                    g.FillPolygon(flagBrush, flagPts)
                End Using
                g.DrawPolygon(Pens.Black, flagPts)
            End If
        End If

        ' Cong chua dung truoc cong thanh, chi hien khi da thang man
        If game.Victory AndAlso castleScreenX > -80 AndAlso castleScreenX < PlatformGame.VIEW_WIDTH_PX + 80 Then
            Dim px As Integer = castleScreenX + 90
            Dim py As Integer = PlatformGame.GROUND_Y - 44
            If spPrincess IsNot Nothing Then
                g.DrawImage(spPrincess, px, py, 24, 44)
            Else
                ' Vay hinh tam giac mau tim, mien bac (khac tao hinh nhan vat ban quyen)
                Dim dressPts As Point() = {
                    New Point(px + 12, py + 10),
                    New Point(px, py + 44),
                    New Point(px + 24, py + 44)
                }
                Using dressBrush As New SolidBrush(Color.MediumPurple)
                    g.FillPolygon(dressBrush, dressPts)
                End Using
                g.DrawPolygon(Pens.Black, dressPts)
                Using skinBrush As New SolidBrush(Color.NavajoWhite)
                    g.FillEllipse(skinBrush, px + 4, py, 16, 16)
                End Using
                g.DrawEllipse(Pens.Black, px + 4, py, 16, 16)
                Dim crownPts As Point() = {
                    New Point(px + 4, py - 2), New Point(px + 8, py - 10), New Point(px + 12, py - 4),
                    New Point(px + 16, py - 10), New Point(px + 20, py - 2)
                }
                Using crownBrush As New SolidBrush(Color.Silver)
                    g.FillPolygon(crownBrush, crownPts)
                End Using
                g.DrawPolygon(Pens.Black, crownPts)
            End If
        End If
    End Sub

    Private Function WorldToScreenX(worldX As Double) As Integer
        Return CInt(Math.Round(worldX - game.CameraX))
    End Function

    Private Sub DrawBackground(g As Graphics)
        If spBackground IsNot Nothing Then
            Dim scaledH As Integer = PlatformGame.VIEW_HEIGHT_PX
            Dim scaledW As Integer = CInt(spBackground.Width * (scaledH / CDbl(spBackground.Height)))
            If scaledW < 1 Then scaledW = 1
            Dim offset As Integer = CInt(game.CameraX * 0.3) Mod scaledW
            g.DrawImage(spBackground, New Rectangle(-offset, 0, scaledW, scaledH))
            g.DrawImage(spBackground, New Rectangle(-offset + scaledW, 0, scaledW, scaledH))
        Else
            Using skyBrush As New SolidBrush(Color.FromArgb(92, 148, 252))
                g.FillRectangle(skyBrush, 0, 0, PlatformGame.VIEW_WIDTH_PX, PlatformGame.VIEW_HEIGHT_PX)
            End Using
        End If
    End Sub

    Private Sub DrawPlatforms(g As Graphics)
        For Each plat In game.Platforms
            Dim sx As Integer = WorldToScreenX(plat.X)
            If sx + plat.W < 0 OrElse sx > PlatformGame.VIEW_WIDTH_PX Then Continue For
            Dim sy As Integer = CInt(plat.Y)
            Dim w As Integer = CInt(plat.W)
            Dim h As Integer = CInt(plat.H)

            Select Case plat.Kind
                Case PlatformGame.PlatformKind.Ground
                    If spGround IsNot Nothing Then
                        Dim tx As Integer = sx
                        Do While tx < sx + w
                            g.DrawImage(spGround, tx, sy)
                            tx += spGround.Width
                        Loop
                    Else
                        Using b As New SolidBrush(Color.FromArgb(150, 90, 40))
                            g.FillRectangle(b, sx, sy, w, h)
                        End Using
                        Using topB As New SolidBrush(Color.FromArgb(70, 190, 70))
                            g.FillRectangle(topB, sx, sy, w, 8)
                        End Using
                    End If

                Case PlatformGame.PlatformKind.OneWay
                    Using b As New SolidBrush(Color.FromArgb(170, 120, 70))
                        g.FillRectangle(b, sx, sy, w, h)
                    End Using
                    g.DrawRectangle(Pens.Black, sx, sy, w, h)

                Case PlatformGame.PlatformKind.Pipe
                    If spPipe IsNot Nothing Then
                        g.DrawImage(spPipe, sx, sy, w, h)
                    Else
                        Using b As New SolidBrush(Color.FromArgb(40, 170, 70))
                            g.FillRectangle(b, sx, sy, w, h)
                        End Using
                        Using rim As New SolidBrush(Color.FromArgb(30, 130, 50))
                            g.FillRectangle(rim, sx - 4, sy, w + 8, 14)
                        End Using
                        g.DrawRectangle(Pens.Black, sx, sy, w, h)
                    End If

                Case PlatformGame.PlatformKind.WarpPipe
                    If spPipe IsNot Nothing Then
                        g.DrawImage(spPipe, sx, sy, w, h)
                    Else
                        Using b As New SolidBrush(Color.FromArgb(40, 170, 70))
                            g.FillRectangle(b, sx, sy, w, h)
                        End Using
                        Using rim As New SolidBrush(Color.FromArgb(30, 130, 50))
                            g.FillRectangle(rim, sx - 4, sy, w + 8, 14)
                        End Using
                        g.DrawRectangle(Pens.Black, sx, sy, w, h)
                    End If
                    ' Mui ten trang bao hieu bam Xuong de chui vao
                    Using f As New Font("Consolas", 8, FontStyle.Bold)
                        g.DrawString(ChrW(8595), f, Brushes.White, sx + w / 2.0F - 5, sy - 16)
                    End Using

                Case PlatformGame.PlatformKind.Brick
                    Using b As New SolidBrush(Color.FromArgb(180, 100, 60))
                        g.FillRectangle(b, sx, sy, w, h)
                    End Using
                    g.DrawRectangle(Pens.Black, sx, sy, w, h)
                    g.DrawLine(Pens.Black, sx + w \ 2, sy, sx + w \ 2, sy + h)

                Case PlatformGame.PlatformKind.QuestionBlock
                    If plat.Used Then
                        Using b As New SolidBrush(Color.FromArgb(120, 90, 60))
                            g.FillRectangle(b, sx, sy, w, h)
                        End Using
                        g.DrawRectangle(Pens.Black, sx, sy, w, h)
                    ElseIf spQuestionBlock IsNot Nothing Then
                        g.DrawImage(spQuestionBlock, sx, sy, w, h)
                    Else
                        Using b As New SolidBrush(Color.FromArgb(240, 180, 30))
                            g.FillRectangle(b, sx, sy, w, h)
                        End Using
                        g.DrawRectangle(Pens.Black, sx, sy, w, h)
                        Using f As New Font("Consolas", 12, FontStyle.Bold)
                            g.DrawString("?", f, Brushes.White, sx + w / 4.0F, sy - 1)
                        End Using
                    End If
            End Select
        Next
    End Sub

    Private Sub DrawPlayers(g As Graphics)
        For i As Integer = 0 To 1
            Dim p As PlatformGame.PlayerState = game.Players(i)
            If Not p.Alive Then Continue For

            Dim sx As Integer = WorldToScreenX(p.X)
            Dim sy As Integer = CInt(p.Y)
            Dim blink As Boolean = (p.InvulnTicks > 0) AndAlso ((p.InvulnTicks \ 4) Mod 2 = 0)
            If blink Then Continue For

            Dim sheet As SpriteSheet = If(i = 0, sheetPlayer0, sheetPlayer1)
            If sheet.Sheet IsNot Nothing Then
                Dim frameIdx As Integer = ChooseSheetFrame(sheet, p.OnGround, p.IsMoving)
                DrawSheetFrame(g, sheet, frameIdx, sx, sy, PlatformGame.PLAYER_W, PlatformGame.PLAYER_H, Not p.FacingRight)
                DrawPowerBadge(g, p, sx, sy)
                Continue For
            End If

            Dim baseSprite As Bitmap = If(i = 0, spPlayer0, spPlayer1)
            Dim walk2Sprite As Bitmap = If(i = 0, spPlayer0Walk2, spPlayer1Walk2)
            Dim jumpSprite As Bitmap = If(i = 0, spPlayer0Jump, spPlayer1Jump)

            Dim sprite As Bitmap
            If Not p.OnGround AndAlso jumpSprite IsNot Nothing Then
                sprite = jumpSprite
            ElseIf p.IsMoving AndAlso p.OnGround AndAlso walk2Sprite IsNot Nothing AndAlso ((frameCounter \ 6) Mod 2 = 1) Then
                sprite = walk2Sprite
            Else
                sprite = baseSprite
            End If

            If sprite IsNot Nothing Then
                Dim st As GraphicsState = g.Save()
                If Not p.FacingRight Then
                    g.TranslateTransform(sx + PlatformGame.PLAYER_W, sy)
                    g.ScaleTransform(-1, 1)
                    g.DrawImage(sprite, 0, 0, PlatformGame.PLAYER_W, PlatformGame.PLAYER_H)
                Else
                    g.DrawImage(sprite, sx, sy, PlatformGame.PLAYER_W, PlatformGame.PLAYER_H)
                End If
                g.Restore(st)
            Else
                ' Mau theo cap do suc manh: xam nhat = nho, xanh la = to, cam = co lua
                Dim c As Color = If(p.WeaponLevel >= 2, Color.OrangeRed, If(p.WeaponLevel = 1, If(i = 0, Color.LightGreen, Color.LightSkyBlue), Color.Gray))
                Using b As New SolidBrush(c)
                    g.FillRectangle(b, sx, sy, PlatformGame.PLAYER_W, PlatformGame.PLAYER_H)
                End Using
                g.DrawRectangle(Pens.Black, sx, sy, PlatformGame.PLAYER_W, PlatformGame.PLAYER_H)
                Dim cx As Integer = If(p.FacingRight, sx + PlatformGame.PLAYER_W - 4, sx)
                g.FillEllipse(Brushes.White, cx - 2, sy + 6, 6, 6)
            End If
            DrawPowerBadge(g, p, sx, sy)
        Next
    End Sub

    Private Sub DrawPowerBadge(g As Graphics, p As PlatformGame.PlayerState, sx As Integer, sy As Integer)
        If p.WeaponLevel >= 2 Then
            Using f As New Font("Consolas", 8, FontStyle.Bold)
                g.DrawString("*", f, Brushes.OrangeRed, sx + 6, sy - 12)
            End Using
        End If
    End Sub

    Private Sub DrawEnemies(g As Graphics)
        For Each en In game.Enemies
            If Not en.Alive Then Continue For
            If en.Kind = PlatformGame.EnemyType.PipePlant AndAlso Not en.IsShell Then Continue For
            Dim sx As Integer = WorldToScreenX(en.X)
            If sx < -60 OrElse sx > PlatformGame.VIEW_WIDTH_PX + 60 Then Continue For
            Dim sy As Integer = CInt(en.Y)

            Dim sprite As Bitmap = Nothing
            Dim fallbackColor As Color = Color.SaddleBrown
            Dim w As Integer = PlatformGame.ENEMY_W
            Dim h As Integer = PlatformGame.ENEMY_H
            Dim useWalk2 As Boolean = ((frameCounter \ 6) Mod 2 = 1)

            Select Case en.Kind
                Case PlatformGame.EnemyType.Walker
                    sprite = If(useWalk2 AndAlso spWalkerWalk2 IsNot Nothing, spWalkerWalk2, spWalker)
                    fallbackColor = Color.SaddleBrown

                Case PlatformGame.EnemyType.Shelled
                    fallbackColor = If(en.IsShell, Color.DarkGreen, Color.ForestGreen)
                    sprite = Nothing ' luon fallback GDI+ de the hien ro trang thai mai/da lan

                Case PlatformGame.EnemyType.Boss
                    sprite = If(useWalk2 AndAlso spBossWalk2 IsNot Nothing, spBossWalk2, spBoss)
                    fallbackColor = Color.Purple
                    w = PlatformGame.BOSS_W : h = PlatformGame.BOSS_H

                Case PlatformGame.EnemyType.PipePlant
                    sprite = spPlant
                    fallbackColor = Color.OrangeRed
                    h = PlatformGame.ENEMY_H
                    w = If(spPlant IsNot Nothing, CInt(spPlant.Width * (h / CDbl(spPlant.Height))), PlatformGame.ENEMY_W)
            End Select

            If en.Kind = PlatformGame.EnemyType.Shelled Then
                DrawShelledEnemy(g, en, sx, sy, w, h)
                Continue For
            End If

            Dim sheet As SpriteSheet = Nothing
            If en.Kind = PlatformGame.EnemyType.Walker Then
                sheet = sheetWalker
            ElseIf en.Kind = PlatformGame.EnemyType.Boss Then
                sheet = sheetBoss
            End If

            If sheet.Sheet IsNot Nothing Then
                Dim frameIdx As Integer = ChooseSheetFrame(sheet, True, True)
                DrawSheetFrame(g, sheet, frameIdx, sx, sy, w, h, Not en.FacingRight)
            ElseIf sprite IsNot Nothing Then
                If Not en.FacingRight Then
                    Dim st As GraphicsState = g.Save()
                    g.TranslateTransform(sx + w, sy)
                    g.ScaleTransform(-1, 1)
                    g.DrawImage(sprite, 0, 0, w, h)
                    g.Restore(st)
                Else
                    g.DrawImage(sprite, sx, sy, w, h)
                End If
            Else
                Using b As New SolidBrush(fallbackColor)
                    g.FillRectangle(b, sx, sy, w, h)
                End Using
                g.DrawRectangle(Pens.Black, sx, sy, w, h)
            End If

            If en.Kind = PlatformGame.EnemyType.Boss Then
                Dim barW As Integer = PlatformGame.BOSS_W
                g.DrawRectangle(Pens.White, sx, sy - 10, barW, 6)
                Dim hpRatio As Double = Math.Max(0.0, Math.Min(1.0, en.HP / 6.0))
                Using hb As New SolidBrush(Color.Red)
                    g.FillRectangle(hb, sx, sy - 10, CInt(barW * hpRatio), 6)
                End Using
            End If
        Next
    End Sub

    ' Quai co mai: dang di (dung sprite spShelled hoac fallback hinh oval mau xanh nhat)
    ' -> bi dam thanh mai (dung sprite spShell hoac fallback bet mau xanh dam, vien do
    ' khi dang bi da lan de canh bao nguy hiem).
    Private Sub DrawShelledEnemy(g As Graphics, en As PlatformGame.EnemyState, sx As Integer, sy As Integer, w As Integer, h As Integer)
        If Not en.IsShell Then
            If spShelled IsNot Nothing Then
                If Not en.FacingRight Then
                    Dim st As GraphicsState = g.Save()
                    g.TranslateTransform(sx + w, sy)
                    g.ScaleTransform(-1, 1)
                    g.DrawImage(spShelled, 0, 0, w, h)
                    g.Restore(st)
                Else
                    g.DrawImage(spShelled, sx, sy, w, h)
                End If
            Else
                Using b As New SolidBrush(Color.ForestGreen)
                    g.FillEllipse(b, sx, sy, w, h)
                End Using
                g.DrawEllipse(Pens.Black, sx, sy, w, h)
            End If
        Else
            Dim shellH As Integer = h \ 2
            Dim shellY As Integer = sy + (h - shellH)
            If spShell IsNot Nothing Then
                g.DrawImage(spShell, sx, shellY, w, shellH)
                If en.ShellMoving Then
                    g.DrawRectangle(Pens.Red, sx, shellY, w, shellH)
                End If
            Else
                Using b As New SolidBrush(Color.DarkGreen)
                    g.FillEllipse(b, sx, shellY, w, shellH)
                End Using
                Dim pen As Pen = If(en.ShellMoving, Pens.Red, Pens.Black)
                g.DrawEllipse(pen, sx, shellY, w, shellH)
            End If
        End If
    End Sub

    Private Sub DrawFireballs(g As Graphics)
        For Each fb In game.Fireballs
            If Not fb.Active Then Continue For
            Dim sx As Integer = WorldToScreenX(fb.X)
            If sx < -20 OrElse sx > PlatformGame.VIEW_WIDTH_PX + 20 Then Continue For
            Dim sy As Integer = CInt(fb.Y)

            Dim sprite As Bitmap = If(fb.Owner >= 0, spFireball, spEnemyFireball)
            If sprite IsNot Nothing Then
                g.DrawImage(sprite, sx - 5, sy - 5, 10, 10)
            Else
                Dim c As Color = If(fb.Owner >= 0, Color.OrangeRed, Color.Magenta)
                Using b As New SolidBrush(c)
                    g.FillEllipse(b, sx - 5, sy - 5, 10, 10)
                End Using
            End If
        Next
    End Sub

    Private Sub DrawPowerUps(g As Graphics)
        For Each pu In game.PowerUps
            If Not pu.Active Then Continue For
            Dim sx As Integer = WorldToScreenX(pu.X)
            If sx < -40 OrElse sx > PlatformGame.VIEW_WIDTH_PX + 40 Then Continue For
            Dim sy As Integer = CInt(pu.Y)

            Select Case pu.Kind
                Case PlatformGame.PowerUpType.Coin
                    Using b As New SolidBrush(Color.Gold)
                        g.FillEllipse(b, sx, sy, 16, 16)
                    End Using
                    g.DrawEllipse(Pens.DarkGoldenrod, sx, sy, 16, 16)
                Case PlatformGame.PowerUpType.GrowMushroom
                    DrawPowerUpSprite(g, spPowerGrow, Color.Red, sx, sy)
                Case PlatformGame.PowerUpType.FireFlower
                    DrawPowerUpSprite(g, spPowerFire, Color.OrangeRed, sx, sy)
                Case PlatformGame.PowerUpType.OneUp
                    DrawPowerUpSprite(g, spPowerLife, Color.LimeGreen, sx, sy)
            End Select
        Next
    End Sub

    Private Sub DrawPowerUpSprite(g As Graphics, sprite As Bitmap, fallbackColor As Color, sx As Integer, sy As Integer)
        If sprite IsNot Nothing Then
            g.DrawImage(sprite, sx, sy, 20, 20)
        Else
            Using b As New SolidBrush(fallbackColor)
                g.FillEllipse(b, sx, sy, 20, 20)
            End Using
            g.DrawEllipse(Pens.Black, sx, sy, 20, 20)
        End If
    End Sub

    Private Sub DrawHud(g As Graphics)
        Using f As New Font("Consolas", 11, FontStyle.Bold)
            Dim txt As String
            If isSoloMode Then
                txt = String.Format("Mang: {0}   Xu: {1}   Diem: {2}   Suc manh Lv{3}",
                    game.SharedLives, game.SharedCoins, game.SharedScore, game.Players(0).WeaponLevel)
            Else
                txt = String.Format("Mang: {0}   Xu: {1}   Diem: {2}   P1 Lv{3}   P2 Lv{4}",
                    game.SharedLives, game.SharedCoins, game.SharedScore, game.Players(0).WeaponLevel, game.Players(1).WeaponLevel)
            End If
            g.DrawString(txt, f, Brushes.White, 8, 6)

            If game.GameOver Then
                DrawCenteredBanner(g, "GAME OVER", Color.Red)
            ElseIf game.Victory Then
                DrawCenteredBanner(g, "CHIEN THANG!", Color.Gold)
            End If
        End Using
    End Sub

    Private Sub DrawCenteredBanner(g As Graphics, text As String, c As Color)
        Using f As New Font("Consolas", 28, FontStyle.Bold)
            Dim sz As SizeF = g.MeasureString(text, f)
            Dim x As Single = (PlatformGame.VIEW_WIDTH_PX - sz.Width) / 2.0F
            Dim y As Single = (PlatformGame.VIEW_HEIGHT_PX - sz.Height) / 2.0F
            Using b As New SolidBrush(c)
                g.DrawString(text, f, b, x, y)
            End Using
        End Using
    End Sub

    ' ===================== NAP SPRITE (tuy chon) =====================
    ' Tai su dung ten file cu (co san trong du an Contra) cho cac vai tro moi:
    ' player0/1 -> nhan vat choi, enemy_soldier -> quai di bo, enemy_boss -> trum cuoi,
    ' bullet_player/enemy -> fireball, powerup_weapon -> nam/hoa (dung chung 1 anh),
    ' powerup_life -> item 1-up. Neu khong tim thay file, tu dong fallback GDI+.
    Private Sub LoadSpritesIfExist()
        Dim dir As String = AppDomain.CurrentDomain.BaseDirectory
        Dim assetsDir As String = Path.Combine(dir, "Assets")

        sheetPlayer0 = TryLoadSheet(assetsDir, "player0_sheet.png", PlatformGame.PLAYER_W, PlatformGame.PLAYER_H)
        sheetPlayer1 = TryLoadSheet(assetsDir, "player1_sheet.png", PlatformGame.PLAYER_W, PlatformGame.PLAYER_H)
        sheetWalker = TryLoadSheet(assetsDir, "enemy_walker_sheet.png", PlatformGame.ENEMY_W, PlatformGame.ENEMY_H)
        sheetBoss = TryLoadSheet(assetsDir, "enemy_boss_sheet.png", PlatformGame.BOSS_W, PlatformGame.BOSS_H)

        spPlayer0 = TryLoad(assetsDir, "player0.png")
        spPlayer0Walk2 = TryLoad(assetsDir, "player0_walk2.png")
        spPlayer0Jump = TryLoad(assetsDir, "player0_jump.png")
        spPlayer1 = TryLoad(assetsDir, "player1.png")
        spPlayer1Walk2 = TryLoad(assetsDir, "player1_walk2.png")
        spPlayer1Jump = TryLoad(assetsDir, "player1_jump.png")
        spWalker = TryLoad(assetsDir, "enemy_soldier.png")
        spWalkerWalk2 = TryLoad(assetsDir, "enemy_soldier_walk2.png")
        spShelled = TryLoad(assetsDir, "enemy_shelled.png")
        spShell = TryLoad(assetsDir, "enemy_shell.png")
        spBoss = TryLoad(assetsDir, "enemy_boss.png")
        spBossWalk2 = TryLoad(assetsDir, "enemy_boss_walk2.png")
        spPlant = TryLoad(assetsDir, "enemy_plant.png")
        spFlag = TryLoad(assetsDir, "flag.png")
        spCastle = TryLoad(assetsDir, "castle.png")
        spPrincess = TryLoad(assetsDir, "princess.png")
        spGround = TryLoad(assetsDir, "tile_ground.png")
        spQuestionBlock = TryLoad(assetsDir, "tile_questionblock.png")
        spPipe = TryLoad(assetsDir, "tile_pipe.png")
        spFireball = TryLoad(assetsDir, "bullet_player.png")
        spEnemyFireball = TryLoad(assetsDir, "bullet_enemy.png")
        spPowerGrow = TryLoad(assetsDir, "powerup_weapon.png")
        spPowerFire = TryLoad(assetsDir, "powerup_weapon.png")
        spPowerLife = TryLoad(assetsDir, "powerup_life.png")
        spBackground = TryLoad(assetsDir, "background.png")
    End Sub

    Private Function TryLoadSheet(assetsDir As String, fileName As String, frameW As Integer, frameH As Integer) As SpriteSheet
        Dim result As New SpriteSheet()
        Dim bmp As Bitmap = TryLoad(assetsDir, fileName)
        If bmp Is Nothing Then Return result
        result.Sheet = bmp
        result.FrameW = frameW
        result.FrameH = frameH
        result.FrameCount = Math.Max(1, bmp.Width \ frameW)
        Return result
    End Function

    Private Function ChooseSheetFrame(sheet As SpriteSheet, onGround As Boolean, isMoving As Boolean) As Integer
        Dim idleIdx As Integer = 0
        Dim walk1Idx As Integer = Math.Min(1, sheet.FrameCount - 1)
        Dim walk2Idx As Integer = Math.Min(2, sheet.FrameCount - 1)
        Dim jumpIdx As Integer = Math.Min(3, sheet.FrameCount - 1)

        If Not onGround Then Return jumpIdx
        If isMoving Then Return If((frameCounter \ 6) Mod 2 = 0, walk1Idx, walk2Idx)
        Return idleIdx
    End Function

    Private Sub DrawSheetFrame(g As Graphics, sheet As SpriteSheet, frameIdx As Integer, destX As Integer, destY As Integer, drawW As Integer, drawH As Integer, flipLeft As Boolean)
        Dim clamped As Integer = Math.Max(0, Math.Min(frameIdx, sheet.FrameCount - 1))
        Dim srcRect As New Rectangle(clamped * sheet.FrameW, 0, sheet.FrameW, sheet.FrameH)

        If flipLeft Then
            Dim st As GraphicsState = g.Save()
            g.TranslateTransform(destX + drawW, destY)
            g.ScaleTransform(-1, 1)
            g.DrawImage(sheet.Sheet, New Rectangle(0, 0, drawW, drawH), srcRect, GraphicsUnit.Pixel)
            g.Restore(st)
        Else
            g.DrawImage(sheet.Sheet, New Rectangle(destX, destY, drawW, drawH), srcRect, GraphicsUnit.Pixel)
        End If
    End Sub

    Private Function TryLoad(assetsDir As String, fileName As String) As Bitmap
        Try
            Dim fullPath As String = Path.Combine(assetsDir, fileName)
            If File.Exists(fullPath) Then
                Return New Bitmap(fullPath)
            End If
        Catch ex As Exception
            ' Loi doc file: bo qua, dung fallback GDI+
        End Try
        Return Nothing
    End Function

End Class
