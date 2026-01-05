<!-- Badge Icon for Changed Folders (bottom-right corner) -->
<FontIcon 
    Glyph="&#xE7BA;"
    FontSize="10"
    HorizontalAlignment="Right"
    VerticalAlignment="Bottom"
    Foreground="{ThemeResource SystemFillColorCriticalBrush}"
    Margin="0,0,-2,-2"
    Visibility="{Binding HasChanged, Mode=OneWay}">
    <ToolTipService.ToolTip>
        <ToolTip Content="Folder has changed since last catalog"/>
    </ToolTipService.ToolTip>
</FontIcon>
