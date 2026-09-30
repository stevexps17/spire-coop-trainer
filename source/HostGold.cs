using System;
using System.IO;
using System.Linq;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;
using MegaCrit.Sts2.Core.Rewards;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace HostGold;
[ModInitializer(nameof(Initialize))]
public static partial class Entry {
    const string ExpectedHash="0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9";
    static PanelContainer panel;
    static Control goldIcon, dragTarget;
    static Label iconGlyph;
    static StyleBoxFlat iconStyle;
    static Vector2 dragMouseStart, dragPositionStart, panelPositionStart;
    static bool dragMoved;

    static Label stateLabel, resultLabel;
    static Button addButton;
    static SpinBox amount;
    static bool busy, keyDown, compatible;
    static DateTime last=DateTime.MinValue;
    static Godot.Timer timer;
    public static void Initialize() {
        LoadPreferences();
        using(var stream=File.OpenRead(typeof(RunManager).Assembly.Location))
        using(var sha=SHA256.Create()) compatible=Convert.ToHexString(sha.ComputeHash(stream))==ExpectedHash;
        Callable.From(() => {
            try { Attach(((SceneTree)Engine.GetMainLoop()).Root); }
            catch(Exception e) { GD.PrintErr("[HostGold] Attach failed: "+e); }
        }).CallDeferred();
        GD.Print("[HostGold 0.6.0] Loaded; draggable coin UI; exact assembly match="+compatible);
    }
    public static void Attach(Node __instance) {
        if(timer!=null && GodotObject.IsInstanceValid(timer))return;
        InitializeLanguage();
        var layer=new CanvasLayer {Layer=100,Name="HostGoldLayer"};
        __instance.AddChild(layer);
        panel=new PanelContainer {Position=new Vector2(28,100),CustomMinimumSize=new Vector2(prefs.PanelWidth,0),Visible=false};
        layer.AddChild(panel);
        spireTheme=CreateSpireTheme();panel.Theme=spireTheme;
        var coinStyle=new StyleBoxFlat {
            BgColor=new Color("30271f"),BorderColor=new Color("d1aa62"),
            BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,
            CornerRadiusTopLeft=22,CornerRadiusTopRight=22,CornerRadiusBottomLeft=22,CornerRadiusBottomRight=22
        };
        var coin=new Panel {Position=new Vector2(28,56),Size=new Vector2(44,44),
            MouseFilter=Control.MouseFilterEnum.Stop,MouseDefaultCursorShape=Control.CursorShape.PointingHand,
            TooltipText=L("k020")};
        coin.AddThemeStyleboxOverride("panel",coinStyle);
        var glyph=new Label {Text="$",Size=new Vector2(44,44),HorizontalAlignment=HorizontalAlignment.Center,
            VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};
        glyph.AddThemeColorOverride("font_color",new Color("53370c"));glyph.AddThemeFontSizeOverride("font_size",28);
        coin.AddChild(glyph);layer.AddChild(coin);goldIcon=coin;iconGlyph=glyph;iconStyle=coinStyle;AddCoinArt(coin);
        coin.GuiInput+=e=>BeginDrag(e,goldIcon);
        var style=Surface("201d24","ab8550",18,2,8);style.ShadowColor=new Color(0,0,0,0.55f);style.ShadowSize=14;
        panel.AddThemeStyleboxOverride("panel",style);
        var shell=new VBoxContainer();shell.AddThemeConstantOverride("separation",12);panel.AddChild(shell);
        var title=new Label {Text=L("k021"),MouseFilter=Control.MouseFilterEnum.Stop,MouseDefaultCursorShape=Control.CursorShape.Move,AutowrapMode=TextServer.AutowrapMode.WordSmart};
        windowTitle=title;title.AddThemeFontSizeOverride("font_size",28);title.AddThemeColorOverride("font_color",new Color("ecd29a"));
        title.GuiInput+=e=>BeginDrag(e,panel);shell.AddChild(title);
        var subtitle=new Label {Text="HOST TOOLS v0.6.1",TooltipText=L("k022")};MutedLabel(subtitle);shell.AddChild(subtitle);
        shell.AddChild(new HSeparator());
        contentScroll=new ScrollContainer {CustomMinimumSize=new Vector2(0,320),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};shell.AddChild(contentScroll);
        var box=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};box.AddThemeConstantOverride("separation",14);contentScroll.AddChild(box);
        stateLabel=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};stateLabel.AddThemeColorOverride("font_color",new Color("d7be88")); box.AddChild(stateLabel);
        BuildTargets(box);
        var tabs=new TabContainer {CustomMinimumSize=new Vector2(440,0),UseHiddenTabsForMinSize=false};box.AddChild(tabs);
        var goldPage=new VBoxContainer {Name=L("k024")};goldPage.AddThemeConstantOverride("separation",12);tabs.AddChild(goldPage);
        goldPage.AddChild(new Label {Text=L("k025")});
        amount=new SpinBox {MinValue=1,MaxValue=999999,Step=1,Value=10,UpdateOnTextChanged=true};goldPage.AddChild(amount);
        addButton=new Button {ThemeTypeVariation="HostPrimary",Text=L("k026")}; addButton.Pressed+=()=>{_ = AddGold();};goldPage.AddChild(addButton);
        BuildBrowser(tabs,true);BuildBrowser(tabs,false);BuildSettings(tabs);
        tabs.TabChanged+=_=>RefreshBrowsers();
        resultLabel=new Label {Text="",AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(400,48)};MutedLabel(resultLabel);shell.AddChild(new HSeparator());shell.AddChild(resultLabel);
        var close=new Button {Text=L("k028")};close.Pressed+=()=>{capturing=false;panel.Hide();};shell.AddChild(close);
        ApplyArtFonts();
        TranslateControls(panel);
        BuildResizeEdges(layer);
        timer=new Godot.Timer {WaitTime=0.016,ProcessMode=Node.ProcessModeEnum.Always};
        timer.Timeout+=Tick;__instance.AddChild(timer);timer.Start();
        GD.Print("[HostGold] F8 panel attached");
    }
    static Vector2 KeepOnScreen(Control control,Vector2 position) {
        var screen=control.GetViewportRect().Size;
        return new Vector2(Mathf.Clamp(position.X,0,Mathf.Max(0,screen.X-control.Size.X*control.Scale.X)),
            Mathf.Clamp(position.Y,0,Mathf.Max(0,screen.Y-control.Size.Y*control.Scale.Y)));
    }
    static void TogglePanel() {
        panel.Visible=!panel.Visible;
        if(panel.Visible) {
            RefreshBrowsers();
            panel.Position=KeepOnScreen(panel,goldIcon.Position+new Vector2(0,goldIcon.Size.Y+8));
            Callable.From(()=>panel.Position=KeepOnScreen(panel,panel.Position)).CallDeferred();
        }
    }
    static void BeginDrag(InputEvent e,Control target) {
        if(e is InputEventMouseButton button && button.ButtonIndex==MouseButton.Left && button.Pressed) {
            dragTarget=target;dragMouseStart=target.GetGlobalMousePosition();
            dragPositionStart=target.Position;panelPositionStart=panel.Position;dragMoved=false;
            target.AcceptEvent();
        }
    }
    static void UpdateDrag() {
        if(dragTarget==null)return;
        if(!NGame.IsGameFocusedWindow()){dragTarget=null;return;}
        var delta=dragTarget.GetGlobalMousePosition()-dragMouseStart;
        if(delta.LengthSquared()>=36)dragMoved=true;
        if(dragMoved && !prefs.LockDrag) {
            dragTarget.Position=KeepOnScreen(dragTarget,dragPositionStart+delta);
            if(dragTarget==goldIcon && panel.Visible)
                panel.Position=KeepOnScreen(panel,panelPositionStart+(goldIcon.Position-dragPositionStart));
        }
        if(!Input.IsMouseButtonPressed(MouseButton.Left)) {
            bool click=dragTarget==goldIcon&&!dragMoved;
            dragTarget=null;
            if(click)TogglePanel();
        }
    }
    static Player Check(out string reason) {
        reason="";
        if(!compatible){reason=L("k029");return null;}
        var run=RunManager.Instance;
        if(run==null || !run.IsInProgress || run.IsGameOver || run.IsCleaningUp){reason=L("k030");return null;}
        var net=run.NetService;
        if(net==null || (net.Type!=NetGameType.Host && net.Type!=NetGameType.Singleplayer)){reason=L("k031");return null;}
        if(net.IsGameLoading || (net.Type==NetGameType.Host && !net.IsConnected)){reason=L("k032");return null;}
        var combat=CombatManager.Instance;
        if(combat.IsInProgress || combat.IsStarting || combat.IsEnding){reason=L("k033");return null;}
        if(run.ActionExecutor?.IsRunning==true){reason=L("k034");return null;}
        var rs=run.DebugOnlyGetState();
        if(rs==null || rs.CurrentRoom==null || run.RewardSynchronizer==null){reason=L("k035");return null;}
        var me=LocalContext.GetMe(rs);
        if(me==null || me.NetId!=net.NetId || me.Creature.IsDead){reason=L("k036");return null;}
        return me;
    }
    static void Tick() {
        try {
            PollLanguage();
            UpdateResize();
            FitArtLayout();
            RefreshTargets();
            UpdateDrag();
            goldIcon.Position=KeepOnScreen(goldIcon,goldIcon.Position);
            if(panel.Visible)panel.Position=KeepOnScreen(panel,panel.Position);
            bool pressed=HotkeyPressed();
            if(pressed&&!keyDown&&NGame.IsGameFocusedWindow())TogglePanel();
            keyDown=pressed;
            if(!panel.Visible)return;
            var me=Check(out string reason);
            stateLabel.Text=me==null?reason:L("k037")+me.Gold;
            addButton.Disabled=busy || me==null || Targets().Count==0 || (DateTime.UtcNow-last).TotalSeconds<2;
            UpdateBrowserButtons(addButton.Disabled);
        }catch(Exception e){GD.PrintErr("[HostGold] UI: "+e.Message);}
    }
    static async Task AddGold() {
        if(busy || (DateTime.UtcNow-last).TotalSeconds<2)return;
        var targets=Targets();
        if(!ValidateTargets(targets,out string reason)){resultLabel.Text=reason;return;}
        int delta=(int)amount.Value;if(delta<1||delta>999999)return;
        if(targets.Any(p=>(long)p.Gold+delta>1000000)){resultLabel.Text=L("k038");return;}
        var state=RunManager.Instance.DebugOnlyGetState();
        busy=true;last=DateTime.UtcNow;int completed=0;
        try {
            foreach(var player in targets){
                if(!ReferenceEquals(state,RunManager.Instance.DebugOnlyGetState())||!ValidateTargets(targets,out reason))throw new InvalidOperationException(reason);
                SyncForTarget(player,new RewardObtainedMessage {location=RunManager.Instance.RunLocationTargetedBuffer.CurrentLocation,wasSkipped=false,rewardType=RewardType.Gold,goldAmount=delta});
                await PlayerCmd.GainGold(delta,player,false);completed++;
            }
            resultLabel.Text=L("k039" ,delta,completed);
        }catch(Exception e){resultLabel.Text=L("k040" ,completed,targets.Count);GD.PrintErr("[HostGold] Gold: "+e);panel.Show();}
        finally{busy=false;last=DateTime.UtcNow;}
    }
}

