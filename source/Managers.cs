using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Godot;
namespace HostGold;
public static partial class Entry {
    static Type jmcApi,ritsuAssemblyMarker;
    static bool broadcasting, registering;
    static readonly Dictionary<string,string> jmcKeys=new();
    static readonly List<string> integrations=new();
    static string[] SettingLabels=>new string[]{L("k047"),L("k048"),L("k049"),L("k050"),L("k051"),L("k052"),L("k053"),L("k054"),L("k055")};
    static Type FindType(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
    static object InvokeOptional(MethodInfo method,object target,params object[] values){
        var p=method.GetParameters();var args=new object[p.Length];
        for(int i=0;i<p.Length;i++)args[i]=i<values.Length?values[i]:p[i].HasDefaultValue?p[i].DefaultValue:null;
        return method.Invoke(target,args);
    }
    static void BroadcastSetting(string key){
        if(broadcasting||registering)return;
        broadcasting=true;
        try{
            if(configApi!=null)try{configApi.GetMethod("SetValue").Invoke(null,new[]{"HostGold",key,GetPreference(key)});}catch(Exception e){GD.PrintErr("[HostGold] ModConfig sync: "+e.Message);}
            if(jmcApi!=null&&jmcKeys.TryGetValue(key,out var storage))try{jmcApi.GetMethod("SetValue").Invoke(null,new[]{storage,ToManagerValue(key, key=="Hotkey"?FindType("JmcModLib.Config.UI.JmcKeyBinding"):typeof(Preferences).GetProperty(key).PropertyType),typeof(Entry).Assembly});}catch(Exception e){GD.PrintErr("[HostGold] JMC sync: "+e.Message);}
        }finally{broadcasting=false;}
    }
    static void RegisterManagers(){
        // Our existing settings file is authoritative, including when several frameworks coexist.
        var saved=SettingKeys.ToDictionary(k=>k,GetPreference);
        registering=true;
        try{
            RegisterModConfig();if(configApi!=null)integrations.Add("ModConfig");
            TryIntegration("JmcModLib",RegisterJmc);
            TryIntegration("RitsuLib",RegisterRitsu);
            foreach(var kv in saved)typeof(Preferences).GetProperty(kv.Key).SetValue(prefs,kv.Value);
        }finally{registering=false;}
        ApplyPreferences();SavePreferences();foreach(var key in SettingKeys)BroadcastSetting(key);
        integrationLabel.Text=integrations.Count==0?L("k056"):L("k044")+string.Join(" / ",integrations)+L("k057");
    }
    static void TryIntegration(string name,Func<bool> register){try{if(register())integrations.Add(name);}catch(Exception e){GD.PrintErr("[HostGold] "+name+" integration: "+e);integrations.Add(name+L("k058"));}}
    static object ToManagerValue(string key,Type type){
        object value=GetPreference(key);
        if(type.FullName=="JmcModLib.Config.UI.JmcKeyBinding"){
            long code=(long)value;int mods=0;
            if((code&(long)KeyModifierMask.MaskCtrl)!=0)mods|=1;if((code&(long)KeyModifierMask.MaskShift)!=0)mods|=2;
            if((code&(long)KeyModifierMask.MaskAlt)!=0)mods|=4;if((code&(long)KeyModifierMask.MaskMeta)!=0)mods|=8;
            var mt=type.Assembly.GetType("JmcModLib.Config.UI.JmcKeyModifiers");
            return Activator.CreateInstance(type,new object[]{(Key)(code&(long)KeyModifierMask.CodeMask),Enum.ToObject(mt,mods),true});
        }
        if(key=="Hotkey"&&type==typeof(string))return HotkeyText((long)value);
        return value;
    }
    static string HotkeyText(long code){
        var parts=new List<string>();
        if((code&(long)KeyModifierMask.MaskCtrl)!=0)parts.Add("Ctrl");if((code&(long)KeyModifierMask.MaskShift)!=0)parts.Add("Shift");
        if((code&(long)KeyModifierMask.MaskAlt)!=0)parts.Add("Alt");if((code&(long)KeyModifierMask.MaskMeta)!=0)parts.Add("Meta");
        parts.Add(((Key)(code&(long)KeyModifierMask.CodeMask)).ToString());return string.Join("+",parts);
    }
    static long ParseHotkeyText(string text){
        long code=0;int primary=0;
        foreach(string raw in text.Split('+')){string s=raw.Trim();switch(s.ToLowerInvariant()){
            case "ctrl":code|=(long)KeyModifierMask.MaskCtrl;break;case "shift":code|=(long)KeyModifierMask.MaskShift;break;
            case "alt":code|=(long)KeyModifierMask.MaskAlt;break;case "meta":code|=(long)KeyModifierMask.MaskMeta;break;
            default:if(!Enum.TryParse<Key>(s,true,out var k))k=OS.FindKeycodeFromString(s);code|=(long)k;primary++;break;
        }}return primary==1&&ValidKey(code)?code:0;
    }
    static object[] Callbacks<T>(string key){
        Func<T> get=()=> (T)ToManagerValue(key,typeof(T));
        Action<T> set=v=>{
            if(registering||broadcasting)return;
            object value=v;
            if(key=="Hotkey"){
                if(value is string s)value=ParseHotkeyText(s);
                else if(typeof(T).FullName=="JmcModLib.Config.UI.JmcKeyBinding"){
                    var t=typeof(T);long code=Convert.ToInt64(t.GetProperty("Keyboard").GetValue(v));int mods=Convert.ToInt32(t.GetProperty("Modifiers").GetValue(v));
                    if((mods&1)!=0)code|=(long)KeyModifierMask.MaskCtrl;if((mods&2)!=0)code|=(long)KeyModifierMask.MaskShift;
                    if((mods&4)!=0)code|=(long)KeyModifierMask.MaskAlt;if((mods&8)!=0)code|=(long)KeyModifierMask.MaskMeta;value=code;
                }
            }
            ChangeSetting(key,value);
        };return new object[]{get,set};
    }
    static object[] MakeCallbacks(Type type,string key)=>(object[])typeof(Entry).GetMethod(nameof(Callbacks),BindingFlags.NonPublic|BindingFlags.Static).MakeGenericMethod(type).Invoke(null,new object[]{key});
    static bool RegisterJmc(){
        var api=FindType("JmcModLib.Config.ConfigManager");if(api==null)return false;
        var registry=FindType("JmcModLib.Core.ModRegistry");
        registry.GetMethods().Single(m=>m.Name=="Register"&&!m.IsGenericMethod&&m.GetParameters().Length==4).Invoke(null,new object[]{"HostGold",L("k059"),"0.6.0",typeof(Entry).Assembly});
        var register=api.GetMethods().Single(m=>m.Name=="RegisterConfig"&&m.IsGenericMethodDefinition);
        for(int i=0;i<SettingKeys.Length;i++){
            string key=SettingKeys[i];var type=key=="Hotkey"?FindType("JmcModLib.Config.UI.JmcKeyBinding"):typeof(Preferences).GetProperty(key).PropertyType;
            var callbacks=MakeCallbacks(type,key);object ui;
            if(key=="Hotkey")ui=Activator.CreateInstance(FindType("JmcModLib.Config.UI.UIKeybindAttribute"),new object[]{true,false});
            else if(type==typeof(bool))ui=Activator.CreateInstance(FindType("JmcModLib.Config.UI.UIToggleAttribute"));
            else ui=Activator.CreateInstance(FindType("JmcModLib.Config.UI.UIIntSliderAttribute"),new object[]{SettingMin(key),SettingMax(key),1});
            jmcKeys[key]=(string)register.MakeGenericMethod(type).Invoke(null,new object[]{SettingLabels[i],callbacks[0],callbacks[1],L("k059"),null,ui,key,null,null,null,null,null,i,false,typeof(Entry).Assembly});
        }
        jmcApi=api;return true;
    }
    static object RitsuText(string s)=>ritsuAssemblyMarker.Assembly.GetType("STS2RitsuLib.Settings.ModSettingsText").GetMethod("Literal").Invoke(null,new object[]{s});
    static Delegate BuilderAction(Type delegateType,string method)=>Delegate.CreateDelegate(delegateType,typeof(Entry).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static));
    static bool RegisterRitsu(){
        var registry=FindType("STS2RitsuLib.Settings.ModSettingsRegistry");if(registry==null)return false;
        // The adapter already registers ModConfig's page in this hub; avoid duplicate pages.
        if(configApi!=null&&configApi.Assembly.GetName().Name.IndexOf("Adapter",StringComparison.OrdinalIgnoreCase)>=0)return false;
        ritsuAssemblyMarker=registry;
        var method=registry.GetMethods().Single(m=>m.Name=="Register"&&m.GetParameters().Length==3);
        method.Invoke(null,new object[]{"HostGold",BuilderAction(method.GetParameters()[1].ParameterType,nameof(BuildRitsuPage)),"host-tools"});return true;
    }
    static void BuildRitsuPage(object page){
        page.GetType().GetMethod("WithTitle").Invoke(page,new[]{RitsuText(L("k059"))});
        page.GetType().GetMethod("WithModDisplayName").Invoke(page,new[]{RitsuText(L("k059"))});
        var add=page.GetType().GetMethod("AddSection");add.Invoke(page,new object[]{"general",BuilderAction(add.GetParameters()[1].ParameterType,nameof(BuildRitsuSection))});
    }
    static void BuildRitsuSection(object section){
        for(int i=0;i<SettingKeys.Length;i++){
            string key=SettingKeys[i];var type=key=="Hotkey"?typeof(string):typeof(Preferences).GetProperty(key).PropertyType;
            var callbacks=MakeCallbacks(type,key);
            var bindingType=ritsuAssemblyMarker.Assembly.GetType("STS2RitsuLib.Settings.ModSettingsCallbackValueBinding`1").MakeGenericType(type);
            var ctor=bindingType.GetConstructors().Single();var scope=Enum.Parse(ctor.GetParameters()[2].ParameterType,"Global");
            var binding=ctor.Invoke(new object[]{"HostGold",key,scope,callbacks[0],callbacks[1],new Action(SavePreferences)});
            var label=RitsuText(SettingLabels[i]);
            if(key=="Hotkey")InvokeOptional(section.GetType().GetMethods().Single(m=>m.Name=="AddKeyBinding"&&m.GetParameters().Length==7),section,key,label,binding,true,false,false);
            else if(type==typeof(bool))InvokeOptional(section.GetType().GetMethod("AddToggle"),section,key,label,binding);
            else InvokeOptional(section.GetType().GetMethod("AddIntSlider"),section,key,label,binding,SettingMin(key),SettingMax(key),1);
        }
    }
}

