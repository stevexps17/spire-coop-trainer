using MegaCrit.Sts2.Core.Nodes;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Collections.Generic;
using Godot;
namespace HostGold;
public static partial class Entry {
    public sealed class Preferences {
        public long Hotkey {get;set;}=(long)Key.F8;
        public bool ShowIcon {get;set;}=true;
        public int IconSize {get;set;}=44;
        public int UiScale {get;set;}=80;
        public int PanelWidth {get;set;}=570;
        public int PanelHeight {get;set;}=0;
        public int IconOpacity {get;set;}=100;
        public bool LockDrag {get;set;}=false;
        public int DefaultGold {get;set;}=10;
        public int DefaultCards {get;set;}=1;
        public bool AutoClose {get;set;}=true;
    }
    static Preferences prefs=new Preferences();
    static bool capturing,syncingSettings;
    
    static Label integrationLabel;
    static CheckButton lockCheck,closeCheck;
    static SpinBox goldDefault,cardsDefault,uiScaleInput;
    static Type configApi;
    static string SettingsPath=>Path.Combine(OS.GetUserDataDir(),"HostGold","settings.json");
    static readonly string[] SettingKeys={"Hotkey","ShowIcon","LockDrag","DefaultGold","DefaultCards","AutoClose","IconSize","IconOpacity","UiScale"};
    static int SettingMin(string key)=>key=="UiScale"?50:key=="IconSize"?24:key=="IconOpacity"?10:1;
    static int SettingMax(string key)=>key=="UiScale"?150:key=="IconSize"?128:key=="IconOpacity"?100:key=="DefaultGold"?999999:10;
    static bool ValidKey(long code) {
        var key=(Key)(code & (long)KeyModifierMask.CodeMask);
        return key!=Key.None && key!=Key.Shift && key!=Key.Ctrl && key!=Key.Alt && key!=Key.Meta && key!=Key.Escape && Enum.IsDefined(typeof(Key),key);
    }
    static void Normalize(Preferences p) {
        if(!ValidKey(p.Hotkey))p.Hotkey=(long)Key.F8;
        p.PanelHeight=p.PanelHeight<=0?0:Math.Clamp(p.PanelHeight,320,1200);
        p.PanelWidth=Math.Clamp(p.PanelWidth,480,1000);
        p.UiScale=Math.Clamp(p.UiScale,50,150);
        p.IconSize=Math.Clamp(p.IconSize,24,128);p.IconOpacity=Math.Clamp(p.IconOpacity,10,100);
        p.DefaultGold=Math.Clamp(p.DefaultGold,1,999999);p.DefaultCards=Math.Clamp(p.DefaultCards,1,10);
    }
    static void LoadPreferences() {
        try { if(File.Exists(SettingsPath))prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(SettingsPath))??new Preferences();Normalize(prefs); }
        catch(Exception e){prefs=new Preferences();GD.PrintErr("[HostGold] Settings load: "+e.Message);}
    }
    static void SavePreferences() {
        try {Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));var tmp=SettingsPath+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(prefs));File.Move(tmp,SettingsPath,true);}
        catch(Exception e){GD.PrintErr("[HostGold] Settings save: "+e.Message);if(integrationLabel!=null)integrationLabel.Text=L("k060")+e.Message;}
    }
    static object GetPreference(string key)=>typeof(Preferences).GetProperty(key).GetValue(prefs);
    static void ChangeSetting(string key,object value,bool notifyManager=true) {
        if(syncingSettings||broadcasting||registering)return;
        try {
            var property=typeof(Preferences).GetProperty(key);
            object converted=value is JsonElement j?JsonSerializer.Deserialize(j.GetRawText(),property.PropertyType):Convert.ChangeType(value,property.PropertyType);
            if(key=="Hotkey"&&!ValidKey((long)converted))return;
            property.SetValue(prefs,converted);Normalize(prefs);
            if(key=="Hotkey"){capturing=false;keyDown=true;}
            ApplyPreferences();SavePreferences();
            BroadcastSetting(key);
        }catch(Exception e){GD.PrintErr("[HostGold] Settings change: "+e.Message);}
    }
    static void ApplyPreferences() {
        if(goldIcon==null)return;
        goldIcon.Visible=prefs.ShowIcon;
        goldIcon.Size=new Vector2(prefs.IconSize,prefs.IconSize);
        goldIcon.Modulate=new Color(1,1,1,prefs.IconOpacity/100f);
        if(iconGlyph!=null){iconGlyph.Size=goldIcon.Size;iconGlyph.AddThemeFontSizeOverride("font_size",Math.Max(12,(int)Math.Round(prefs.IconSize*28.0/44.0)));}
        if(iconStyle!=null){int radius=prefs.IconSize/2;iconStyle.CornerRadiusTopLeft=radius;iconStyle.CornerRadiusTopRight=radius;iconStyle.CornerRadiusBottomLeft=radius;iconStyle.CornerRadiusBottomRight=radius;}
        goldIcon.Position=KeepOnScreen(goldIcon,goldIcon.Position);
        goldIcon.TooltipText=L("k041")+OS.GetKeycodeString((Key)prefs.Hotkey)+L("k042");
        if(amount!=null)amount.Value=prefs.DefaultGold;
        foreach(var b in browsers)if(b.Count!=null)b.Count.Value=prefs.DefaultCards;
        syncingSettings=true;
        try {
            lockCheck?.SetPressedNoSignal(prefs.LockDrag);closeCheck?.SetPressedNoSignal(prefs.AutoClose);
            if(goldDefault!=null)goldDefault.Value=prefs.DefaultGold;
            if(cardsDefault!=null)cardsDefault.Value=prefs.DefaultCards;
            if(uiScaleInput!=null)uiScaleInput.Value=prefs.UiScale;
        }finally{syncingSettings=false;}
        FitArtLayout();
    }
    static bool HotkeyPressed() {
        if(capturing || !NGame.IsGameFocusedWindow())return false;
        var focus=((SceneTree)Engine.GetMainLoop()).Root.GuiGetFocusOwner();
        if(focus is LineEdit || focus is TextEdit)return false;
        long code=prefs.Hotkey;
        return Input.IsKeyPressed((Key)(code&(long)KeyModifierMask.CodeMask))
            && Input.IsKeyPressed(Key.Ctrl)==((code&(long)KeyModifierMask.MaskCtrl)!=0)
            && Input.IsKeyPressed(Key.Shift)==((code&(long)KeyModifierMask.MaskShift)!=0)
            && Input.IsKeyPressed(Key.Alt)==((code&(long)KeyModifierMask.MaskAlt)!=0)
            && Input.IsKeyPressed(Key.Meta)==((code&(long)KeyModifierMask.MaskMeta)!=0);
    }
    static void BuildSettings(TabContainer tabs) {
        var page=new VBoxContainer {Name=L("k061")};page.AddThemeConstantOverride("separation",10);tabs.AddChild(page);
        page.AddChild(new Label {Text=L("k062"),AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var scaleRow=new HBoxContainer();scaleRow.AddChild(new Label {Text=L("k055")});uiScaleInput=new SpinBox {MinValue=50,MaxValue=150,Step=5,Value=prefs.UiScale,Suffix="%",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};uiScaleInput.ValueChanged+=v=>ChangeSetting("UiScale",(int)v);scaleRow.AddChild(uiScaleInput);page.AddChild(scaleRow);
        uiScaleInput.TooltipText=L("k063");
        lockCheck=new CheckButton {Text=L("k064")};lockCheck.Toggled+=v=>ChangeSetting("LockDrag",v);page.AddChild(lockCheck);
        closeCheck=new CheckButton {Text=L("k065")};closeCheck.Toggled+=v=>ChangeSetting("AutoClose",v);page.AddChild(closeCheck);
        var row=new HBoxContainer();row.AddChild(new Label {Text=L("k050")});goldDefault=new SpinBox {MinValue=1,MaxValue=999999,Step=1,UpdateOnTextChanged=false};goldDefault.ValueChanged+=v=>ChangeSetting("DefaultGold",(int)v);goldDefault.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(goldDefault);page.AddChild(row);
        row=new HBoxContainer();row.AddChild(new Label {Text=L("k051")});cardsDefault=new SpinBox {MinValue=1,MaxValue=10,Step=1};cardsDefault.ValueChanged+=v=>ChangeSetting("DefaultCards",(int)v);cardsDefault.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(cardsDefault);page.AddChild(row);
        var recover=new Button {Text=L("k066")};recover.Pressed+=ResetToolState;page.AddChild(recover);
        var reset=new Button {Text=L("k067")};reset.Pressed+=()=>{var defaults=new Preferences();foreach(string key in SettingKeys)ChangeSetting(key,typeof(Preferences).GetProperty(key).GetValue(defaults));};page.AddChild(reset);
        integrationLabel=new Label {Text=L("k068"),AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(400,40)};page.AddChild(integrationLabel);
        ApplyPreferences();
        Callable.From(RegisterManagers).CallDeferred();
    }
    static Array MakeConfigEntries(Type entryType,Type kind) {
        var values=new[]{("Hotkey",L("k047"),"KeyBind"),("ShowIcon",L("k048"),"Toggle"),("LockDrag",L("k049"),"Toggle"),("DefaultGold",L("k050"),"Slider"),("DefaultCards",L("k051"),"Slider"),("AutoClose",L("k052"),"Toggle"),("IconSize",L("k053"),"Slider"),("IconOpacity",L("k054"),"Slider"),("UiScale",L("k055"),"Slider")};
        var entries=Array.CreateInstance(entryType,values.Length);
        for(int i=0;i<values.Length;i++){
            var (key,label,type)=values[i];object entry=Activator.CreateInstance(entryType);
            void Set(string prop,object val)=>entryType.GetProperty(prop).SetValue(entry,val);
            Set("Key",key);Set("Label",label);Set("Type",Enum.Parse(kind,type));Set("DefaultValue",GetPreference(key));
            Set("OnChanged",new Action<object>(v=>ChangeSetting(key,v,false)));
            if(type=="Slider"){Set("Min",(float)SettingMin(key));Set("Max",(float)SettingMax(key));Set("Step",1f);Set("Format","F0");}
            entries.SetValue(entry,i);
        }
        return entries;
    }
    static void RegisterModConfig() {
        try {
            var assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetType("ModConfig.ModConfigApi")!=null);
            if(assembly==null){integrationLabel.Text=L("k069");return;}
            var api=assembly.GetType("ModConfig.ModConfigApi");
            var entries=MakeConfigEntries(assembly.GetType("ModConfig.ConfigEntry"),assembly.GetType("ModConfig.ConfigType"));
            api.GetMethods().Single(m=>m.Name=="Register"&&m.GetParameters().Length==3).Invoke(null,new object[]{"HostGold",L("k059"),entries});
            configApi=api;
            var get=api.GetMethod("GetValue");
            foreach(string key in SettingKeys){var p=typeof(Preferences).GetProperty(key);var val=get.MakeGenericMethod(p.PropertyType).Invoke(null,new object[]{"HostGold",key});ChangeSetting(key,val,false);}
            integrationLabel.Text=L("k070");
            GD.Print("[HostGold 0.6.0] Registered 9 ModConfig settings");
        }catch(Exception e){configApi=null;integrationLabel.Text=L("k071");GD.PrintErr("[HostGold] ModConfig: "+e);}
    }
}



