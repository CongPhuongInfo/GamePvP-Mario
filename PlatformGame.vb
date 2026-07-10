Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

''' <summary>
''' Logic game Phieu Luu Ong Nuoc Co-op (2 nguoi cung vuot man qua mang).
''' Lay cam hung tu the loai platformer nhay-dam-dau-quai kinh dien (khong dung ten/nhan vat
''' thuong hieu cua ben thu 3), ket hop them kha nang nem lua khi len Level lua.
''' Kien truc giu nguyen tinh than ContraGame.vb: Host la authoritative, chay toan bo simulation,
''' client chi gui INPUT, host broadcast STATE moi tick (giao thuc pipe-delimited qua NetworkPeer).
''' </summary>
Public Class PlatformGame

    ' ===================== HANG SO THE GIOI =====================
    Public Const TICK_MS As Integer = 33
    Public Const LEVEL_WIDTH_PX As Integer = 6400
    Public Const VIEW_WIDTH_PX As Integer = 960
    Public Const VIEW_HEIGHT_PX As Integer = 540
    Public Const GROUND_Y As Integer = 480

    Public Const GRAVITY As Double = 0.85
    Public Const MAX_FALL_SPEED As Double = 14.0
    Public Const MOVE_SPEED As Double = 4.2
    Public Const JUMP_VELOCITY As Double = -14.5
    Public Const STOMP_BOUNCE_VELOCITY As Double = -9.0
    Public Const PLAYER_W As Integer = 32
    Public Const PLAYER_H As Integer = 44

    Public Const ENEMY_W As Integer = 36
    Public Const ENEMY_H As Integer = 44
    Public Const BOSS_W As Integer = 88
    Public Const BOSS_H As Integer = 88

    Public Const FIREBALL_SPEED As Double = 7.0
    Public Const FIREBALL_GRAVITY As Double = 0.6
    Public Const FIREBALL_BOUNCE_VY As Double = -7.5
    Public Const FIREBALL_MAX_BOUNCES As Integer = 4
    Public Const FIREBALL_COOLDOWN As Integer = 20
    Public Const FIREBALL_MAX_PER_OWNER As Integer = 2

    Public Const SHELL_KICK_SPEED As Double = 6.5
    Public Const BLOCK_W As Integer = 32
    Public Const BLOCK_H As Integer = 32

    Public Const RESPAWN_INVULN_TICKS As Integer = 90
    Public Const HIT_INVULN_TICKS As Integer = 70
    Public Const SHARED_LIVES_START As Integer = 5
    Public Const COINS_PER_LIFE As Integer = 100

    Public Const ENEMY_MAX_ALIVE As Integer = 5
    Public Const ENEMY_SPAWN_CHECK_TICKS As Integer = 10

    Public Const PLANT_HIDDEN_TICKS As Integer = 60   ' thoi gian nam an trong ong
    Public Const PLANT_RISE_TICKS As Integer = 12      ' thoi gian tho len
    Public Const PLANT_POPPED_TICKS As Integer = 75    ' thoi gian lo ra ngoai (nguy hiem)
    Public Const PLANT_FALL_TICKS As Integer = 12       ' thoi gian thut xuong

    Public Const FLAGPOLE_X As Double = 6100
    Public Const FLAGPOLE_TOP_Y As Double = GROUND_Y - 200
    Public Const FLAGPOLE_BOTTOM_Y As Double = GROUND_Y
    Public Const CASTLE_X As Double = 6220
    Public Const WARP_DEST_X As Double = 4400 ' ong Warp gan dau man se day toi gan cuoi man

    ' ===================== ENUM =====================
    Public Enum EnemyType As Byte
        Walker = 0      ' quai di bo cham, dam dau la chet ngay (kieu "nam quai")
        Shelled = 1     ' quai co mai, dam dau lan 1 -> thu vao mai, dam/dung lan 2 -> da mai bay di
        Boss = 2        ' trum cuoi, nhieu mau, thinh thoang nha lua
        PipePlant = 3   ' cay trong ong nuoc, tho len/thut xuong theo chu ky, cham vao khi tho len la mat mang/suc manh
    End Enum

    Public Enum PowerUpType As Byte
        GrowMushroom = 0    ' -> WeaponLevel 0->1 (to len)
        FireFlower = 1      ' -> WeaponLevel ->2 (nem lua duoc)
        OneUp = 2           ' +1 mang chung
        Coin = 3            ' +1 xu chung (moc COINS_PER_LIFE xu se +1 mang)
    End Enum

    Public Enum PlatformKind As Byte
        Ground = 0          ' nen dac
        OneWay = 1          ' san bay: nhay xuyen tu duoi len duoc, chi chan khi roi tu tren
        QuestionBlock = 2   ' khoi "?" : dam dau tu duoi len se bung item ra (dung 1 lan)
        Brick = 3           ' gach thuong: chi de trang tri/chan, khong bung item
        Pipe = 4            ' ong nuoc trang tri, va cham nhu Ground
        WarpPipe = 5        ' ong nuoc chui vao duoc: dung tren + bam Xuong se dich chuyen den WarpDestX
    End Enum

    ' ===================== STRUCT =====================
    Public Structure PlayerState
        Public X As Double
        Public Y As Double
        Public VelY As Double
        Public FacingRight As Boolean
        Public OnGround As Boolean
        Public Alive As Boolean
        Public IsMoving As Boolean
        Public WeaponLevel As Integer      ' 0 = nho, 1 = to, 2 = lua (nem duoc fireball)
        Public InvulnTicks As Integer
        Public ShootCooldown As Integer
        Public RespawnTimer As Integer
    End Structure

    Public Structure FireballState
        Public X As Double
        Public Y As Double
        Public DirX As Double          ' van toc ngang co dinh
        Public VelY As Double          ' van toc doc, bi trong luc + nay
        Public Owner As Integer        ' 0/1 = player, -1 = boss
        Public Bounces As Integer
        Public Active As Boolean
    End Structure

    Public Structure EnemyState
        Public X As Double
        Public Y As Double
        Public VelX As Double
        Public Kind As EnemyType
        Public HP As Integer
        Public Alive As Boolean
        Public FacingRight As Boolean
        Public IsShell As Boolean      ' rieng Shelled: dang o dang mai (bat dong hoac lan)
        Public ShellMoving As Boolean  ' mai dang bi da lan di (nguy hiem cho quai khac + nguoi choi)
        Public ShootCooldown As Integer
        Public PatrolMinX As Double
        Public PatrolMaxX As Double
    End Structure

    Public Structure PowerUpState
        Public X As Double
        Public Y As Double
        Public VelY As Double
        Public Kind As PowerUpType
        Public Active As Boolean
        Public TtlTicks As Integer
    End Structure

    Public Structure PlatformRect
        Public X As Double
        Public Y As Double
        Public W As Double
        Public H As Double
        Public Kind As PlatformKind
        Public ItemInside As PowerUpType   ' chi dung cho QuestionBlock
        Public Used As Boolean             ' chi dung cho QuestionBlock (da bung item chua)
        Public WarpDestX As Double         ' chi dung cho WarpPipe: vi tri X se day nguoi choi den
    End Structure

    Private Structure EnemySpawnDef
        Public SpawnAtCamX As Double
        Public X As Double
        Public Y As Double
        Public Kind As EnemyType
        Public HP As Integer
        Public PatrolMinX As Double
        Public PatrolMaxX As Double
        Public Used As Boolean
    End Structure

    Public Structure PlayerInput
        Public Left As Boolean
        Public Right As Boolean
        Public Jump As Boolean
        Public Shoot As Boolean
        Public Down As Boolean     ' bam Xuong khi dung tren WarpPipe de chui vao ong
    End Structure

    ' ===================== STATE CHINH =====================
    Public Players(1) As PlayerState
    Public Fireballs As New List(Of FireballState)
    Public Enemies As New List(Of EnemyState)
    Public PowerUps As New List(Of PowerUpState)
    Public Platforms As New List(Of PlatformRect)

    Public CameraX As Double = 0
    Public SharedLives As Integer = SHARED_LIVES_START
    Public SharedCoins As Integer = 0
    Public SharedScore As Integer = 0
    Public GameOver As Boolean = False
    Public Victory As Boolean = False
    Public FlagpoleTouched As Boolean = False

    Private inputs(1) As PlayerInput
    Private spawnDefs As New List(Of EnemySpawnDef)
    Private rng As New Random()
    Private tickCount As Integer = 0

    ' ===================== KHOI TAO =====================
    ' Luu y: BuildLevel1() la thuat toan tat dinh (khong random), nen Host va Client
    ' deu tao ra Platforms/spawnDefs giong het nhau khi khoi tao game rieng. Vi vay giao thuc
    ' mang KHONG can gui lai toan bo Platforms, chi can dong bo co "Used" cua QuestionBlock.
    Public Sub New()
        BuildLevel1()
        Players(0) = MakeFreshPlayer(120, GROUND_Y - PLAYER_H)
        Players(1) = MakeFreshPlayer(60, GROUND_Y - PLAYER_H)
    End Sub

    ' Tat han nguoi choi thu 2 cho che do choi 1 minh: Alive=False vinh vien va
    ' RespawnTimer=0 nen UpdatePlayer(1) se khong bao gio hoi sinh lai no.
    Public Sub SetSoloMode()
        Dim p As PlayerState = Players(1)
        p.Alive = False
        p.RespawnTimer = 0
        Players(1) = p
    End Sub

    Private Function MakeFreshPlayer(x As Double, y As Double) As PlayerState
        Dim p As New PlayerState()
        p.X = x
        p.Y = y
        p.VelY = 0
        p.FacingRight = True
        p.OnGround = True
        p.Alive = True
        p.WeaponLevel = 0
        p.InvulnTicks = RESPAWN_INVULN_TICKS
        p.ShootCooldown = 0
        p.RespawnTimer = 0
        Return p
    End Function

    Private Sub BuildLevel1()
        Platforms.Clear()
        spawnDefs.Clear()

        Dim groundSegments As Double()() = New Double()() {
            New Double() {0, 1400},
            New Double() {1500, 2600},
            New Double() {2750, 4200},
            New Double() {4350, LEVEL_WIDTH_PX}
        }
        For Each seg In groundSegments
            AddPlatform(seg(0), GROUND_Y, seg(1) - seg(0), 60, PlatformKind.Ground)
        Next

        ' San bay nhieu tang
        AddPlatform(500, 380, 160, 16, PlatformKind.OneWay)
        AddPlatform(900, 320, 160, 16, PlatformKind.OneWay)
        AddPlatform(1900, 380, 200, 16, PlatformKind.OneWay)
        AddPlatform(3100, 360, 180, 16, PlatformKind.OneWay)
        AddPlatform(3500, 300, 160, 16, PlatformKind.OneWay)

        ' Ong nuoc trang tri (chan nhu Ground)
        AddPlatform(1460, GROUND_Y - 48, 60, 48, PlatformKind.Pipe)
        AddPlatform(2680, GROUND_Y - 64, 60, 64, PlatformKind.Pipe)
        AddPlatform(4280, GROUND_Y - 40, 60, 40, PlatformKind.Pipe)

        ' Cay trong ong: tho len/thut xuong theo chu ky (xem Case PipePlant trong UpdateEnemies)
        AddPipePlant(700, 1460, 60, GROUND_Y - 48)
        AddPipePlant(1900, 2680, 60, GROUND_Y - 64)
        AddPipePlant(3900, 4280, 60, GROUND_Y - 40)

        ' Ong Warp: dung tren + bam Xuong (S / mui ten Duoi) se "chui vao ong" va duoc day toi gan cuoi man
        AddWarpPipe(300, 48, GROUND_Y - 48, WARP_DEST_X)
        AddQuestionBlock(360, 340, PowerUpType.Coin)
        AddQuestionBlock(392, 340, PowerUpType.GrowMushroom)
        AddQuestionBlock(760, 280, PowerUpType.Coin)
        AddQuestionBlock(1950, 280, PowerUpType.FireFlower)
        AddQuestionBlock(3140, 280, PowerUpType.OneUp)
        AddQuestionBlock(3700, 240, PowerUpType.Coin)
        AddQuestionBlock(4620, 340, PowerUpType.FireFlower)

        ' Vai vien gach trang tri
        AddPlatform(328, 340, 96, BLOCK_H, PlatformKind.Brick)
        AddPlatform(700, 280, 96, BLOCK_H, PlatformKind.Brick)

        ' Dinh spawn quai theo tien do camera
        AddSpawn(300, 700, GROUND_Y - PLAYER_H, EnemyType.Walker, 1, 600, 900)
        AddSpawn(700, 1000, GROUND_Y - PLAYER_H, EnemyType.Shelled, 1, 950, 1300)
        AddSpawn(1200, 1350, GROUND_Y - PLAYER_H, EnemyType.Walker, 1, 1300, 1600)
        AddSpawn(1800, 2000, GROUND_Y - PLAYER_H, EnemyType.Walker, 1, 1950, 2300)
        AddSpawn(2200, 2400, GROUND_Y - PLAYER_H, EnemyType.Shelled, 1, 2350, 2600)
        AddSpawn(2900, 3050, GROUND_Y - PLAYER_H, EnemyType.Walker, 1, 3050, 3350)
        AddSpawn(3400, 3600, GROUND_Y - PLAYER_H, EnemyType.Shelled, 1, 3550, 3900)
        AddSpawn(4500, 4700, GROUND_Y - PLAYER_H, EnemyType.Walker, 1, 4650, 5000)
        ' Trum cuoi man
        AddSpawn(5600, 5900, GROUND_Y - BOSS_H, EnemyType.Boss, 6, 5900, 5900)

        ' Xu ran rac tren duong (nhat truc tiep, khong can dam khoi)
        PowerUps.Clear()
        AddPowerUp(650, GROUND_Y - 40, PowerUpType.Coin)
        AddPowerUp(680, GROUND_Y - 40, PowerUpType.Coin)
        AddPowerUp(2050, GROUND_Y - 40, PowerUpType.Coin)
        AddPowerUp(3150, 320, PowerUpType.Coin)
        AddPowerUp(4600, GROUND_Y - 40, PowerUpType.Coin)
    End Sub

    Private Sub AddPlatform(x As Double, y As Double, w As Double, h As Double, kind As PlatformKind)
        Dim rect As New PlatformRect()
        rect.X = x
        rect.Y = y
        rect.W = w
        rect.H = h
        rect.Kind = kind
        Platforms.Add(rect)
    End Sub

    Private Sub AddQuestionBlock(x As Double, y As Double, item As PowerUpType)
        Dim rect As New PlatformRect()
        rect.X = x
        rect.Y = y
        rect.W = BLOCK_W
        rect.H = BLOCK_H
        rect.Kind = PlatformKind.QuestionBlock
        rect.ItemInside = item
        rect.Used = False
        Platforms.Add(rect)
    End Sub

    ' Ong nuoc chui vao duoc: dung tren dinh ong roi bam Xuong se day nguoi choi den warpDestX.
    Private Sub AddWarpPipe(pipeX As Double, pipeW As Double, pipeTopY As Double, warpDestX As Double)
        Dim rect As New PlatformRect()
        rect.X = pipeX
        rect.Y = pipeTopY
        rect.W = pipeW
        rect.H = GROUND_Y - pipeTopY
        rect.Kind = PlatformKind.WarpPipe
        rect.WarpDestX = warpDestX
        Platforms.Add(rect)
    End Sub

    Private Sub AddSpawn(camTrigger As Double, x As Double, y As Double, kind As EnemyType, hp As Integer, patrolMin As Double, patrolMax As Double)
        Dim d As New EnemySpawnDef()
        d.SpawnAtCamX = camTrigger
        d.X = x
        d.Y = y
        d.Kind = kind
        d.HP = hp
        d.PatrolMinX = patrolMin
        d.PatrolMaxX = patrolMax
        d.Used = False
        spawnDefs.Add(d)
    End Sub

    ' Tao 1 cay trong ong: X can giua ong, Y-an = mep tren ong, Y-tho = nhoi len khoi ong 32px.
    Private Sub AddPipePlant(camTrigger As Double, pipeX As Double, pipeW As Double, pipeTopY As Double)
        Const PLANT_W As Double = 24
        Const PLANT_H As Double = 32
        Dim centerX As Double = pipeX + (pipeW - PLANT_W) / 2.0
        Dim hiddenY As Double = pipeTopY
        Dim poppedY As Double = pipeTopY - PLANT_H
        AddSpawn(camTrigger, centerX, hiddenY, EnemyType.PipePlant, 1, hiddenY, poppedY)
    End Sub

    Private Sub AddPowerUp(x As Double, y As Double, kind As PowerUpType)
        Dim p As New PowerUpState()
        p.X = x
        p.Y = y
        p.VelY = 0
        p.Kind = kind
        p.Active = True
        p.TtlTicks = -1
        PowerUps.Add(p)
    End Sub

    ' ===================== INPUT =====================
    Public Sub SetInput(playerIndex As Integer, inp As PlayerInput)
        If playerIndex < 0 OrElse playerIndex > 1 Then Return
        inputs(playerIndex) = inp
    End Sub

    ' ===================== VONG LAP CHINH (goi tu Timer ben Form1, chi tren HOST) =====================
    Public Sub Tick()
        If GameOver OrElse Victory Then Return
        tickCount += 1

        For i As Integer = 0 To 1
            UpdatePlayer(i)
        Next

        UpdateFireballs()
        UpdateEnemies()
        CheckPlayerEnemyCollisions()
        UpdatePowerUps()
        CheckSpawns()
        UpdateCamera()
        CheckFlagpole()
        CheckWinLose()
    End Sub

    Private Sub UpdatePlayer(idx As Integer)
        Dim p As PlayerState = Players(idx)
        Dim inp As PlayerInput = inputs(idx)

        If Not p.Alive Then
            If p.RespawnTimer > 0 Then
                p.RespawnTimer -= 1
                If p.RespawnTimer = 0 Then
                    Dim spawnX As Double = Math.Max(CameraX + 40, 40)
                    p.X = spawnX
                    p.Y = GROUND_Y - PLAYER_H
                    p.VelY = 0
                    p.Alive = True
                    p.InvulnTicks = RESPAWN_INVULN_TICKS
                    p.WeaponLevel = 0
                End If
            End If
            Players(idx) = p
            Return
        End If

        Dim moveX As Double = 0
        If inp.Left Then moveX -= MOVE_SPEED
        If inp.Right Then moveX += MOVE_SPEED
        If moveX > 0 Then p.FacingRight = True
        If moveX < 0 Then p.FacingRight = False
        p.IsMoving = (moveX <> 0)

        If inp.Jump AndAlso p.OnGround Then
            p.VelY = JUMP_VELOCITY
            p.OnGround = False
        End If

        p.VelY += GRAVITY
        If p.VelY > MAX_FALL_SPEED Then p.VelY = MAX_FALL_SPEED

        Dim oldY As Double = p.Y
        Dim newX As Double = p.X + moveX
        Dim newY As Double = p.Y + p.VelY
        newX = Math.Max(0, Math.Min(newX, CDbl(LEVEL_WIDTH_PX - PLAYER_W)))

        ResolvePlatformCollision(newX, newY, oldY, p)

        If inp.Down AndAlso p.OnGround Then
            TryEnterWarpPipe(p)
        End If

        If p.ShootCooldown > 0 Then p.ShootCooldown -= 1
        If inp.Shoot AndAlso p.ShootCooldown = 0 AndAlso p.WeaponLevel >= 2 Then
            FireFireball(idx, p)
            p.ShootCooldown = FIREBALL_COOLDOWN
        End If

        If p.InvulnTicks > 0 Then p.InvulnTicks -= 1

        Players(idx) = p
    End Sub

    ' Va cham voi platform (AABB truc doc): xu ly ca roi xuong (tu tren) va dam dau (tu duoi len).
    Private Sub ResolvePlatformCollision(newX As Double, newY As Double, oldY As Double, ByRef p As PlayerState)
        p.X = newX
        Dim landed As Boolean = False

        For pi As Integer = 0 To Platforms.Count - 1
            Dim plat As PlatformRect = Platforms(pi)
            Dim withinX As Boolean = (p.X + PLAYER_W > plat.X) AndAlso (p.X < plat.X + plat.W)
            If Not withinX Then Continue For

            Dim playerBottomOld As Double = oldY + PLAYER_H
            Dim playerBottomNew As Double = newY + PLAYER_H
            Dim playerTopOld As Double = oldY
            Dim playerTopNew As Double = newY

            Dim solidFromTop As Boolean = True ' tat ca cac loai platform hien co deu chan tu tren xuong

            If solidFromTop AndAlso p.VelY >= 0 AndAlso playerBottomOld <= plat.Y + 4 AndAlso playerBottomNew >= plat.Y Then
                newY = plat.Y - PLAYER_H
                p.VelY = 0
                landed = True
            ElseIf plat.Kind <> PlatformKind.OneWay Then
                ' Dam dau tu duoi len (chi ap dung cho khoi dac: Ground/QuestionBlock/Brick/Pipe)
                If p.VelY < 0 AndAlso playerTopOld >= plat.Y + plat.H - 4 AndAlso playerTopNew <= plat.Y + plat.H Then
                    newY = plat.Y + plat.H
                    p.VelY = 0.5
                    If plat.Kind = PlatformKind.QuestionBlock AndAlso Not plat.Used Then
                        Dim itemX As Double = plat.X + plat.W / 2.0 - 10
                        SpawnPoppedItem(itemX, plat.Y - 20, plat.ItemInside)
                        plat.Used = True
                        Platforms(pi) = plat
                    End If
                End If
            End If
        Next

        p.Y = newY
        p.OnGround = landed

        If p.Y > VIEW_HEIGHT_PX + 200 Then
            KillPlayer(p)
        End If
    End Sub

    Private Sub SpawnPoppedItem(x As Double, y As Double, kind As PowerUpType)
        Dim pu As New PowerUpState()
        pu.X = x
        pu.Y = y
        pu.VelY = -3.5 ' item bung len roi roi xuong
        pu.Kind = kind
        pu.Active = True
        pu.TtlTicks = If(kind = PowerUpType.Coin, 60, -1) ' xu tu bien mat, item khac nam lai cho nguoi choi nhat
        PowerUps.Add(pu)
    End Sub

    ' Neu nguoi choi dang dung dung tren dinh 1 WarpPipe, day ho toi WarpDestX (mo phong "chui vao ong").
    Private Sub TryEnterWarpPipe(ByRef p As PlayerState)
        For Each plat In Platforms
            If plat.Kind <> PlatformKind.WarpPipe Then Continue For
            Dim onTop As Boolean = (p.X + PLAYER_W > plat.X) AndAlso (p.X < plat.X + plat.W) AndAlso
                                    Math.Abs((p.Y + PLAYER_H) - plat.Y) < 6
            If onTop Then
                p.X = plat.WarpDestX
                p.Y = GROUND_Y - PLAYER_H
                p.VelY = 0
                Return
            End If
        Next
    End Sub

    Private Sub KillPlayer(ByRef p As PlayerState)
        If Not p.Alive Then Return
        p.Alive = False
        p.RespawnTimer = 60
        SharedLives -= 1
    End Sub

    ' Nguoi choi mat 1 muc suc manh khi trung don; ve 0 thi moi chet han.
    Private Sub DamagePlayer(ByRef p As PlayerState)
        If p.InvulnTicks > 0 Then Return
        If p.WeaponLevel > 0 Then
            p.WeaponLevel -= 1
            p.InvulnTicks = HIT_INVULN_TICKS
        Else
            KillPlayer(p)
        End If
    End Sub

    Private Sub FireFireball(idx As Integer, p As PlayerState)
        Dim activeCount As Integer = 0
        For Each fb In Fireballs
            If fb.Active AndAlso fb.Owner = idx Then activeCount += 1
        Next
        If activeCount >= FIREBALL_MAX_PER_OWNER Then Return

        Dim fireball As New FireballState()
        fireball.X = p.X + PLAYER_W / 2.0
        fireball.Y = p.Y + PLAYER_H / 2.0
        fireball.DirX = If(p.FacingRight, FIREBALL_SPEED, -FIREBALL_SPEED)
        fireball.VelY = 2.0
        fireball.Owner = idx
        fireball.Bounces = 0
        fireball.Active = True
        Fireballs.Add(fireball)
    End Sub

    Private Sub UpdateFireballs()
        For i As Integer = Fireballs.Count - 1 To 0 Step -1
            Dim fb As FireballState = Fireballs(i)
            If Not fb.Active Then
                Fireballs.RemoveAt(i)
                Continue For
            End If

            fb.VelY += FIREBALL_GRAVITY
            fb.X += fb.DirX
            fb.Y += fb.VelY

            ' Nay tren nen dat gan dung (dung GROUND_Y lam mat dat tham chieu cho don gian)
            If fb.Y >= GROUND_Y - 4 AndAlso fb.VelY > 0 Then
                fb.Y = GROUND_Y - 4
                fb.VelY = FIREBALL_BOUNCE_VY
                fb.Bounces += 1
                If fb.Bounces > FIREBALL_MAX_BOUNCES Then fb.Active = False
            End If

            If fb.X < CameraX - 100 OrElse fb.X > CameraX + VIEW_WIDTH_PX + 100 OrElse fb.Y > VIEW_HEIGHT_PX + 100 Then
                fb.Active = False
            End If

            If fb.Active Then
                If fb.Owner >= 0 Then
                    For e As Integer = 0 To Enemies.Count - 1
                        Dim en As EnemyState = Enemies(e)
                        If Not en.Alive Then Continue For
                        If en.Kind = EnemyType.PipePlant AndAlso Not en.IsShell Then Continue For
                        If RectHit(fb.X, fb.Y, en.X, en.Y, EnemyW(en.Kind), EnemyH(en.Kind)) Then
                            fb.Active = False
                            HandleEnemyHitByFireball(en)
                            Enemies(e) = en
                            Exit For
                        End If
                    Next
                Else
                    For pi As Integer = 0 To 1
                        Dim pl As PlayerState = Players(pi)
                        If Not pl.Alive OrElse pl.InvulnTicks > 0 Then Continue For
                        If RectHit(fb.X, fb.Y, pl.X, pl.Y, PLAYER_W, PLAYER_H) Then
                            fb.Active = False
                            DamagePlayer(pl)
                            Players(pi) = pl
                            Exit For
                        End If
                    Next
                End If
            End If

            Fireballs(i) = fb
        Next
    End Sub

    Private Sub HandleEnemyHitByFireball(ByRef en As EnemyState)
        en.HP -= 1
        If en.HP <= 0 Then
            en.Alive = False
            SharedScore += 100
            MaybeDropCoin(en.X, en.Y)
        End If
    End Sub

    Private Function EnemyW(kind As EnemyType) As Double
        Return If(kind = EnemyType.Boss, BOSS_W, ENEMY_W)
    End Function

    Private Function EnemyH(kind As EnemyType) As Double
        Return If(kind = EnemyType.Boss, BOSS_H, ENEMY_H)
    End Function

    Private Function RectHit(bx As Double, by As Double, rx As Double, ry As Double, rw As Double, rh As Double) As Boolean
        Return bx >= rx AndAlso bx <= rx + rw AndAlso by >= ry AndAlso by <= ry + rh
    End Function

    Private Sub MaybeDropCoin(x As Double, y As Double)
        If rng.Next(0, 100) < 35 Then
            SpawnPoppedItem(x, y, PowerUpType.Coin)
        End If
    End Sub

    Private Sub UpdateEnemies()
        For i As Integer = 0 To Enemies.Count - 1
            Dim en As EnemyState = Enemies(i)
            If Not en.Alive Then Continue For

            Select Case en.Kind
                Case EnemyType.Walker
                    en.X += en.VelX
                    If en.X <= en.PatrolMinX OrElse en.X >= en.PatrolMaxX Then en.VelX = -en.VelX
                    If en.VelX <> 0 Then en.FacingRight = (en.VelX > 0)

                Case EnemyType.Shelled
                    If en.IsShell Then
                        If en.ShellMoving Then
                            en.X += en.VelX
                            If en.X <= en.PatrolMinX - 300 OrElse en.X >= en.PatrolMaxX + 300 Then
                                en.VelX = -en.VelX
                            End If
                        End If
                        ' shell dung yen (chua bi da) khong di chuyen
                    Else
                        en.X += en.VelX
                        If en.X <= en.PatrolMinX OrElse en.X >= en.PatrolMaxX Then en.VelX = -en.VelX
                        If en.VelX <> 0 Then en.FacingRight = (en.VelX > 0)
                    End If

                Case EnemyType.Boss
                    en.X += en.VelX
                    If en.X <= en.PatrolMinX - 100 OrElse en.X >= en.PatrolMaxX + 100 Then en.VelX = -en.VelX
                    If en.VelX <> 0 Then en.FacingRight = (en.VelX > 0)

                    If en.ShootCooldown > 0 Then
                        en.ShootCooldown -= 1
                    Else
                        Dim target As PlayerState = NearestAlivePlayer(en.X, en.Y)
                        If target.Alive Then
                            Dim fireball As New FireballState()
                            fireball.X = en.X
                            fireball.Y = en.Y
                            fireball.DirX = If(target.X >= en.X, FIREBALL_SPEED * 0.8, -FIREBALL_SPEED * 0.8)
                            fireball.VelY = -3.0
                            fireball.Owner = -1
                            fireball.Bounces = 0
                            fireball.Active = True
                            Fireballs.Add(fireball)
                            en.ShootCooldown = 70
                        End If
                    End If

                Case EnemyType.PipePlant
                    ' Dung PatrolMinX/PatrolMaxX (khong dung cho patrol ngang o loai nay) lam
                    ' Y-an va Y-tho ra; ShootCooldown lam bo dem chu ky. Chi Host chay logic nay,
                    ' client chi nhan X/Y/IsShell da tinh san qua goi STATE nen khong can dong bo them.
                    Dim totalCycle As Integer = PLANT_HIDDEN_TICKS + PLANT_RISE_TICKS + PLANT_POPPED_TICKS + PLANT_FALL_TICKS
                    en.ShootCooldown -= 1
                    If en.ShootCooldown <= 0 Then en.ShootCooldown = totalCycle
                    Dim elapsed As Integer = totalCycle - en.ShootCooldown
                    Dim hiddenY As Double = en.PatrolMinX
                    Dim poppedY As Double = en.PatrolMaxX

                    If elapsed < PLANT_HIDDEN_TICKS Then
                        en.Y = hiddenY
                        en.IsShell = False
                    ElseIf elapsed < PLANT_HIDDEN_TICKS + PLANT_RISE_TICKS Then
                        Dim t As Double = (elapsed - PLANT_HIDDEN_TICKS) / CDbl(PLANT_RISE_TICKS)
                        en.Y = hiddenY + (poppedY - hiddenY) * t
                        en.IsShell = True
                    ElseIf elapsed < PLANT_HIDDEN_TICKS + PLANT_RISE_TICKS + PLANT_POPPED_TICKS Then
                        en.Y = poppedY
                        en.IsShell = True
                    Else
                        Dim t As Double = (elapsed - PLANT_HIDDEN_TICKS - PLANT_RISE_TICKS - PLANT_POPPED_TICKS) / CDbl(PLANT_FALL_TICKS)
                        en.Y = poppedY + (hiddenY - poppedY) * t
                        en.IsShell = True
                    End If
            End Select

            Enemies(i) = en
        Next

        ' Mai dang lan (ShellMoving) huy diet quai khac khi cham
        For i As Integer = 0 To Enemies.Count - 1
            Dim shell As EnemyState = Enemies(i)
            If Not shell.Alive OrElse Not (shell.Kind = EnemyType.Shelled AndAlso shell.IsShell AndAlso shell.ShellMoving) Then Continue For

            For j As Integer = 0 To Enemies.Count - 1
                If i = j Then Continue For
                Dim other As EnemyState = Enemies(j)
                If Not other.Alive Then Continue For
                If other.Kind = EnemyType.PipePlant AndAlso Not other.IsShell Then Continue For
                If RectHit(shell.X, shell.Y, other.X, other.Y, EnemyW(other.Kind), EnemyH(other.Kind)) Then
                    If other.Kind = EnemyType.Boss Then
                        other.HP -= 1
                        If other.HP <= 0 Then
                            other.Alive = False
                            SharedScore += 200
                        End If
                    Else
                        other.Alive = False
                        SharedScore += 100
                    End If
                    Enemies(j) = other
                End If
            Next
        Next
    End Sub

    Private Function NearestAlivePlayer(x As Double, y As Double) As PlayerState
        Dim best As PlayerState = Players(0)
        Dim bestDist As Double = Double.MaxValue
        Dim found As Boolean = False
        For i As Integer = 0 To 1
            If Players(i).Alive Then
                Dim d As Double = Math.Abs(Players(i).X - x)
                If d < bestDist Then
                    bestDist = d
                    best = Players(i)
                    found = True
                End If
            End If
        Next
        If Not found Then
            Dim dead As New PlayerState()
            dead.Alive = False
            Return dead
        End If
        Return best
    End Function

    ' Dam-dau (stomp) va va cham canh (side hit) giua nguoi choi va quai.
    Private Sub CheckPlayerEnemyCollisions()
        For pi As Integer = 0 To 1
            Dim p As PlayerState = Players(pi)
            If Not p.Alive Then Continue For

            For ei As Integer = 0 To Enemies.Count - 1
                Dim en As EnemyState = Enemies(ei)
                If Not en.Alive Then Continue For
                If en.Kind = EnemyType.PipePlant AndAlso Not en.IsShell Then Continue For

                Dim enW As Double = EnemyW(en.Kind)
                Dim enH As Double = EnemyH(en.Kind)
                Dim overlap As Boolean = (p.X + PLAYER_W > en.X) AndAlso (p.X < en.X + enW) AndAlso
                                          (p.Y + PLAYER_H > en.Y) AndAlso (p.Y < en.Y + enH)
                If Not overlap Then Continue For

                Dim isStomp As Boolean = (p.VelY > 0) AndAlso ((p.Y + PLAYER_H) - en.Y < enH * 0.6)

                If isStomp AndAlso en.Kind <> EnemyType.Boss AndAlso en.Kind <> EnemyType.PipePlant AndAlso Not (en.Kind = EnemyType.Shelled AndAlso en.IsShell AndAlso en.ShellMoving) Then
                    ' Dam dau -> tieu diet (hoac Shelled lan dau thu vao mai)
                    HandleStomp(en, p)
                    p.VelY = STOMP_BOUNCE_VELOCITY
                    Enemies(ei) = en
                ElseIf en.Kind = EnemyType.Shelled AndAlso en.IsShell Then
                    If en.ShellMoving Then
                        If isStomp Then
                            en.ShellMoving = False
                            en.VelX = 0
                            p.VelY = STOMP_BOUNCE_VELOCITY
                        Else
                            DamagePlayer(p)
                        End If
                    Else
                        ' Da mai: huong theo vi tri tuong doi cua nguoi choi
                        en.ShellMoving = True
                        en.VelX = If(p.X < en.X, SHELL_KICK_SPEED, -SHELL_KICK_SPEED)
                        SharedScore += 20
                    End If
                    Enemies(ei) = en
                ElseIf isStomp AndAlso en.Kind = EnemyType.Boss Then
                    en.HP -= 1
                    p.VelY = STOMP_BOUNCE_VELOCITY
                    If en.HP <= 0 Then
                        en.Alive = False
                        SharedScore += 500
                    End If
                    Enemies(ei) = en
                Else
                    DamagePlayer(p)
                End If
            Next

            Players(pi) = p
        Next
    End Sub

    Private Sub HandleStomp(ByRef en As EnemyState, p As PlayerState)
        Select Case en.Kind
            Case EnemyType.Walker
                en.Alive = False
                SharedScore += 100
                MaybeDropCoin(en.X, en.Y)
            Case EnemyType.Shelled
                If Not en.IsShell Then
                    en.IsShell = True
                    en.ShellMoving = False
                    en.VelX = 0
                    SharedScore += 100
                End If
        End Select
    End Sub

    Private Sub UpdatePowerUps()
        For i As Integer = PowerUps.Count - 1 To 0 Step -1
            Dim pu As PowerUpState = PowerUps(i)
            If Not pu.Active Then
                PowerUps.RemoveAt(i)
                Continue For
            End If

            ' Trong luc nhe cho item vua bung ra tu khoi "?", roi xuong dat va dung lai
            If pu.VelY <> 0 OrElse pu.Y < GROUND_Y - 20 Then
                pu.VelY += GRAVITY * 0.4
                pu.Y += pu.VelY
                If pu.Y > GROUND_Y - 20 Then
                    pu.Y = GROUND_Y - 20
                    pu.VelY = 0
                End If
            End If

            If pu.TtlTicks > 0 Then
                pu.TtlTicks -= 1
                If pu.TtlTicks = 0 Then pu.Active = False
            End If

            For pi As Integer = 0 To 1
                Dim pl As PlayerState = Players(pi)
                If Not pl.Alive Then Continue For
                If RectHit(pu.X, pu.Y, pl.X, pl.Y, PLAYER_W, PLAYER_H) Then
                    ApplyPowerUp(pl, pu.Kind)
                    Players(pi) = pl
                    pu.Active = False
                End If
            Next

            PowerUps(i) = pu
        Next
    End Sub

    Private Sub ApplyPowerUp(ByRef p As PlayerState, kind As PowerUpType)
        Select Case kind
            Case PowerUpType.GrowMushroom
                If p.WeaponLevel < 1 Then p.WeaponLevel = 1
            Case PowerUpType.FireFlower
                p.WeaponLevel = 2
            Case PowerUpType.OneUp
                SharedLives += 1
            Case PowerUpType.Coin
                SharedCoins += 1
                SharedScore += 10
                If SharedCoins Mod COINS_PER_LIFE = 0 Then SharedLives += 1
        End Select
    End Sub

    Private Sub CheckSpawns()
        If tickCount Mod ENEMY_SPAWN_CHECK_TICKS <> 0 Then Return
        Dim aliveCount As Integer = 0
        For Each en In Enemies
            If en.Alive Then aliveCount += 1
        Next
        If aliveCount >= ENEMY_MAX_ALIVE Then Return

        For i As Integer = 0 To spawnDefs.Count - 1
            Dim d As EnemySpawnDef = spawnDefs(i)
            If d.Used Then Continue For
            If CameraX + VIEW_WIDTH_PX >= d.SpawnAtCamX Then
                Dim en As New EnemyState()
                en.X = d.X
                en.Y = d.Y
                en.Kind = d.Kind
                en.HP = d.HP
                en.Alive = True
                en.FacingRight = True
                en.IsShell = False
                en.ShellMoving = False
                en.ShootCooldown = If(d.Kind = EnemyType.PipePlant,
                    PLANT_HIDDEN_TICKS + PLANT_RISE_TICKS + PLANT_POPPED_TICKS + PLANT_FALL_TICKS, 40)
                en.PatrolMinX = d.PatrolMinX
                en.PatrolMaxX = d.PatrolMaxX
                en.VelX = If(d.Kind = EnemyType.Boss, 0.8, 1.0)
                Enemies.Add(en)
                d.Used = True
                spawnDefs(i) = d
            End If
        Next
    End Sub

    Private Sub UpdateCamera()
        Dim maxX As Double = CameraX
        For i As Integer = 0 To 1
            If Players(i).Alive Then
                Dim desired As Double = Players(i).X - 200
                If desired > maxX Then maxX = desired
            End If
        Next
        maxX = Math.Min(maxX, CDbl(LEVEL_WIDTH_PX - VIEW_WIDTH_PX))
        CameraX = Math.Max(CameraX, Math.Max(0.0, maxX))

        For i As Integer = 0 To 1
            Dim p As PlayerState = Players(i)
            If p.Alive AndAlso p.X < CameraX Then
                p.X = CameraX
                Players(i) = p
            End If
        Next
    End Sub

    ' Cham vao cot co o cuoi man se ket thuc man thang cuoc (kieu Mario), diem thuong cang cao
    ' neu cham cot co o vi tri cang cao (nhay len cao truoc khi roi xuong cham cot).
    Private Sub CheckFlagpole()
        If FlagpoleTouched OrElse GameOver OrElse Victory Then Return
        For i As Integer = 0 To 1
            Dim p As PlayerState = Players(i)
            If p.Alive AndAlso p.X + PLAYER_W >= FLAGPOLE_X Then
                FlagpoleTouched = True
                Dim heightRatio As Double = 1.0 - Math.Max(0.0, Math.Min(1.0, (p.Y - FLAGPOLE_TOP_Y) / (FLAGPOLE_BOTTOM_Y - FLAGPOLE_TOP_Y)))
                SharedScore += CInt(200 + heightRatio * 800)
                Victory = True
                Exit For
            End If
        Next
    End Sub

    Private Sub CheckWinLose()
        If SharedLives <= 0 Then
            Dim bothDead As Boolean = (Not Players(0).Alive) AndAlso (Not Players(1).Alive)
            If bothDead Then GameOver = True
            Return
        End If

        Dim bossDead As Boolean = True
        Dim bossExists As Boolean = False
        For Each en In Enemies
            If en.Kind = EnemyType.Boss Then
                bossExists = True
                If en.Alive Then bossDead = False
            End If
        Next
        If bossExists AndAlso bossDead Then Victory = True
    End Sub

    ' ===================== SERIALIZE / DESERIALIZE (giao thuc mang) =====================
    ' STATE|camX|lives|coins|score|gameOver|victory|p0|p1|nFireballs|f1;f2;...|nEnemies|e1;e2;...|nPowerups|u1;u2;...|usedBlocksBits
    Public Function SerializeState() As String
        Dim sb As New StringBuilder()
        sb.Append("STATE|")
        sb.Append(CameraX.ToString("F1")).Append("|")
        sb.Append(SharedLives.ToString()).Append("|")
        sb.Append(SharedCoins.ToString()).Append("|")
        sb.Append(SharedScore.ToString()).Append("|")
        sb.Append(If(GameOver, "1", "0")).Append("|")
        sb.Append(If(Victory, "1", "0")).Append("|")
        sb.Append(SerializePlayer(Players(0))).Append("|")
        sb.Append(SerializePlayer(Players(1))).Append("|")

        sb.Append(Fireballs.Count.ToString()).Append("|")
        For i As Integer = 0 To Fireballs.Count - 1
            If i > 0 Then sb.Append(";")
            Dim fb As FireballState = Fireballs(i)
            sb.Append(String.Format(Globalization.CultureInfo.InvariantCulture, "{0:F1},{1:F1},{2:F1},{3:F1},{4}", fb.X, fb.Y, fb.DirX, fb.VelY, fb.Owner))
        Next
        sb.Append("|")

        sb.Append(Enemies.Count.ToString()).Append("|")
        For i As Integer = 0 To Enemies.Count - 1
            If i > 0 Then sb.Append(";")
            Dim en As EnemyState = Enemies(i)
            sb.Append(String.Format(Globalization.CultureInfo.InvariantCulture, "{0:F1},{1:F1},{2},{3},{4},{5},{6},{7}",
                en.X, en.Y, CInt(en.Kind), en.HP, If(en.Alive, 1, 0), If(en.FacingRight, 1, 0), If(en.IsShell, 1, 0), If(en.ShellMoving, 1, 0)))
        Next
        sb.Append("|")

        sb.Append(PowerUps.Count.ToString()).Append("|")
        For i As Integer = 0 To PowerUps.Count - 1
            If i > 0 Then sb.Append(";")
            Dim pu As PowerUpState = PowerUps(i)
            sb.Append(String.Format(Globalization.CultureInfo.InvariantCulture, "{0:F1},{1:F1},{2}", pu.X, pu.Y, CInt(pu.Kind)))
        Next
        sb.Append("|")

        Dim bits As New StringBuilder()
        For Each plat In Platforms
            bits.Append(If(plat.Kind = PlatformKind.QuestionBlock AndAlso plat.Used, "1"c, "0"c))
        Next
        sb.Append(bits.ToString())
        sb.Append("|").Append(If(FlagpoleTouched, "1", "0"))

        Return sb.ToString()
    End Function

    Private Function SerializePlayer(p As PlayerState) As String
        Return String.Format(Globalization.CultureInfo.InvariantCulture, "{0:F1},{1:F1},{2},{3},{4},{5},{6},{7},{8}",
            p.X, p.Y,
            If(p.FacingRight, 1, 0),
            If(p.OnGround, 1, 0),
            If(p.Alive, 1, 0),
            p.WeaponLevel,
            p.InvulnTicks,
            p.RespawnTimer,
            If(p.IsMoving, 1, 0))
    End Function

    Public Sub ApplyStateLine(line As String)
        Dim parts As String() = line.Split("|"c)
        If parts.Length < 12 Then Return
        If parts(0) <> "STATE" Then Return

        Dim ic As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
        CameraX = Double.Parse(parts(1), ic)
        SharedLives = Integer.Parse(parts(2))
        SharedCoins = Integer.Parse(parts(3))
        SharedScore = Integer.Parse(parts(4))
        GameOver = (parts(5) = "1")
        Victory = (parts(6) = "1")
        Players(0) = ParsePlayer(parts(7))
        Players(1) = ParsePlayer(parts(8))

        Dim nFireballs As Integer = Integer.Parse(parts(9))
        Fireballs.Clear()
        If nFireballs > 0 Then
            For Each item In parts(10).Split(";"c)
                Fireballs.Add(ParseFireball(item))
            Next
        End If

        Dim nEnemies As Integer = Integer.Parse(parts(11))
        Enemies.Clear()
        If nEnemies > 0 AndAlso parts.Length > 12 Then
            For Each item In parts(12).Split(";"c)
                Enemies.Add(ParseEnemy(item))
            Next
        End If

        If parts.Length > 14 Then
            Dim nPowerups As Integer = Integer.Parse(parts(13))
            PowerUps.Clear()
            If nPowerups > 0 Then
                For Each item In parts(14).Split(";"c)
                    PowerUps.Add(ParsePowerUp(item))
                Next
            End If
        End If

        If parts.Length > 15 Then
            Dim bits As String = parts(15)
            For i As Integer = 0 To Math.Min(bits.Length, Platforms.Count) - 1
                If bits(i) = "1"c Then
                    Dim plat As PlatformRect = Platforms(i)
                    plat.Used = True
                    Platforms(i) = plat
                End If
            Next
        End If

        If parts.Length > 16 Then
            FlagpoleTouched = (parts(16) = "1")
        End If
    End Sub

    Private Function ParsePlayer(s As String) As PlayerState
        Dim f As String() = s.Split(","c)
        Dim ic As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
        Dim p As New PlayerState()
        p.X = Double.Parse(f(0), ic)
        p.Y = Double.Parse(f(1), ic)
        p.FacingRight = (f(2) = "1")
        p.OnGround = (f(3) = "1")
        p.Alive = (f(4) = "1")
        p.WeaponLevel = Integer.Parse(f(5))
        p.InvulnTicks = Integer.Parse(f(6))
        p.RespawnTimer = Integer.Parse(f(7))
        p.IsMoving = If(f.Length > 8, f(8) = "1", False)
        Return p
    End Function

    Private Function ParseFireball(s As String) As FireballState
        Dim f As String() = s.Split(","c)
        Dim ic As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
        Dim fb As New FireballState()
        fb.X = Double.Parse(f(0), ic)
        fb.Y = Double.Parse(f(1), ic)
        fb.DirX = Double.Parse(f(2), ic)
        fb.VelY = Double.Parse(f(3), ic)
        fb.Owner = Integer.Parse(f(4))
        fb.Active = True
        Return fb
    End Function

    Private Function ParseEnemy(s As String) As EnemyState
        Dim f As String() = s.Split(","c)
        Dim ic As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
        Dim en As New EnemyState()
        en.X = Double.Parse(f(0), ic)
        en.Y = Double.Parse(f(1), ic)
        en.Kind = CType(Integer.Parse(f(2)), EnemyType)
        en.HP = Integer.Parse(f(3))
        en.Alive = (f(4) = "1")
        en.FacingRight = If(f.Length > 5, f(5) = "1", True)
        en.IsShell = If(f.Length > 6, f(6) = "1", False)
        en.ShellMoving = If(f.Length > 7, f(7) = "1", False)
        Return en
    End Function

    Private Function ParsePowerUp(s As String) As PowerUpState
        Dim f As String() = s.Split(","c)
        Dim ic As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
        Dim pu As New PowerUpState()
        pu.X = Double.Parse(f(0), ic)
        pu.Y = Double.Parse(f(1), ic)
        pu.Kind = CType(Integer.Parse(f(2)), PowerUpType)
        pu.Active = True
        Return pu
    End Function

    Public Shared Function SerializeInput(inp As PlayerInput) As String
        Return String.Format("INPUT|{0}|{1}|{2}|{3}|{4}",
            If(inp.Left, 1, 0), If(inp.Right, 1, 0),
            If(inp.Jump, 1, 0), If(inp.Shoot, 1, 0), If(inp.Down, 1, 0))
    End Function

    Public Shared Function ParseInput(line As String) As PlayerInput
        Dim inp As New PlayerInput()
        Dim parts As String() = line.Split("|"c)
        If parts.Length < 5 OrElse parts(0) <> "INPUT" Then Return inp
        inp.Left = (parts(1) = "1")
        inp.Right = (parts(2) = "1")
        inp.Jump = (parts(3) = "1")
        inp.Shoot = (parts(4) = "1")
        inp.Down = (parts.Length > 5 AndAlso parts(5) = "1")
        Return inp
    End Function

End Class
