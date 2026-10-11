namespace ClawGuard
{
    public static class AppXaml
    {
        public const string Xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title="""" Height=""498"" Width=""390""
        WindowStartupLocation=""CenterScreen"" ResizeMode=""CanMinimize""
        Background=""#14141c"" Foreground=""White""
        FontFamily=""Segoe UI"">
    <Window.Resources>

        <Style x:Key=""Card"" TargetType=""Border"">
            <Setter Property=""Background"" Value=""#1a1a28""/>
            <Setter Property=""CornerRadius"" Value=""10""/>
            <Setter Property=""Padding"" Value=""14,10""/>
            <Setter Property=""Margin"" Value=""0,0,0,8""/>
            <Setter Property=""BorderBrush"" Value=""#292942""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
        </Style>

        <Style x:Key=""Lbl"" TargetType=""TextBlock"">
            <Setter Property=""FontSize"" Value=""9.5""/>
            <Setter Property=""Foreground"" Value=""#656592""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Setter Property=""Margin"" Value=""0,0,0,2""/>
        </Style>

        <Style x:Key=""Val"" TargetType=""TextBlock"">
            <Setter Property=""FontSize"" Value=""13.5""/>
            <Setter Property=""Foreground"" Value=""#dedef8""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
        </Style>

        <Style x:Key=""Btn"" TargetType=""Button"">
            <Setter Property=""Height"" Value=""34""/>
            <Setter Property=""FontSize"" Value=""12.5""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Setter Property=""Foreground"" Value=""White""/>
            <Setter Property=""BorderThickness"" Value=""0""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""bd"" Background=""{TemplateBinding Background}""
                                CornerRadius=""8"" Padding=""12,0"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.82""/>
                            </Trigger>
                            <Trigger Property=""IsEnabled"" Value=""False"">
                                <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.35""/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key=""HeaderBtn"" TargetType=""Button"">
            <Setter Property=""Height"" Value=""23""/>
            <Setter Property=""FontSize"" Value=""10.5""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
            <Setter Property=""Foreground"" Value=""#b0b0dc""/>
            <Setter Property=""Background"" Value=""#252538""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Margin"" Value=""0,0,6,0""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""bdH"" Background=""{TemplateBinding Background}"" CornerRadius=""4"" Padding=""7,2"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""bdH"" Property=""Background"" Value=""#3a3a58""/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern Stylized Toggle Switch for Settings Flags -->
        <Style x:Key=""ModernToggle"" TargetType=""CheckBox"">
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""FontSize"" Value=""11""/>
            <Setter Property=""Foreground"" Value=""#dedef8""/>
            <Setter Property=""Margin"" Value=""0,0,0,10""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""CheckBox"">
                        <Grid Background=""Transparent"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto""/>
                                <ColumnDefinition Width=""*""/>
                            </Grid.ColumnDefinitions>

                            <Border x:Name=""toggleTrack"" Grid.Column=""0"" Width=""36"" Height=""19"" CornerRadius=""9.5""
                                    Background=""#232336"" BorderBrush=""#383854"" BorderThickness=""1""
                                    Margin=""0,0,10,0"" VerticalAlignment=""Center"">
                                <Canvas Width=""34"" Height=""17"" ClipToBounds=""False"">
                                    <Ellipse x:Name=""toggleKnob"" Width=""13"" Height=""13"" Fill=""#727295""
                                             Canvas.Left=""2"" Canvas.Top=""2""/>
                                </Canvas>
                            </Border>

                            <ContentPresenter Grid.Column=""1"" VerticalAlignment=""Center"" RecognizesAccessKey=""False""/>
                        </Grid>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsChecked"" Value=""True"">
                                <Setter TargetName=""toggleTrack"" Property=""Background"" Value=""#D97757""/>
                                <Setter TargetName=""toggleTrack"" Property=""BorderBrush"" Value=""#f08866""/>
                                <Setter TargetName=""toggleKnob"" Property=""Fill"" Value=""#FFFFFF""/>
                                <Setter TargetName=""toggleKnob"" Property=""Canvas.Left"" Value=""19""/>
                            </Trigger>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""toggleTrack"" Property=""Opacity"" Value=""0.88""/>
                            </Trigger>
                            <Trigger Property=""IsEnabled"" Value=""False"">
                                <Setter Property=""Opacity"" Value=""0.45""/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

    </Window.Resources>

    <Grid>
        <!-- ── Layer 0: Main Application View (Lifted Up to Top) ── -->
        <StackPanel x:Name=""mainPanel"" Margin=""14,2,14,10"">

            <!-- Top Header: Logo, Title, [CFG], [LOG], [RU], Status -->
            <Grid Margin=""0,0,0,8"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>

                <!-- App Title with Terracotta Crab Mascot -->
                <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                    <Border Width=""22"" Height=""17"" Margin=""0,0,8,0"" Background=""#281a17"" CornerRadius=""3"" VerticalAlignment=""Center"" BorderBrush=""#503028"" BorderThickness=""1"">
                        <Image x:Name=""imgTitleLogo"" Width=""16"" Height=""13"" Stretch=""Uniform"" RenderOptions.BitmapScalingMode=""NearestNeighbor""/>
                    </Border>
                    <TextBlock x:Name=""txtAppTitle"" Text=""ClawGuard"" FontSize=""18"" FontWeight=""Bold"" Foreground=""#D97757"" VerticalAlignment=""Center""/>
                </StackPanel>

                <!-- Actions: [⚙ CFG] [📋 LOG] [RU] [Status Dot] -->
                <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                    <Button x:Name=""btnConfig"" Style=""{StaticResource HeaderBtn}"" Content=""⚙ CFG"" ToolTip=""Settings / Настройки""/>
                    <Button x:Name=""btnLog""    Style=""{StaticResource HeaderBtn}"" Content=""📋 LOG"" ToolTip=""Live Log / Журнал""/>
                    <Button x:Name=""btnLang""   Style=""{StaticResource HeaderBtn}"" Margin=""0,0,8,0"">
                        <TextBlock x:Name=""txtLangBtn"" Text=""RU"" FontWeight=""Bold"" Foreground=""#e0af85""/>
                    </Button>

                    <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                        <Ellipse x:Name=""statusDot"" Width=""8"" Height=""8"" Fill=""#444466"" Margin=""0,0,4,0"" VerticalAlignment=""Center""/>
                        <TextBlock x:Name=""txtStatusShort"" Text=""—"" FontSize=""11"" Foreground=""#606090"" VerticalAlignment=""Center""/>
                    </StackPanel>
                </StackPanel>
            </Grid>

            <!-- Network Info Card: IP on Left, Country + ISP on Right -->
            <Border Style=""{StaticResource Card}"">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""135""/>
                        <ColumnDefinition Width=""12""/>
                        <ColumnDefinition Width=""*""/>
                    </Grid.ColumnDefinitions>

                    <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                        <TextBlock x:Name=""lblPublicIP"" Style=""{StaticResource Lbl}"" Text=""IP-АДРЕС""/>
                        <TextBlock x:Name=""txtIP"" Style=""{StaticResource Val}"" Text=""—""/>
                    </StackPanel>

                    <Rectangle Grid.Column=""1"" Width=""1"" Fill=""#282845"" HorizontalAlignment=""Center"" Margin=""0,2""/>

                    <StackPanel Grid.Column=""2"" VerticalAlignment=""Center"" Margin=""2,0,0,0"">
                        <TextBlock x:Name=""lblCountry"" Style=""{StaticResource Lbl}"" Text=""СТРАНА""/>
                        <StackPanel Orientation=""Horizontal"" Margin=""0,1,0,5"" VerticalAlignment=""Center"">
                            <Border x:Name=""bdFlag"" CornerRadius=""2"" Margin=""0,0,6,0"" Visibility=""Collapsed"" VerticalAlignment=""Center"" ClipToBounds=""True"" BorderBrush=""#353555"" BorderThickness=""1"">
                                <Image x:Name=""imgFlag"" Width=""19"" Height=""13"" Stretch=""UniformToFill"" RenderOptions.BitmapScalingMode=""HighQuality""/>
                            </Border>
                            <TextBlock x:Name=""txtCountry"" Style=""{StaticResource Val}"" Text=""—"" FontSize=""12.5"" VerticalAlignment=""Center"" TextTrimming=""CharacterEllipsis""/>
                        </StackPanel>

                        <TextBlock x:Name=""lblISP"" Style=""{StaticResource Lbl}"" Text=""ПРОВАЙДЕР""/>
                        <TextBlock x:Name=""txtISP"" Foreground=""#9292bd"" FontSize=""11.5"" Text=""—"" TextTrimming=""CharacterEllipsis""/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- Pinned IP Card -->
            <Border Style=""{StaticResource Card}"">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""8""/>
                        <ColumnDefinition Width=""Auto""/>
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                        <TextBlock x:Name=""lblPinnedIP"" Style=""{StaticResource Lbl}"" Text=""ЗАКРЕПЛЁННЫЙ IP""/>
                        <TextBlock x:Name=""txtPinnedIP"" Style=""{StaticResource Val}"" Text=""не задан"" Foreground=""#e05050"" FontSize=""12.5""/>
                    </StackPanel>
                    <Button x:Name=""btnPin"" Grid.Column=""2"" Style=""{StaticResource Btn}""
                            Background=""#5a3fbf"" Content=""Закрепить"" MinWidth=""112"" Padding=""12,0""/>
                </Grid>
            </Border>

            <!-- Guard Status Bar -->
            <Border Style=""{StaticResource Card}"" Padding=""13,8"">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""Auto""/>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""Auto""/>
                    </Grid.ColumnDefinitions>
                    <TextBlock x:Name=""txtGuardDot"" Text=""○"" FontSize=""14"" Foreground=""#e05050"" VerticalAlignment=""Center"" Margin=""0,0,7,0""/>
                    <TextBlock x:Name=""txtGuardStatus"" Grid.Column=""1"" Text=""Свитчер остановлен"" Foreground=""#8f8fba"" FontSize=""12"" VerticalAlignment=""Center""/>
                    <TextBlock x:Name=""txtProtecting"" Grid.Column=""2"" Text="""" Foreground=""#505072"" FontSize=""10.5"" VerticalAlignment=""Center""/>
                </Grid>
            </Border>

            <!-- Start / Stop Controls -->
            <Grid Margin=""0,0,0,6"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""8""/>
                    <ColumnDefinition Width=""*""/>
                </Grid.ColumnDefinitions>
                <Button x:Name=""btnStart"" Grid.Column=""0"" Style=""{StaticResource Btn}"" Background=""#1d9766"" Content=""Запустить"" IsEnabled=""False""/>
                <Button x:Name=""btnStop""  Grid.Column=""2"" Style=""{StaticResource Btn}"" Background=""#9b204e"" Content=""Остановить"" IsEnabled=""False""/>
            </Grid>

            <!-- Launch Claude Button -->
            <Button x:Name=""btnClaude"" Style=""{StaticResource Btn}"" Background=""#7a54ec"" Content=""Запустить Claude"" IsEnabled=""False"" Height=""36"" FontSize=""13""/>

            <!-- ── Claude Terminal Bottom Panel with Treadmill Track ── -->
            <Border Background=""#0f0f16"" CornerRadius=""8"" Padding=""10,6"" Margin=""0,8,0,0"" BorderBrush=""#252538"" BorderThickness=""1"">
                <StackPanel>
                    <!-- Exact Official Segmented CLAUDE Logo Header -->
                    <Grid Margin=""0,1,0,3"">
                        <Image x:Name=""imgClaudeLogo"" Height=""22"" Stretch=""Uniform"" HorizontalAlignment=""Left"" RenderOptions.BitmapScalingMode=""HighQuality""/>
                    </Grid>

                    <!-- Treadmill Track with Stepping Crab & Dynamic Sky -->
                    <Canvas x:Name=""trackCanvas"" Height=""52"" ClipToBounds=""True"" Background=""Transparent"">
                        <!-- 1. Day Sky Scenery Layer -->
                        <Canvas x:Name=""dayLayer"" Visibility=""Collapsed"">
                            <Canvas x:Name=""dayCloud1"" Canvas.Left=""14"" Canvas.Top=""8""/>
                            <Canvas x:Name=""dayCloud2"" Canvas.Left=""190"" Canvas.Top=""3""/>
                            <Canvas x:Name=""sunHost"" Canvas.Left=""270"" Canvas.Top=""4""/>
                        </Canvas>

                        <!-- 2. Night Sky Scenery Layer -->
                        <Canvas x:Name=""nightLayer"" Visibility=""Visible"">
                            <TextBlock x:Name=""star1"" Text=""*"" FontFamily=""Consolas"" FontSize=""10"" Foreground=""#444470"" Canvas.Left=""28"" Canvas.Top=""4""/>
                            <TextBlock x:Name=""star2"" Text=""*"" FontFamily=""Consolas"" FontSize=""11"" Foreground=""#323255"" Canvas.Left=""85"" Canvas.Top=""12""/>
                            <TextBlock x:Name=""star3"" Text=""*"" FontFamily=""Consolas"" FontSize=""9""  Foreground=""#444470"" Canvas.Left=""135"" Canvas.Top=""5""/>
                            <TextBlock x:Name=""star4"" Text=""*"" FontFamily=""Consolas"" FontSize=""10"" Foreground=""#323255"" Canvas.Left=""305"" Canvas.Top=""10""/>
                            <Canvas x:Name=""nightCloud1"" Canvas.Left=""14"" Canvas.Top=""8""/>
                            <Canvas x:Name=""nightCloud2"" Canvas.Left=""190"" Canvas.Top=""3""/>
                            <Canvas x:Name=""moonHost"" Canvas.Left=""270"" Canvas.Top=""4""/>
                        </Canvas>

                        <!-- 3. Sinister IP Leak Scenery Layer (Blood Moon & Storm Cloud) -->
                        <Canvas x:Name=""sinisterLayer"" Visibility=""Collapsed"">
                            <Canvas x:Name=""stormCloud"" Canvas.Left=""18"" Canvas.Top=""8""/>
                            <Canvas x:Name=""bloodMoonHost"" Canvas.Left=""270"" Canvas.Top=""4""/>
                        </Canvas>

                        <!-- Ground Track Lines (Normal & Severed) -->
                        <Line x:Name=""trackLine"" X1=""-20"" Y1=""48"" X2=""380"" Y2=""48"" Stroke=""#3a3a58"" StrokeThickness=""1.5"" StrokeDashArray=""2,3""/>
                        <Line x:Name=""brokenTrackLine"" X1=""-20"" Y1=""48"" X2=""380"" Y2=""48"" Stroke=""#3a3a58"" StrokeThickness=""1.5"" StrokeDashArray=""8,10,2,12"" Visibility=""Collapsed""/>

                        <!-- Stepping Pixel Crab Host (Stationary in Center) -->
                        <Canvas x:Name=""crabHost"" Width=""36"" Height=""28"" Canvas.Left=""152"" Canvas.Top=""22""/>

                        <!-- Floating ZZZ for sleeping state -->
                        <TextBlock x:Name=""txtZzz"" Text=""z Z Z"" FontFamily=""Consolas, Cascadia Code"" FontSize=""11"" FontWeight=""Bold"" Foreground=""#7e7eb0"" Canvas.Left=""192"" Canvas.Top=""18"" Visibility=""Collapsed""/>
                    </Canvas>

                    <!-- Terminal Monospace Status Banner -->
                    <Border Background=""#151520"" CornerRadius=""4"" Padding=""5,2"" Margin=""0,3,0,0"">
                        <TextBlock x:Name=""txtCrabStatus"" Text=""[ 💤 СВИТЧЕР ВЫКЛЮЧЕН • КРАБ СПИТ ]""
                                   FontFamily=""Consolas, Cascadia Code, Courier New""
                                   FontSize=""10"" FontWeight=""SemiBold""
                                   Foreground=""#8888aa"" HorizontalAlignment=""Center""/>
                    </Border>
                </StackPanel>
            </Border>

        </StackPanel>

        <!-- ── Layer 1: Semi-Transparent Backdrop Overlay (Click to Dismiss) ── -->
        <Border x:Name=""drawerOverlay"" Background=""#750a0a14"" Visibility=""Collapsed""/>

        <!-- ── Layer 2: Slide-Out Side Menu (Drawer) from Right Side ── -->
        <Border x:Name=""sideDrawer"" HorizontalAlignment=""Right"" Width=""335""
                Background=""#161624"" BorderBrush=""#2e2e48"" BorderThickness=""1,0,0,0""
                Visibility=""Collapsed"">
            <Grid Margin=""14,12,14,12"">
                <Grid.RowDefinitions>
                    <RowDefinition Height=""Auto""/>
                    <RowDefinition Height=""*""/>
                </Grid.RowDefinitions>

                <!-- Drawer Header -->
                <Grid Grid.Row=""0"" Margin=""0,0,0,12"">
                    <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                        <TextBlock x:Name=""txtDrawerIcon"" Text=""⚙"" FontSize=""14"" Foreground=""#D97757"" Margin=""0,0,6,0"" VerticalAlignment=""Center""/>
                        <TextBlock x:Name=""txtDrawerTitle"" Text=""Настройки"" FontSize=""14"" FontWeight=""Bold"" Foreground=""#D97757"" VerticalAlignment=""Center""/>
                    </StackPanel>
                    <Button x:Name=""btnDrawerClose"" Content=""✕"" Width=""26"" Height=""26"" HorizontalAlignment=""Right""
                            Background=""#252538"" Foreground=""#c0c0e0"" BorderThickness=""0"" Cursor=""Hand"" FontWeight=""Bold"" FontSize=""12"">
                        <Button.Template>
                            <ControlTemplate TargetType=""Button"">
                                <Border x:Name=""bdClose"" Background=""{TemplateBinding Background}"" CornerRadius=""4"">
                                    <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                                </Border>
                                <ControlTemplate.Triggers>
                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                        <Setter TargetName=""bdClose"" Property=""Background"" Value=""#3a3a58""/>
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Button.Template>
                    </Button>
                </Grid>

                <!-- Drawer Body Views -->
                <Grid Grid.Row=""1"">

                    <!-- 1. Settings View with Styled Modern Toggles -->
                    <StackPanel x:Name=""viewConfig"" Visibility=""Visible"">
                        <TextBlock x:Name=""lblCfgPinnedIP"" Text=""Закреплённый IP:"" FontSize=""10"" Foreground=""#8585aa"" Margin=""0,0,0,3""/>
                        <Grid Margin=""0,0,0,10"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""*""/>
                                <ColumnDefinition Width=""6""/>
                                <ColumnDefinition Width=""Auto""/>
                            </Grid.ColumnDefinitions>
                            <TextBox x:Name=""inPinnedIP"" Background=""#1c1c28"" Foreground=""White"" BorderBrush=""#353550"" Height=""28"" Padding=""6,3"" VerticalContentAlignment=""Center""/>
                            <Button x:Name=""btnCfgUseCurrent"" Grid.Column=""2"" Content=""Текущий"" Height=""28"" Padding=""8,0""
                                    Background=""#353554"" Foreground=""#e0e0ff"" BorderThickness=""0"" Cursor=""Hand"" FontSize=""11""/>
                        </Grid>

                        <TextBlock x:Name=""lblCfgProcess"" Text=""Защищаемый процесс:"" FontSize=""10"" Foreground=""#8585aa"" Margin=""0,0,0,3""/>
                        <TextBox x:Name=""inProcess"" Background=""#1c1c28"" Foreground=""White"" BorderBrush=""#353550"" Height=""28"" Padding=""6,3"" Margin=""0,0,0,10"" VerticalContentAlignment=""Center""/>

                        <TextBlock x:Name=""lblCfgInterval"" Text=""Интервал проверки (сек):"" FontSize=""10"" Foreground=""#8585aa"" Margin=""0,0,0,3""/>
                        <TextBox x:Name=""inInterval"" Background=""#1c1c28"" Foreground=""White"" BorderBrush=""#353550"" Height=""28"" Padding=""6,3"" Margin=""0,0,0,14"" VerticalContentAlignment=""Center""/>

                        <!-- Styled Modern Toggles for Flags -->
                        <CheckBox x:Name=""chkAutoExit"" Style=""{StaticResource ModernToggle}"" Content=""Закрывать вместе с Claude""/>
                        <CheckBox x:Name=""chkTray"" Style=""{StaticResource ModernToggle}"" Content=""Уведомлять о сворачивании в трей"" Margin=""0,0,0,16""/>

                        <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                            <Button x:Name=""btnCfgCancel"" Content=""Отмена"" Width=""80"" Height=""28""
                                    Background=""#252538"" Foreground=""White"" BorderThickness=""0"" Margin=""0,0,8,0"" Cursor=""Hand"" FontSize=""11.5""/>
                            <Button x:Name=""btnCfgSave"" Content=""Сохранить"" Width=""95"" Height=""28""
                                    Background=""#1d9766"" Foreground=""White"" BorderThickness=""0"" FontWeight=""SemiBold"" Cursor=""Hand"" FontSize=""11.5""/>
                        </StackPanel>
                    </StackPanel>

                    <!-- 2. Activity Log View -->
                    <Grid x:Name=""viewLog"" Visibility=""Collapsed"">
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""*""/>
                            <RowDefinition Height=""Auto""/>
                        </Grid.RowDefinitions>

                        <TextBox x:Name=""txtLogBody"" Grid.Row=""0"" Background=""#101018"" Foreground=""#85e0a3""
                                 BorderBrush=""#252538"" BorderThickness=""1"" FontFamily=""Consolas, Cascadia Code""
                                 FontSize=""10.5"" IsReadOnly=""True"" TextWrapping=""Wrap""
                                 VerticalScrollBarVisibility=""Auto"" Padding=""7""/>

                        <StackPanel Grid.Row=""1"" Orientation=""Horizontal"" HorizontalAlignment=""Right"" Margin=""0,10,0,0"">
                            <Button x:Name=""btnCopyLog"" Content=""Копировать"" Width=""85"" Height=""26""
                                    Background=""#252538"" Foreground=""White"" BorderThickness=""0"" Margin=""0,0,8,0"" Cursor=""Hand"" FontSize=""11""/>
                            <Button x:Name=""btnClearLog"" Content=""Очистить"" Width=""75"" Height=""26""
                                    Background=""#252538"" Foreground=""White"" BorderThickness=""0"" Cursor=""Hand"" FontSize=""11""/>
                        </StackPanel>
                    </Grid>

                </Grid>
            </Grid>
        </Border>

    </Grid>
</Window>";
    }
}